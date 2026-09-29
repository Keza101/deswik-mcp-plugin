using System.Text.Json;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Deswik.Bridge.Models;
using Deswik.Bridge.Standalone;
using Deswik.Mcp.Server;
using Deswik.Ug.Design.Profiles;
using Deswik.Ug.Design;
using Deswik.Addin;

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
    ("guarded writes reject source handle additions and removals", TestSourceHandleSet),
    ("Process Map actions use an exact allowlist", TestProcessMapActionPolicy),
    ("write tokens are opaque bound and single use", TestWriteTokens),
    ("write tokens expire", TestWriteTokenExpiry),
    ("release bridge has one token mint call site", TestSingleMintCallSite),
    ("job actions use an exact allowlist", TestJobActionAllowlist),
    ("cancel before commit dispatch writes nothing", TestJobCancelBeforeWrite),
    ("cancel during a 40-hole commit preserves its complete result", TestJobCancelDuringWrite),
    ("partial writes report every surviving handle", TestJobPartialWrite),
    ("expired jobs cancel and restarted registries know no prior IDs", TestJobExpiryAndRestart),
    ("expired in-flight write never reports cancelled", TestJobDeadlineDuringWrite),
    ("MCP tool catalogue is exact and typed", TestMcpToolCatalogue),
    ("MCP refuses unknown and tokenless write tools", TestMcpToolRefusals),
    ("MCP rejects malformed JSON-RPC without exiting", TestMcpMalformedRequest),
    ("MCP read tool round-trips the live bridge envelope", TestMcpReadRoundTrip),
    ("MCP stdio server accepts a BOM and completes initialize list call and refusals", TestMcpStdioRoundTrip),
    ("synthetic profiles round-trip and reject invalid or production claims", TestSyntheticProfiles),
    ("profile save-as creates a distinct JSON file without changing the starter", TestProfileSaveAs),
    ("selected design snapshot preserves handles and refuses inferred roles", TestSelectionContext),
    ("operator roles and polyface metrics stay explicit and read-only", TestOperatorRoleContext),
    ("polyface geometry pages are bounded and preserve raw face indexes", TestPolyfaceGeometryPaging),
    ("polyline geometry pages preserve closed state and boundaries", TestPolylineGeometryPaging),
};

