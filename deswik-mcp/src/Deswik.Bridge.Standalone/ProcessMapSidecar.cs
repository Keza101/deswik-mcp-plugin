using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Deswik.Bridge.Models;

namespace Deswik.Bridge.Standalone;

public sealed class ProcessMapSidecar
{
    public const string PythonExe = @"C:\Python314\python.exe";
    public const string EntryPoint = @"W:\deswik-mcp-plugin\deswik_pm\sidecar.py";
    public const string EntryPointSha256 = "B334E3A6B71D265385807F2CBCB3003226BB732789AB811E125ABF8428CA9960";

    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
    {
        "map.inspect", "map.validate", "map.generate", "map.install", "map.inventory"
    };

    public static bool CanHandle(string action) => Actions.Contains(action);

    public static bool VerifyEntryPoint(string path, string expectedSha256)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        return string.Equals(
            Convert.ToHexString(SHA256.HashData(stream)),
            expectedSha256,
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<McpResponse> InvokeAsync(McpCommand command)
    {
        if (!CanHandle(command.Action))
            return McpResponse.Fail(command.Id, $"Unsupported sidecar action: {command.Action}", "UNSUPPORTED_ACTION");
        if (!Path.IsPathFullyQualified(PythonExe) || !File.Exists(PythonExe))
            return McpResponse.Fail(command.Id, $"Pinned Python interpreter not found: {PythonExe}", "SIDECAR_UNAVAILABLE");
        if (!VerifyEntryPoint(EntryPoint, EntryPointSha256))
            return McpResponse.Fail(command.Id, "Process Map sidecar integrity check failed", "SIDECAR_INTEGRITY");

        var start = new ProcessStartInfo
        {
            FileName = PythonExe,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(EntryPoint);

        using var process = Process.Start(start);
        if (process == null)
            return McpResponse.Fail(command.Id, "Failed to start Process Map sidecar", "SIDECAR_UNAVAILABLE");

        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            action = command.Action,
            @params = command.Params,
        }));
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            return McpResponse.Fail(command.Id, "Process Map sidecar timed out", "SIDECAR_TIMEOUT");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        try
        {
            using var result = JsonDocument.Parse(stdout);
            var root = result.RootElement;
            if (root.GetProperty("success").GetBoolean())
                return McpResponse.Ok(command.Id, root.GetProperty("data").Clone());

            var error = root.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString() ?? "Process Map sidecar failed"
                : "Process Map sidecar failed";
            var code = root.TryGetProperty("errorCode", out var codeElement)
                ? codeElement.GetString()
                : "MAP_SIDECAR_ERROR";
            return McpResponse.Fail(command.Id, error, code);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? ex.Message : stderr.Trim();
            return McpResponse.Fail(command.Id, $"Invalid sidecar response: {detail}", "SIDECAR_PROTOCOL");
        }
    }
}
