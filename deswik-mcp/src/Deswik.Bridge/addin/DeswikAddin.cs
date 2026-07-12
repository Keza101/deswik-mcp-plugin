using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Deswik.Bridge.Communication;
using Deswik.Bridge.Models;
using Deswik.Bridge.Services;

namespace Deswik.Bridge.Addin;

/// <summary>
/// Deswik Addin that provides MCP bridge functionality.
/// This addin loads into the Deswik application and exposes APIs via Named Pipe.
/// </summary>
public class DeswikAddin
{
    private const string AddinName = "Deswik.MCP.Bridge";
    private const string AddinVersion = "1.0.0";
    private const string PipeName = "deswik-mcp-pipe";

    private NamedPipeServer? _pipeServer;
    private SchedulerService? _schedulerService;
    private CadService? _cadService;
    private LhsService? _lhsService;
    private CommandHandler? _commandHandler;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Called when the addin is loaded by Deswik.
    /// </summary>
    public void OnStartup()
    {
        try
        {
            Log($"[{AddinName}] Starting...");

            // Initialize services
            InitializeServices();

            // Start the Named Pipe server
            StartPipeServer();

            Log($"[{AddinName}] Started successfully. Version: {AddinVersion}");
        }
        catch (Exception ex)
        {
            Log($"[{AddinName}] Startup error: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when the addin is unloaded by Deswik.
    /// </summary>
    public void OnShutdown()
    {
        try
        {
            Log($"[{AddinName}] Shutting down...");

            _cts?.Cancel();
            _pipeServer?.Dispose();
            _pipeServer = null;

            Log($"[{AddinName}] Shutdown complete.");
        }
        catch (Exception ex)
        {
            Log($"[{AddinName}] Shutdown error: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when a schedule is opened in Deswik.
    /// </summary>
    public void OnScheduleOpened(object schedule)
    {
        try
        {
            Log($"[{AddinName}] Schedule opened, initializing SchedulerService...");

            _schedulerService?.Initialize(schedule);

            Log($"[{AddinName}] SchedulerService initialized with schedule.");
        }
        catch (Exception ex)
        {
            Log($"[{AddinName}] OnScheduleOpened error: {ex.Message}");
        }
    }

    private void InitializeServices()
    {
        _schedulerService = new SchedulerService();
        _cadService = new CadService();
        _lhsService = new LhsService();

        _cadService.Initialize();
        _lhsService.Initialize();

        _commandHandler = new CommandHandler(_schedulerService, _cadService, _lhsService);

        Log($"[{AddinName}] Services initialized.");
    }

    private void StartPipeServer()
    {
        _pipeServer = new NamedPipeServer();
        _pipeServer.OnCommandReceived += async command =>
        {
            if (_commandHandler != null)
            {
                return await _commandHandler.HandleCommandAsync(command);
            }
            return McpResponse.Fail(command.Id, "Command handler not initialized", "NOT_INITIALIZED");
        };

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => _pipeServer.StartAsync(_cts.Token));

        Log($"[{AddinName}] Named Pipe server started on '{PipeName}'.");
    }

    private static void Log(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Deswik",
                "Logs",
                $"{AddinName}.log");

            var logDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry);

            System.Diagnostics.Debug.WriteLine(logEntry);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}

/// <summary>
/// Entry point for standalone testing without Deswik.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Deswik MCP Bridge - Standalone Mode");
        Console.WriteLine("====================================");
        Console.WriteLine("This mode is for testing without the Deswik application.");
        Console.WriteLine("In production, this code runs as a Deswik Addin.");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to exit...");
        Console.WriteLine();

        var addin = new DeswikAddin();
        addin.OnStartup();

        // Keep running
        try
        {
            await Task.Delay(Timeout.Infinite);
        }
        catch (OperationCanceledException)
        {
            addin.OnShutdown();
        }
    }
}