try
{
    var filter = args.Length == 2 && args[0] == "--filter" ? args[1] : null;
    foreach (var test in tests.Where(test => filter == null ||
                 test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
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

static Task TestSyntheticProfiles()
{
    var starter = ProfileJson.Starter();
    Equal("synthetic-starter", starter.ProfileId);
    Equal(ProfileJson.SyntheticBasis, starter.Basis);
    starter.Name = "Edited synthetic ring";
    starter.Ring.ToeSpacingM = 2.4;
    var saved = ProfileJson.CopyAsNew(starter);
    Equal(1, saved.Revision);
    False(saved.ProfileId == starter.ProfileId, "saving a copy reused the starter ID");
    Equal(ProfileJson.SyntheticBasis, saved.Basis);
    var reloaded = ProfileJson.Parse(ProfileJson.Serialize(saved));
    Equal(saved.ProfileId, reloaded.ProfileId);
    Equal(2.4, reloaded.Ring.ToeSpacingM);

    var invalid = ProfileJson.Starter();
    invalid.Ring.MinHoleSeparationM = invalid.Ring.ToeSpacingM + 1;
    try { ProfileJson.Serialize(invalid); throw new Exception("invalid spacing was saved"); }
    catch (ArgumentException) { }

    var claimedApproved = ProfileJson.Serialize(saved).Replace(ProfileJson.SyntheticBasis, "approved");
    try { ProfileJson.Parse(claimedApproved); throw new Exception("unapproved profile claimed approval"); }
    catch (ArgumentException) { }
    return Task.CompletedTask;
}

static Task TestProfileSaveAs()
{
    var tempRoot = Path.GetFullPath(Path.GetTempPath());
    var directory = Path.Combine(tempRoot, "deswik-profile-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var starter = ProfileJson.Starter();
        starter.Name = "Edited / synthetic ring";
        starter.Ring.BurdenM = 2.1;
        var first = ProfileFiles.SaveNew(starter, directory);
        True(File.Exists(first.Path), "new profile file is missing");
        False(first.Profile.ProfileId == starter.ProfileId, "starter ID was reused");
        var loaded = ProfileFiles.Load(first.Path);
        Equal(2.1, loaded.Ring.BurdenM);
        Equal(ProfileJson.SyntheticBasis, loaded.Basis);
        var second = ProfileFiles.SaveNew(loaded, directory);
        False(first.Path == second.Path, "save-as overwrote an existing profile");
        Equal(2, Directory.EnumerateFiles(directory, "*.json").Count());
        loaded.Ring.MinHoleSeparationM = loaded.Ring.ToeSpacingM + 1;
        try { ProfileFiles.SaveNew(loaded, directory); throw new Exception("invalid profile created a file"); }
        catch (ArgumentException) { }
        Equal(2, Directory.EnumerateFiles(directory, "*.json").Count());
        Equal("synthetic-starter", ProfileJson.Starter().ProfileId);
    }
    finally
    {
        var full = Path.GetFullPath(directory);
        if (!full.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test cleanup escaped the temporary directory");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
    return Task.CompletedTask;
}

static Task TestSelectionContext()
{
    var snapshot = SelectionContext.FromSelection(@"C:\drawings\disposable.duf", false,
        new[]
        {
            new SelectedFigure(42, "g42", "Polyface", "STOPE", null),
            new SelectedFigure(7, "g7", "Polyline", "BROW", null),
        });
    Equal(1, snapshot.SchemaVersion);
    True(snapshot.SourceHandles.SequenceEqual(new ulong[] { 7, 42 }), "source handles were not sorted");
    False(snapshot.ReadyForDesign, "unclassified selection was marked design-ready");
    Equal("unknown", snapshot.CoordinateSystem);
    Equal("unknown", snapshot.Units);
    True(snapshot.Figures.All(figure => figure.Role == "unclassified"), "CAD layer was treated as a mining role");
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot));
    Equal("unclassified", json.RootElement.GetProperty("figures")[0].GetProperty("role").GetString());
    Equal(7UL, json.RootElement.GetProperty("sourceHandles")[0].GetUInt64());
    var empty = SelectionContext.FromSelection("", false, Array.Empty<SelectedFigure>());
    True(empty.Warnings.Count >= 2, "missing selection or drawing identity was not reported");
    try
    {
        SelectionContext.FromSelection("saved.duf", false, new[]
        {
            new SelectedFigure(7, "first", "Line", null, null),
            new SelectedFigure(7, "second", "Line", null, null),
        });
        throw new Exception("duplicate source handle was accepted");
    }
    catch (ArgumentException) { }
    return Task.CompletedTask;
}

static Task TestOperatorRoleContext()
{
    using var requestJson = JsonDocument.Parse(
        "{\"roles\":{\"0x670\":\"stope\",\"1658\":\"drive\"},\"includePolyfaceMetrics\":true}");
    var request = SelectionContextRequest.Parse(requestJson.RootElement);
    var context = SelectionContext.FromSelection(@"C:\drawings\disposable.duf", false,
        new[]
        {
            new SelectedFigure(1658, "g2", "Polyface", "0", null, null),
            new SelectedFigure(1648, "g1", "Polyface", "0", null,
                new PolyfaceMetrics(new Point3(1, 2, 3), 100, 24)),
        }, request);
    True(context.SourceHandles.SequenceEqual(new ulong[] { 1648, 1658 }), "role mapping changed source handle order");
    Equal("stope", context.Figures[0].Role);
    Equal("operator", context.Figures[0].RoleSource);
    Equal(24, context.Figures[0].PolyfaceMetrics!.VertexCount);
    Equal("drive", context.Figures[1].Role);
    True(context.Warnings.Any(warning => warning.Contains("1658", StringComparison.Ordinal)),
        "missing polyface metrics were not reported");
    False(context.ReadyForDesign, "operator labels made context design-ready");
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(context));
    Equal("operator", json.RootElement.GetProperty("figures")[0].GetProperty("roleSource").GetString());
    Equal(100.0, json.RootElement.GetProperty("figures")[0]
        .GetProperty("polyfaceMetrics").GetProperty("volume").GetDouble());

    foreach (var invalid in new[]
    {
        "{\"roles\":{\"0x670\":\"stope\",\"1648\":\"drive\"}}",
        "{\"roles\":{\"0x670\":\"invented\"}}",
        "{\"roles\":{\"670\":\"stope\"},\"includePolyfaceMetrics\":\"yes\"}",
    })
    {
        using var document = JsonDocument.Parse(invalid);
        try { SelectionContextRequest.Parse(document.RootElement); throw new Exception("invalid role request passed"); }
        catch (ArgumentException) { }
    }
    using var unknownJson = JsonDocument.Parse("{\"roles\":{\"999\":\"stope\"}}");
    var unknown = SelectionContextRequest.Parse(unknownJson.RootElement);
    try
    {
        SelectionContext.FromSelection("saved.duf", false,
            new[] { new SelectedFigure(1648, "g1", "Polyface", "0", null) }, unknown);
        throw new Exception("unselected role handle was accepted");
    }
    catch (ArgumentException) { }
    return Task.CompletedTask;
}

static Task TestPolyfaceGeometryPaging()
{
    using var json = JsonDocument.Parse("{\"handle\":\"0x670\",\"start\":500,\"limit\":500}");
    var request = GeometryPageRequest.Parse(json.RootElement);
    Equal(1648UL, request.Handle);
    Equal(500, request.CountFor(1200));
    Equal(100, request.CountFor(600));
    Equal(1000, request.NextStart(1200, 600));
    Equal(null, new GeometryPageRequest(1648, 1000, 500).NextStart(1200, 600));
    Equal(0, new GeometryPageRequest(1648, 1200, 500).CountFor(1200));
    Equal(0, new GeometryPageRequest(1648, 1500, 500).CountFor(1200));
    Equal(null, new GeometryPageRequest(1648, 1500, 500).NextStart(1200, 600));
    var page = new PolyfaceGeometryPage(1, 1648, 0, 1, 3, 1,
        new[] { new Point3(0, 0, 0) }, new[] { new FaceIndices(0, 1, 2, 0) },
        1, "Deswik.GetFaceIndexes (raw)", "unknown", "unknown", false);
    using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(page));
    Equal(2, serialized.RootElement.GetProperty("faces")[0].GetProperty("c").GetInt32());
    False(serialized.RootElement.GetProperty("readyForDesign").GetBoolean(),
        "raw topology was promoted to design-ready geometry");
    foreach (var invalid in new[]
    {
        "{\"handle\":0}", "{\"handle\":\"0x0\"}", "{\"handle\":7,\"limit\":501}",
        "{\"handle\":7,\"start\":-1}", "{\"handle\":7,\"limit\":\"5\"}",
        "{\"handle\":7,\"extra\":true}", "{}", "{\"handle\":null}",
    })
    {
        using var bad = JsonDocument.Parse(invalid);
        try { GeometryPageRequest.Parse(bad.RootElement); throw new Exception("invalid page request passed"); }
        catch (ArgumentException) { }
    }
    return Task.CompletedTask;
}

static Task TestPolylineGeometryPaging()
{
    var request = new GeometryPageRequest(42, 1, 2);
    Equal(2, request.CountFor(4));
    Equal(3, request.NextStart(4));
    Equal(null, new GeometryPageRequest(42, 3, 2).NextStart(4));
    Equal(0, new GeometryPageRequest(42, 5, 2).CountFor(4));
    var page = new PolylineGeometryPage(1, 42, 1, 2, 4,
        new[] { new Point3(1, 2, 3), new Point3(4, 5, 6) }, true, 3,
        "unknown", "unknown", false);
    using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(page));
    Equal(2, serialized.RootElement.GetProperty("vertices").GetArrayLength());
    True(serialized.RootElement.GetProperty("closed").GetBoolean(), "closed flag was lost");
    False(serialized.RootElement.GetProperty("readyForDesign").GetBoolean(),
        "raw polyline was promoted to design-ready geometry");
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
    var projectRoot = Directory.GetParent(Path.GetDirectoryName(ProcessMapSidecar.EntryPoint)!)!.FullName;
    var sample = Path.Combine(projectRoot, "tests", "archive_ddf", "SDK - Stope Design Layout.ddf");
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
        "_job_cancel",
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

static Task TestSourceHandleSet()
{
    var recorded = new ulong[] { 30, 10, 20 };
    True(GuardedWriteSourcePolicy.HandleSetMatches(recorded, new ulong[] { 20, 30, 10 }),
        "unchanged handles in a different enumeration order were rejected");
    False(GuardedWriteSourcePolicy.HandleSetMatches(recorded, new ulong[] { 10, 20, 30, 40 }),
        "a source handle added after preview was accepted");
    False(GuardedWriteSourcePolicy.HandleSetMatches(recorded, new ulong[] { 10, 30 }),
        "a source handle removed after preview was accepted");
    return Task.CompletedTask;
}

static Task TestProcessMapActionPolicy()
{
    True(ProcessMapActionPolicy.TryGetAction(
            ProcessMapActionPolicy.ReadDocumentCommand, out var action),
        "approved Process Map command was refused");
    Equal(ProcessMapActionPolicy.ReadDocumentAction, action);
    False(ProcessMapActionPolicy.TryGetAction("MCP_READ_DOCUMENT_EXTRA", out _),
        "prefix extension bypassed the Process Map allowlist");
    False(ProcessMapActionPolicy.TryGetAction("mcp_read_document", out _),
        "Process Map command matching was normalized");
    False(ProcessMapActionPolicy.TryGetAction("cmd.exe", out _),
        "executable text was accepted as a Process Map command");
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

static Task TestJobActionAllowlist()
{
    True(BridgeJobPolicy.CanSubmit(GuardedWritePolicy.CommitAction), "guarded commit was excluded");
    True(BridgeJobPolicy.CanSubmit("get_cad_elements"), "long read was excluded");
    True(BridgeJobPolicy.CanSubmit("get_cad_layer_attributes"), "attribute read was excluded");
    True(BridgeJobPolicy.CanSubmit("get_cad_polyface_geometry"), "geometry page read was excluded");
    True(BridgeJobPolicy.CanSubmit("get_cad_polyline_geometry"), "polyline page read was excluded");
    False(BridgeJobPolicy.CanSubmit("draw_cad_ugdrillholes"), "unfenced writer was accepted");
    False(BridgeJobPolicy.CanSubmit("register_addin"), "registration was accepted as a job");
    False(BridgeJobPolicy.CanSubmit("commit_ugdrillholes_extra"), "suffix bypassed allowlist");
    return Task.CompletedTask;
}

static async Task TestJobCancelBeforeWrite()
{
    var jobs = new BridgeJobs();
    var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var wrote = false;
    var job = jobs.Submit(GuardedWritePolicy.CommitAction, TimeSpan.FromMinutes(1),
        async (id, _) =>
        {
            running.SetResult();
            await release.Task;
            if (jobs.TryStartWrite(id)) wrote = true;
            return McpResponse.Ok(id);
        });
    await running.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Equal("cancelled", jobs.Cancel(job.JobId)!.State);
    release.SetResult();
    await Task.Delay(30);
    False(wrote, "cancelled job dispatched a production write");
    Equal("cancelled", jobs.Get(job.JobId)!.State);
    Equal("cancelled", jobs.Cancel(job.JobId)!.State);
}

static async Task TestJobCancelDuringWrite()
{
    var jobs = new BridgeJobs();
    string? cancellationSignal = null;
    var cancellationSignals = 0;
    jobs.WriteCancellationRequested += id =>
    {
        cancellationSignal = id;
        cancellationSignals++;
    };
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handles = Enumerable.Range(100, 40).Select(i => (ulong)i).ToArray();
    var job = jobs.Submit(GuardedWritePolicy.CommitAction, TimeSpan.FromMinutes(1),
        async (id, _) =>
        {
            True(jobs.TryStartWrite(id), "write could not start");
            for (var i = 0; i < 20; i++) jobs.ReportProgress(id, i + 1, 40, handles[i]);
            started.SetResult();
            await release.Task;
            for (var i = 20; i < 40; i++) jobs.ReportProgress(id, i + 1, 40, handles[i]);
            return McpResponse.Ok(id, new { handles });
        });
    await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Equal(20, jobs.Get(job.JobId)!.CompletedUnits);
    Equal(20, jobs.Get(job.JobId)!.Handles.Count);
    var cancelling = jobs.Cancel(job.JobId)!;
    Equal("running", cancelling.State);
    True(cancelling.CancelRequested, "cancel request was lost");
    Equal(job.JobId, cancellationSignal);
    release.SetResult();
    await WaitForJob(jobs, job.JobId, "completed");
    var finished = jobs.Get(job.JobId)!;
    Equal(40, finished.Handles.Count);
    True(finished.Handles.SequenceEqual(handles), "job record omitted a created handle");
    Equal("completed", jobs.Cancel(job.JobId)!.State);
    Equal(1, cancellationSignals);
}

static async Task TestJobPartialWrite()
{
    var jobs = new BridgeJobs();
    var job = jobs.Submit(GuardedWritePolicy.CommitAction, TimeSpan.FromMinutes(1),
        (id, _) =>
        {
            True(jobs.TryStartWrite(id), "write could not start");
            var failure = McpResponse.Fail(id, "two surviving holes", "partial_write");
            failure.Data = new { handles = new ulong[] { 11, 12 } };
            return Task.FromResult(failure);
        });
    await WaitForJob(jobs, job.JobId, "partial");
    var result = jobs.Get(job.JobId)!;
    Equal("partial_write", result.ErrorCode);
    True(result.Handles.SequenceEqual(new ulong[] { 11, 12 }), "partial handles were not retained");
    False(result.State == "cancelled", "partial write was reported cancelled");
}

static async Task TestJobExpiryAndRestart()
{
    var jobs = new BridgeJobs();
    var job = jobs.Submit("get_cad_elements", TimeSpan.FromSeconds(1),
        async (id, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return McpResponse.Ok(id);
        });
    await WaitForJob(jobs, job.JobId, "cancelled", TimeSpan.FromSeconds(4));
    Equal("job_expired", jobs.Get(job.JobId)!.ErrorCode);
    True(new BridgeJobs().Get(job.JobId) == null, "a restarted bridge retained old job state");
}

static async Task TestJobDeadlineDuringWrite()
{
    var jobs = new BridgeJobs();
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var job = jobs.Submit(GuardedWritePolicy.CommitAction, TimeSpan.FromSeconds(1),
        async (id, _) =>
        {
            True(jobs.TryStartWrite(id), "write could not start");
            jobs.ReportProgress(id, 1, 40, 77);
            started.SetResult();
            await release.Task;
            return McpResponse.Ok(id, new { handles = new ulong[] { 77 } });
        });
    await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    await WaitForJob(jobs, job.JobId, "partial", TimeSpan.FromSeconds(4));
    var expired = jobs.Get(job.JobId)!;
    False(expired.State == "cancelled", "in-flight write was reported cancelled");
    Equal("job_deadline_write_unknown", expired.ErrorCode);
    release.SetResult();
    await WaitForJob(jobs, job.JobId, "completed");
    True(jobs.Get(job.JobId)!.Handles.SequenceEqual(new ulong[] { 77 }),
        "late commit result did not resolve the partial state");
}

static Task TestMcpToolCatalogue()
{
    var names = McpToolCatalog.Tools.Select(tool => tool.Name).ToArray();
    Equal(23, names.Length);
    Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    True(names.Contains("get_cad_document"), "CAD reads are missing");
    True(names.Contains("get_ug_selection_context"), "selected design snapshot is missing");
    True(names.Contains("get_cad_polyface_geometry"), "paged geometry read is missing");
    True(names.Contains("get_cad_polyline_geometry"), "paged polyline read is missing");
    True(names.Contains("commit_ugdrillholes"), "guarded commit is missing");
    True(names.Contains("job.submit"), "async jobs are missing");
    True(names.Contains("map.inspect"), "read-only Process Map inspection is missing");
    False(names.Contains("map.generate"), "file-producing map.generate was exposed");
    False(names.Contains("map.install"), "production map.install was exposed");
    False(names.Contains("draw_cad_ugdrillholes"), "legacy writer was exposed");
    False(names.Any(name => name.StartsWith("_", StringComparison.Ordinal)), "internal action was exposed");
    foreach (var tool in McpToolCatalog.Tools)
    {
        var json = JsonSerializer.SerializeToElement(tool.InputSchema);
        Equal("object", json.GetProperty("type").GetString());
    }
    return Task.CompletedTask;
}

static async Task TestMcpToolRefusals()
{
    var bridge = new RecordingBridgeClient();
    var adapter = new McpAdapter(bridge);
    using var unknown = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"draw_cad_ugdrillholes\",\"arguments\":{}}}"))!);
    Equal(-32602, unknown.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    Equal(0, bridge.Calls.Count);

    using var tokenless = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"commit_ugdrillholes\",\"arguments\":{}}}"))!);
    True(tokenless.RootElement.GetProperty("result").GetProperty("isError").GetBoolean(),
        "tokenless write did not return a tool error");
    Equal("human_token_required", tokenless.RootElement.GetProperty("result")
        .GetProperty("structuredContent").GetProperty("errorCode").GetString());
    Equal(0, bridge.Calls.Count);

    using var tokenlessJob = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"job.submit\",\"arguments\":{\"action\":\"commit_ugdrillholes\",\"args\":{}}}}"))!);
    True(tokenlessJob.RootElement.GetProperty("result").GetProperty("isError").GetBoolean(),
        "tokenless async write did not return a tool error");
    Equal(0, bridge.Calls.Count);
}

