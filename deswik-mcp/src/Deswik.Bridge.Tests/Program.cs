using System.Text.Json;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using Deswik.Bridge.Models;
using Deswik.Bridge.Standalone;

var tests = new (string Name, Func<Task> Run)[]
{
    ("response envelope always includes mode", TestEnvelopeMode),
    ("disconnected live request fails without data", TestDisconnected),
    ("connected unregistered action is unsupported", TestUnsupported),
    ("demo requires an explicit request", TestExplicitDemo),
    ("addin refusal remains a live failure", TestLiveRefusal),
    ("invalid mode is rejected", TestInvalidMode),
    ("HTTP clients receive a clear protocol rejection", TestHttpRejection),
    ("Process Map action allowlist is exact", TestSidecarAllowlist),
    ("Process Map sidecar integrity pin is enforced", TestSidecarIntegrity),
    ("Process Map inspect returns file provenance", TestSidecarInspect),
    ("all legacy CAD writers are fenced", TestWriterFence),
    ("preview layer ownership detects foreign handles", TestPreviewLayerOwnership),
    ("write tokens are opaque bound and single use", TestWriteTokens),
    ("write tokens expire", TestWriteTokenExpiry),
    ("release bridge has one token mint call site", TestSingleMintCallSite),
};

try
{
    foreach (var test in tests)
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }

    Console.WriteLine("ALL TESTS PASSED");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAIL {ex.Message}");
    return 1;
}

static Task TestEnvelopeMode()
{
    var json = JsonSerializer.Serialize(McpResponse.Ok("one", new { value = 7 }));
    using var doc = JsonDocument.Parse(json);
    Equal(McpResponseMode.Live, doc.RootElement.GetProperty("mode").GetString());
    True(doc.RootElement.GetProperty("success").GetBoolean(), "success should remain independent of mode");
    return Task.CompletedTask;
}

static Task TestDisconnected()
{
    var response = BridgeResponsePolicy.NoProvider(
        new McpCommand { Id = "two", Action = "get_cad_selection" },
        hasConnectedAddin: false);

    Equal(McpResponseMode.Disconnected, response.Mode);
    False(response.Success, "disconnected response must fail");
    Equal("ADDIN_DISCONNECTED", response.ErrorCode);
    True(response.Data is null, "disconnected response must not contain data");

    var json = JsonSerializer.Serialize(response);
    False(json.Contains("coordinate", StringComparison.OrdinalIgnoreCase), "coordinates leaked");
    False(json.Contains("handle", StringComparison.OrdinalIgnoreCase), "handles leaked");
    False(json.Contains("entit", StringComparison.OrdinalIgnoreCase), "entity data leaked");
    return Task.CompletedTask;
}

static Task TestUnsupported()
{
    var response = BridgeResponsePolicy.NoProvider(
        new McpCommand { Id = "three", Action = "not_registered" },
        hasConnectedAddin: true);

    Equal(McpResponseMode.Unsupported, response.Mode);
    False(response.Success, "unsupported response must fail");
    Equal("UNSUPPORTED_ACTION", response.ErrorCode);
    True(response.Data is null, "unsupported response must not contain data");
    return Task.CompletedTask;
}

static async Task TestExplicitDemo()
{
    False(McpResponseMode.IsDemoRequest(null), "omitted mode selected demo");
    False(McpResponseMode.IsDemoRequest(McpResponseMode.Live), "live mode selected demo");
    True(McpResponseMode.IsDemoRequest(McpResponseMode.Demo), "explicit demo mode was ignored");

    var command = new McpCommand { Id = "four", Action = "ping", Mode = McpResponseMode.Demo };
    var handler = new DemoCommandHandler(new DemoSchedulerService());
    var response = BridgeResponsePolicy.AsDemo(await handler.HandleCommandAsync(command));
    Equal(McpResponseMode.Demo, response.Mode);
    True(response.Success, "explicit demo ping should succeed");

    var json = JsonSerializer.Serialize(response);
    using var doc = JsonDocument.Parse(json);
    Equal(McpResponseMode.Demo, doc.RootElement.GetProperty("mode").GetString());
    False(doc.RootElement.GetProperty("data").TryGetProperty("mode", out _), "mode was nested in demo data");
}

static Task TestLiveRefusal()
{
    var response = McpResponse.Fail("five", "operator refused", "OPERATOR_REFUSED");
    Equal(McpResponseMode.Live, response.Mode);
    False(response.Success, "live refusal must fail");
    Equal("OPERATOR_REFUSED", response.ErrorCode);
    return Task.CompletedTask;
}

static Task TestInvalidMode()
{
    var response = BridgeResponsePolicy.ValidateRequestMode(
        new McpCommand { Id = "six", Action = "ping", Mode = "automatic" });
    True(response is not null, "invalid request mode was accepted");
    Equal(McpResponseMode.Live, response!.Mode);
    False(response.Success, "invalid request mode must fail");
    Equal("INVALID_MODE", response.ErrorCode);
    return Task.CompletedTask;
}

