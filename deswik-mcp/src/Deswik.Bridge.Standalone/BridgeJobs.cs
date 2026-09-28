using System.Text.Json;
using Deswik.Bridge.Models;

namespace Deswik.Bridge.Standalone;

/// <summary>Process-local job state. A new bridge process starts with an empty registry.</summary>
public sealed class BridgeJobs
{
    public sealed record Snapshot(
        string JobId, string Action, string State, bool CancelRequested,
        DateTimeOffset SubmittedAt, DateTimeOffset DeadlineAt,
        DateTimeOffset? FinishedAt, object? Result, string? Error,
        string? ErrorCode, int CompletedUnits, int TotalUnits,
        IReadOnlyList<ulong> Handles);

    private sealed class Entry
    {
        public required string Id;
        public required string Action;
        public required DateTimeOffset SubmittedAt;
        public required DateTimeOffset DeadlineAt;
        public string State = "queued";
        public bool CancelRequested;
        public bool WriteStarted;
        public DateTimeOffset? FinishedAt;
        public object? Result;
        public string? Error;
        public string? ErrorCode;
        public int CompletedUnits;
        public int TotalUnits;
        public IReadOnlyList<ulong> Handles = Array.Empty<ulong>();
        public CancellationTokenSource Cancellation = new();
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private readonly TimeProvider _clock;

    public event Action<string>? WriteCancellationRequested;

