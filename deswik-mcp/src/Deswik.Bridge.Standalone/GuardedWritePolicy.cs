using System.Security.Cryptography;
using System.Text.Json;
using Deswik.Bridge.Models;

namespace Deswik.Bridge.Standalone;

public static class GuardedWritePolicy
{
    public const string PreviewLayer = "_MCP_PREVIEW";
    public const string PreviewAction = "preview_ugdrillholes";
    public const string ApprovalAction = "get_write_approval";
    public const string CommitAction = "commit_ugdrillholes";
    public const string PrepareRollbackAction = "prepare_rollback_ugdrillholes";
    public const string RollbackAction = "rollback_ugdrillholes";
    public const string InternalCommitAction = "_commit_ugdrillholes_authorized";
    public const string InternalRollbackAction = "_rollback_ugdrillholes_authorized";
    public const string InternalApprovalAction = "_human_write_approval";

    public static readonly IReadOnlySet<string> LegacyWriterActions = new HashSet<string>(StringComparer.Ordinal)
    {
        "create_cad_layer",
        "draw_cad_text",
        "draw_cad_polylines",
        "slice_cad_polyface",
        "draw_cad_blastholes",
        "draw_cad_ugdrillholes",
    };

    public static McpResponse? RefuseUnfenced(McpCommand command)
    {
        if (command.Action is InternalCommitAction or InternalRollbackAction or InternalApprovalAction)
            return McpResponse.Fail(command.Id, "Internal write action is not callable", "forbidden_unfenced");
        if (!LegacyWriterActions.Contains(command.Action)) return null;
        if (command.Action == "create_cad_layer" && ExactPreviewLayer(command.Params)) return null;
        return McpResponse.Fail(command.Id, $"Writer '{command.Action}' requires the guarded write flow", "forbidden_unfenced");
    }

    public static bool PreviewLayerIsDirty(IEnumerable<ulong> existingHandles, IEnumerable<ulong> recordedHandles)
    {
        var recorded = recordedHandles.ToHashSet();
        return existingHandles.Any(handle => !recorded.Contains(handle));
    }

    private static bool ExactPreviewLayer(Dictionary<string, object>? parameters)
    {
        if (parameters == null || !parameters.TryGetValue("name", out var value)) return false;
        return value is JsonElement element && element.ValueKind == JsonValueKind.String
            ? string.Equals(element.GetString(), PreviewLayer, StringComparison.Ordinal)
            : value is string text && string.Equals(text, PreviewLayer, StringComparison.Ordinal);
    }
}

public sealed record WriteBinding(
    string ApprovalId,
    string Operation,
    string DocumentGuid,
    string DrawingPath,
    string TargetLayer,
    string ManifestHash,
    string SourceFingerprint,
    string RecordId);

public sealed record TokenConsumeResult(bool Success, string? ErrorCode, WriteBinding? Binding);

public sealed class WriteTokenVault
{
    private sealed record Entry(string Token, WriteBinding Binding, DateTimeOffset ExpiresAt, bool Consumed);

    private readonly Dictionary<string, Entry> _byToken = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _byApproval = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly TimeProvider _clock;
    private readonly TimeSpan _lifetime;

    public WriteTokenVault(TimeProvider? clock = null, TimeSpan? lifetime = null)
    {
        _clock = clock ?? TimeProvider.System;
        _lifetime = lifetime ?? TimeSpan.FromMinutes(10);
        if (_lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));
    }

    internal string MintFromHumanApproval(WriteBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.ApprovalId)) throw new ArgumentException("approvalId is required");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return AddApprovedToken(binding, token, _clock.GetUtcNow() + _lifetime);
    }

    internal string AddApprovedToken(WriteBinding binding, string token, DateTimeOffset expiresAt)
    {
        lock (_lock)
        {
            if (_byApproval.TryGetValue(binding.ApprovalId, out var existing)) return existing;
            var entry = new Entry(token, binding, expiresAt, false);
            _byToken[token] = entry;
            _byApproval[binding.ApprovalId] = token;
            return token;
        }
    }

    public string? GetApprovedToken(string approvalId)
    {
        lock (_lock)
        {
            if (!_byApproval.TryGetValue(approvalId, out var token) || !_byToken.TryGetValue(token, out var entry))
                return null;
            if (entry.Consumed || _clock.GetUtcNow() > entry.ExpiresAt) return null;
            return token;
        }
    }

    public TokenConsumeResult Consume(string token, string requiredOperation)
    {
        lock (_lock)
        {
            if (!_byToken.TryGetValue(token, out var entry))
                return new(false, "token_invalid", null);
            if (entry.Consumed)
                return new(false, "token_consumed", null);

            _byToken[token] = entry with { Consumed = true };
            if (_clock.GetUtcNow() > entry.ExpiresAt)
                return new(false, "token_expired", null);
            if (!string.Equals(entry.Binding.Operation, requiredOperation, StringComparison.Ordinal))
                return new(false, "token_binding_mismatch", null);
            return new(true, null, entry.Binding);
        }
    }

    public static bool BindingMatches(WriteBinding approved, WriteBinding actual) =>
        string.Equals(approved.Operation, actual.Operation, StringComparison.Ordinal) &&
        string.Equals(approved.DocumentGuid, actual.DocumentGuid, StringComparison.Ordinal) &&
        string.Equals(approved.DrawingPath, actual.DrawingPath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(approved.TargetLayer, actual.TargetLayer, StringComparison.Ordinal) &&
        string.Equals(approved.ManifestHash, actual.ManifestHash, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(approved.SourceFingerprint, actual.SourceFingerprint, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(approved.RecordId, actual.RecordId, StringComparison.Ordinal);
}