static Task TestHttpRejection()
{
    True(BridgeResponsePolicy.IsHttpRequestLine("GET / HTTP/1.1"), "HTTP GET was not detected");
    False(BridgeResponsePolicy.IsHttpRequestLine("{\"id\":\"seven\"}"), "JSON was mistaken for HTTP");

    var response = BridgeResponsePolicy.HttpRejection();
    True(response.StartsWith("HTTP/1.1 400 Bad Request\r\n", StringComparison.Ordinal), "invalid HTTP status line");
    True(response.Contains("not a website", StringComparison.Ordinal), "protocol guidance missing");
    True(response.EndsWith("\r\n", StringComparison.Ordinal), "HTTP body must end with CRLF");
    return Task.CompletedTask;
}

static Task TestSidecarAllowlist()
{
    True(ProcessMapSidecar.CanHandle("map.inspect"), "approved action was refused");
    False(ProcessMapSidecar.CanHandle("map.inspect.extra"), "prefix action was accepted");
    False(ProcessMapSidecar.CanHandle("MAP.INSPECT"), "action matching was normalized");
    return Task.CompletedTask;
}

static Task TestSidecarIntegrity()
{
    True(ProcessMapSidecar.VerifyEntryPoint(
        ProcessMapSidecar.EntryPoint,
        ProcessMapSidecar.EntryPointSha256), "current sidecar failed its integrity pin");
    False(ProcessMapSidecar.VerifyEntryPoint(
        ProcessMapSidecar.EntryPoint,
        new string('0', 64)), "tampered hash was accepted");
    return Task.CompletedTask;
}

static async Task TestSidecarInspect()
{
    var sample = @"W:\deswik-mcp-plugin\tests\archive_ddf\SDK - Stope Design Layout.ddf";
    var command = new McpCommand
    {
        Id = "sidecar-inspect",
        Action = "map.inspect",
        Params = new Dictionary<string, object> { ["path"] = sample },
    };
    var response = await new ProcessMapSidecar().InvokeAsync(command);
    True(response.Success, response.Error ?? "sidecar inspect failed");
    Equal(McpResponseMode.Live, response.Mode);
    var json = JsonSerializer.Serialize(response.Data);
    using var doc = JsonDocument.Parse(json);
    var hashes = doc.RootElement.GetProperty("fileHashes");
    Equal(1, hashes.GetArrayLength());
    Equal("read", hashes[0].GetProperty("access").GetString());
    Equal(64, hashes[0].GetProperty("sha256").GetString()!.Length);
}

static Task TestWriterFence()
{
    foreach (var action in GuardedWritePolicy.LegacyWriterActions)
    {
        var parameters = action == "create_cad_layer"
            ? new Dictionary<string, object> { ["name"] = "PRODUCTION" }
            : new Dictionary<string, object>();
        var response = GuardedWritePolicy.RefuseUnfenced(
            new McpCommand { Id = action, Action = action, Params = parameters });
        True(response != null, $"{action} was reachable without a token");
        Equal("forbidden_unfenced", response!.ErrorCode);
    }

    var previewLayer = GuardedWritePolicy.RefuseUnfenced(new McpCommand
    {
        Id = "preview-layer",
        Action = "create_cad_layer",
        Params = new Dictionary<string, object> { ["name"] = GuardedWritePolicy.PreviewLayer }
    });
    True(previewLayer == null, "exact preview layer exception was refused");

    foreach (var internalAction in new[]
    {
        GuardedWritePolicy.InternalApprovalAction,
        GuardedWritePolicy.InternalCommitAction,
        GuardedWritePolicy.InternalRollbackAction,
    })
    {
        True(GuardedWritePolicy.RefuseUnfenced(new McpCommand
        {
            Id = internalAction, Action = internalAction
        }) != null, $"internal action {internalAction} was publicly routable");
    }
    return Task.CompletedTask;
}

static Task TestPreviewLayerOwnership()
{
    False(GuardedWritePolicy.PreviewLayerIsDirty(new ulong[] { 10, 11 }, new ulong[] { 10, 11 }),
        "owned preview handles were marked dirty");
    True(GuardedWritePolicy.PreviewLayerIsDirty(new ulong[] { 10, 11, 12 }, new ulong[] { 10, 11 }),
        "foreign preview handle was accepted");
    True(GuardedWritePolicy.PreviewLayerIsDirty(new ulong[] { 10 }, Array.Empty<ulong>()),
        "stale preview survived an empty process record");
    return Task.CompletedTask;
}