static async Task TestMcpMalformedRequest()
{
    var adapter = new McpAdapter(new RecordingBridgeClient());
    using var array = JsonDocument.Parse((await adapter.ProcessLineAsync("[]"))!);
    Equal(-32600, array.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    using var wrongVersion = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"1.0\",\"id\":9,\"method\":\"tools/list\"}"))!);
    Equal(-32600, wrongVersion.RootElement.GetProperty("error").GetProperty("code").GetInt32());
}

static async Task TestMcpReadRoundTrip()
{
    var bridge = new RecordingBridgeClient();
    var adapter = new McpAdapter(bridge);
    using var initialized = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}"))!);
    Equal(McpAdapter.ProtocolVersion, initialized.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());

    using var listed = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}"))!);
    Equal(23, listed.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength());

    using var called = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"get_cad_document\",\"arguments\":{}}}"))!);
    False(called.RootElement.GetProperty("result").GetProperty("isError").GetBoolean(), "read tool failed");
    Equal("live", called.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("mode").GetString());
    Equal("get_cad_document", bridge.Calls.Single());
    using var contextCall = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"get_ug_selection_context\",\"arguments\":{\"roles\":{\"0x670\":\"stope\"},\"includePolyfaceMetrics\":true}}}"))!);
    False(contextCall.RootElement.GetProperty("result").GetProperty("isError").GetBoolean(),
        "selected context tool failed");
    Equal("get_ug_selection_context", bridge.Calls.Last());
    Equal("stope", bridge.Arguments.Last().GetProperty("roles").GetProperty("0x670").GetString());
    True(bridge.Arguments.Last().GetProperty("includePolyfaceMetrics").GetBoolean(),
        "MCP dropped the opt-in geometry flag");
    using var geometryCall = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"get_cad_polyface_geometry\",\"arguments\":{\"handle\":\"0x670\",\"start\":0,\"limit\":25}}}"))!);
    False(geometryCall.RootElement.GetProperty("result").GetProperty("isError").GetBoolean(),
        "geometry page tool failed");
    Equal("get_cad_polyface_geometry", bridge.Calls.Last());
    Equal(25, bridge.Arguments.Last().GetProperty("limit").GetInt32());
    using var polylineCall = JsonDocument.Parse((await adapter.ProcessLineAsync(
        "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"tools/call\",\"params\":{\"name\":\"get_cad_polyline_geometry\",\"arguments\":{\"handle\":42,\"start\":1,\"limit\":2}}}"))!);
    False(polylineCall.RootElement.GetProperty("result").GetProperty("isError").GetBoolean(),
        "polyline page tool failed");
    Equal("get_cad_polyline_geometry", bridge.Calls.Last());
    Equal(1, bridge.Arguments.Last().GetProperty("start").GetInt32());
}