    public BridgeJobs(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public Snapshot Submit(string action, TimeSpan ceiling,
        Func<string, CancellationToken, Task<McpResponse>> execute)
    {
        if (ceiling <= TimeSpan.Zero || ceiling > TimeSpan.FromMinutes(30))
            throw new ArgumentOutOfRangeException(nameof(ceiling));
        var now = _clock.GetUtcNow();
        var entry = new Entry
        {
            Id = Guid.NewGuid().ToString("D"), Action = action,
            SubmittedAt = now, DeadlineAt = now + ceiling
        };
        lock (_sync) _entries.Add(entry.Id, entry);
        _ = Task.Run(() => RunAsync(entry, execute));
        _ = Task.Run(async () =>
        {
            await Task.Delay(ceiling);
            Expire(entry.Id);
        });
        return View(entry);
    }

    public Snapshot? Get(string id)
    {
        lock (_sync) return _entries.TryGetValue(id, out var entry) ? View(entry) : null;
    }

    public Snapshot? Cancel(string id)
    {
        Snapshot? snapshot;
        var signalWrite = false;
        lock (_sync)
        {
            if (!_entries.TryGetValue(id, out var entry)) return null;
            if (IsFinal(entry.State)) return View(entry);
            signalWrite = entry.WriteStarted && !entry.CancelRequested;
            entry.CancelRequested = true;
            if (!entry.WriteStarted)
            {
                entry.State = "cancelled";
                entry.Error = "Job cancelled before a production commit began";
                entry.ErrorCode = "job_cancelled";
                entry.FinishedAt = _clock.GetUtcNow();
                entry.Cancellation.Cancel();
            }
            snapshot = View(entry);
        }
        if (signalWrite) WriteCancellationRequested?.Invoke(id);
        return snapshot;
    }

    /// <summary>Atomic checkpoint immediately before a production commit is dispatched.</summary>
    public bool TryStartWrite(string id)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.State != "running" ||
                entry.CancelRequested || _clock.GetUtcNow() >= entry.DeadlineAt) return false;
            entry.WriteStarted = true;
            return true;
        }
    }

    public void ReportProgress(string id, int completed, int total, ulong? handle = null)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(id, out var entry) ||
                entry.State is "completed" or "failed" or "cancelled") return;
            if (total < 0 || completed < 0 || completed > total ||
                completed < entry.CompletedUnits) return;
            entry.CompletedUnits = completed;
            entry.TotalUnits = total;
            if (handle.HasValue && !entry.Handles.Contains(handle.Value))
                entry.Handles = entry.Handles.Append(handle.Value).ToArray();
        }
    }

    private void Expire(string id)
    {
        var signalWrite = false;
        lock (_sync)
        {
            if (!_entries.TryGetValue(id, out var entry) || IsFinal(entry.State)) return;
            entry.CancelRequested = true;
            entry.Cancellation.Cancel();
            if (entry.WriteStarted)
            {
                signalWrite = true;
                // A started commit cannot be interrupted safely. Until its
                // result arrives, its geometry outcome is unknown.
                entry.State = "partial";
                entry.Error = "Commit exceeded its deadline; inspect the drawing and final job result";
                entry.ErrorCode = "job_deadline_write_unknown";
            }
            else
            {
                entry.State = "cancelled";
                entry.Error = "Job deadline expired before a production commit began";
                entry.ErrorCode = "job_expired";
            }
            entry.FinishedAt = _clock.GetUtcNow();
        }
        if (signalWrite) WriteCancellationRequested?.Invoke(id);
    }

    private async Task RunAsync(Entry entry,
        Func<string, CancellationToken, Task<McpResponse>> execute)
    {
        lock (_sync)
        {
            if (entry.State != "queued") return;
            entry.State = "running";
        }
        McpResponse response;
        try { response = await execute(entry.Id, entry.Cancellation.Token); }
        catch (OperationCanceledException)
        {
            Cancel(entry.Id);
            return;
        }
        catch (Exception ex)
        {
            response = McpResponse.Fail(entry.Id, ex.Message, "job_execution_failed");
        }

        lock (_sync)
        {
            // A late result may resolve an expired in-progress write. A
            // cancelled read result is discarded because no write began.
            if (IsFinal(entry.State) && !(entry.State == "partial" && entry.WriteStarted &&
                                           entry.ErrorCode == "job_deadline_write_unknown")) return;
            entry.State = response.Success ? "completed" :
                string.Equals(response.ErrorCode, "partial_write", StringComparison.Ordinal)
                    ? "partial" : string.Equals(response.ErrorCode, "job_cancelled", StringComparison.Ordinal)
                        ? "cancelled" : "failed";
            entry.Result = response.Data;
            entry.Error = response.Error;
            entry.ErrorCode = response.ErrorCode;
            entry.Handles = ExtractHandles(response.Data);
            if (entry.State == "completed" && entry.TotalUnits > 0)
                entry.CompletedUnits = entry.TotalUnits;
            entry.FinishedAt = _clock.GetUtcNow();
        }
    }

    private static IReadOnlyList<ulong> ExtractHandles(object? data)
    {
        if (data == null) return Array.Empty<ulong>();
        var json = JsonSerializer.SerializeToElement(data);
        if (!json.TryGetProperty("Handles", out var handles) &&
            !json.TryGetProperty("handles", out handles)) return Array.Empty<ulong>();
        return handles.ValueKind == JsonValueKind.Array
            ? handles.EnumerateArray().Select(item => item.GetUInt64()).ToArray()
            : Array.Empty<ulong>();
    }

    private static bool IsFinal(string state) =>
        state is "completed" or "failed" or "cancelled" or "partial";

    private static Snapshot View(Entry entry) => new(
        entry.Id, entry.Action, entry.State, entry.CancelRequested,
        entry.SubmittedAt, entry.DeadlineAt, entry.FinishedAt,
        entry.Result, entry.Error, entry.ErrorCode,
        entry.CompletedUnits, entry.TotalUnits, entry.Handles);
}

public static class BridgeJobPolicy
{
    public const string SubmitAction = "job.submit";
    public const string GetAction = "job.get";
    public const string CancelAction = "job.cancel";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "get_cad_document", "get_cad_layers", "get_cad_layer_attributes",
        "get_cad_elements", "get_cad_selection", "get_cad_polyface_info",
        "get_cad_polylines_under",
        "get_cad_blasthole_details", "get_cad_ugdrillhole_details",
        GuardedWritePolicy.CommitAction, GuardedWritePolicy.RollbackAction,
    };

    public static bool CanSubmit(string action) => Allowed.Contains(action);
    public static bool IsWrite(string action) =>
        action is GuardedWritePolicy.CommitAction or GuardedWritePolicy.RollbackAction;

    public static TimeSpan CeilingFor(string action)
    {
        var key = "DESWIK_JOB_TIMEOUT_SECONDS_" + action.ToUpperInvariant().Replace('.', '_');
        var raw = Environment.GetEnvironmentVariable(key);
        return int.TryParse(raw, out var seconds) && seconds is >= 1 and <= 1800
            ? TimeSpan.FromSeconds(seconds) : TimeSpan.FromMinutes(30);
    }
}