static Task TestWriteTokens()
{
    var binding = Binding("doc-a", "source-a");
    var vault = new WriteTokenVault();
    var token = TestMintStub(vault, binding, DateTimeOffset.UtcNow.AddMinutes(10));
    Equal(token, TestMintStub(vault, binding, DateTimeOffset.UtcNow.AddMinutes(10)));
    True(token.Length >= 22, "token carries less than 128 bits of encoded entropy");
    False(token.Contains(binding.ApprovalId, StringComparison.Ordinal), "token contains the request/approval id");

    var first = vault.Consume(token, "commit");
    True(first.Success, first.ErrorCode ?? "first token use failed");
    True(WriteTokenVault.BindingMatches(binding, first.Binding!), "approved binding changed");
    var replay = vault.Consume(token, "commit");
    False(replay.Success, "token replay succeeded");
    Equal("token_consumed", replay.ErrorCode);

    var otherDrawing = Binding("doc-b", "source-a");
    False(WriteTokenVault.BindingMatches(binding, otherDrawing), "token replayed across a second drawing");
    var staleSource = Binding("doc-a", "source-b");
    False(WriteTokenVault.BindingMatches(binding, staleSource), "stale source fingerprint was accepted");
    return Task.CompletedTask;
}

static Task TestWriteTokenExpiry()
{
    var clock = new TestClock(DateTimeOffset.Parse("2026-09-23T00:00:00Z"));
    var vault = new WriteTokenVault(clock, TimeSpan.FromMinutes(10));
    var token = TestMintStub(vault, Binding("doc-a", "source-a"), clock.GetUtcNow().AddMinutes(10));
    clock.Advance(TimeSpan.FromMinutes(11));
    var expired = vault.Consume(token, "commit");
    False(expired.Success, "expired token succeeded");
    Equal("token_expired", expired.ErrorCode);
    Equal("token_consumed", vault.Consume(token, "commit").ErrorCode);
    return Task.CompletedTask;
}

static Task TestSingleMintCallSite()
{
    var target = typeof(WriteTokenVault).GetMethod(nameof(WriteTokenVault.MintFromHumanApproval),
        BindingFlags.Instance | BindingFlags.NonPublic)!;
    var count = typeof(GuardedWritePolicy).Assembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Static | BindingFlags.Instance))
        .Sum(method => CountCalls(method, target));
    Equal(1, count);
    return Task.CompletedTask;
}

static WriteBinding Binding(string documentGuid, string sourceFingerprint) => new(
    "approval-1", "commit", documentGuid, @"C:\drawings\test.dwg",
    @"RINGDESIGN\_MCP_APPROVED\HOLES", new string('A', 64), sourceFingerprint, "record-1");

static string TestMintStub(WriteTokenVault vault, WriteBinding binding, DateTimeOffset expiresAt)
{
    var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    return vault.AddApprovedToken(binding, token, expiresAt);
}

static int CountCalls(MethodInfo caller, MethodInfo target)
{
    var bytes = caller.GetMethodBody()?.GetILAsByteArray();
    if (bytes == null) return 0;
    var singleByteOpCodes = BuildOpCodes(multiByte: false);
    var multiByteOpCodes = BuildOpCodes(multiByte: true);
    var count = 0;
    for (var index = 0; index < bytes.Length;)
    {
        OpCode code;
        var first = bytes[index++];
        if (first == 0xfe) code = multiByteOpCodes[bytes[index++]];
        else code = singleByteOpCodes[first];
        if (code is var candidate && (candidate == OpCodes.Call || candidate == OpCodes.Callvirt))
        {
            var token = BitConverter.ToInt32(bytes, index);
            try
            {
                var called = caller.Module.ResolveMethod(token);
                if (called?.MetadataToken == target.MetadataToken && called.Module == target.Module) count++;
            }
            catch { }
        }
        index += OperandSize(code.OperandType, bytes, index);
    }
    return count;
}

static int OperandSize(OperandType type, byte[] bytes, int index) => type switch
{
    OperandType.InlineNone => 0,
    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
    OperandType.InlineVar => 2,
    OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or
    OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
    OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
    OperandType.InlineI8 or OperandType.InlineR => 8,
    OperandType.InlineSwitch => 4 + BitConverter.ToInt32(bytes, index) * 4,
    _ => throw new InvalidOperationException($"Unknown IL operand type {type}")
};

static OpCode[] BuildOpCodes(bool multiByte)
{
    var result = new OpCode[256];
    foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        if (field.GetValue(null) is not OpCode code) continue;
        var value = unchecked((ushort)code.Value);
        if (multiByte == ((value & 0xff00) == 0xfe00)) result[value & 0xff] = code;
    }
    return result;
}

static void True(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void False(bool condition, string message) => True(!condition, message);

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"expected '{expected}', got '{actual}'");
}

sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now;
    public TestClock(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan duration) => _now += duration;
}