static async Task TestMcpStdioRoundTrip()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var bridgeTask = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false), false, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        var line = await reader.ReadLineAsync();
        True(line != null, "MCP adapter sent no bridge request");
        using var request = JsonDocument.Parse(line!);
        Equal("get_cad_document", request.RootElement.GetProperty("action").GetString());
        Equal("live", request.RootElement.GetProperty("mode").GetString());
        var id = request.RootElement.GetProperty("id").GetString();
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            id, mode = "live", success = true,
            data = new { DocumentName = "Disposable Test Drawing" },
        }));
    });

    var serverPath = Path.Combine(AppContext.BaseDirectory, "Deswik.Mcp.Server.exe");
    True(File.Exists(serverPath), $"MCP server executable missing: {serverPath}");
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo(serverPath)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(true),
        },
    };
    process.StartInfo.Environment["DESWIK_BRIDGE_PORT"] = port.ToString();
    True(process.Start(), "MCP server did not start");
    var transcript = new List<(string Request, string Response)>();

    async Task<JsonDocument> Exchange(string request)
    {
        await process.StandardInput.WriteLineAsync(request);
        await process.StandardInput.FlushAsync();
        string? response;
        try
        {
            response = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        True(response != null, "MCP server returned no stdio response");
        transcript.Add((request, response!));
        return JsonDocument.Parse(response!);
    }

    using var initialized = await Exchange(
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"stdio-test\",\"version\":\"1\"}}}");
    Equal(McpAdapter.ProtocolVersion, initialized.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
    using var listed = await Exchange(
        "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}");
    var tools = listed.RootElement.GetProperty("result").GetProperty("tools");
    Equal(23, tools.GetArrayLength());
    True(tools[0].TryGetProperty("name", out _), "tools/list did not use MCP field casing");
    True(tools[0].TryGetProperty("inputSchema", out _), "tools/list omitted inputSchema");

    using var unknown = await Exchange(
        "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"draw_cad_ugdrillholes\",\"arguments\":{}}}");
    Equal(-32602, unknown.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    using var tokenless = await Exchange(
        "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"commit_ugdrillholes\",\"arguments\":{}}}");
    True(tokenless.RootElement.GetProperty("result").GetProperty("isError").GetBoolean(), "stdio tokenless write was accepted");
    using var called = await Exchange(
        "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"get_cad_document\",\"arguments\":{}}}");
    Equal("Disposable Test Drawing", called.RootElement.GetProperty("result").GetProperty("structuredContent")
        .GetProperty("data").GetProperty("DocumentName").GetString());

    await bridgeTask.WaitAsync(TimeSpan.FromSeconds(5));
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    listener.Stop();
    Equal(0, process.ExitCode);
    if (Environment.GetEnvironmentVariable("DESWIK_MCP_TRACE") == "1")
    {
        foreach (var frame in transcript)
        {
            Console.WriteLine($"MCP> {frame.Request}");
            Console.WriteLine($"MCP< {frame.Response}");
        }
    }
}

static async Task WaitForJob(BridgeJobs jobs, string id, string state,
    TimeSpan? timeout = null)
{
    var until = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
    while (DateTimeOffset.UtcNow < until)
    {
        if (jobs.Get(id)?.State == state) return;
        await Task.Delay(10);
    }
    throw new InvalidOperationException($"job {id} did not reach {state}; got {jobs.Get(id)?.State}");
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

sealed class RecordingBridgeClient : IBridgeClient
{
    public List<string> Calls { get; } = new();
    public List<JsonElement> Arguments { get; } = new();

    public Task<JsonElement> InvokeAsync(
        string action, JsonElement arguments, CancellationToken cancellationToken)
    {
        Calls.Add(action);
        Arguments.Add(arguments.Clone());
        return Task.FromResult(JsonSerializer.SerializeToElement(new
        {
            id = "bridge-test", mode = "live", success = true,
            data = new { DocumentName = "Disposable Test Drawing" },
        }));
    }
}
