using System.Text.Json;
using Deswik.Bridge.Models;
using Deswik.Bridge.Services;

namespace Deswik.Bridge.Communication;

/// <summary>
/// Routes MCP commands to appropriate service handlers.
/// </summary>
public class CommandHandler
{
    private readonly SchedulerService _schedulerService;
    private readonly CadService _cadService;
    private readonly LhsService _lhsService;

    public CommandHandler(SchedulerService schedulerService, CadService cadService, LhsService lhsService)
    {
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        _cadService = cadService ?? throw new ArgumentNullException(nameof(cadService));
        _lhsService = lhsService ?? throw new ArgumentNullException(nameof(lhsService));
    }

    /// <summary>
    /// Handles an MCP command and returns the response.
    /// </summary>
    public async Task<McpResponse> HandleCommandAsync(McpCommand command)
    {
        try
        {
            return command.Action.ToLowerInvariant() switch
            {
                // Scheduler commands
                "get_schedule_info" => await HandleGetScheduleInfoAsync(command),
                "get_tasks" => await HandleGetTasksAsync(command),
                "get_task_details" => await HandleGetTaskDetailsAsync(command),
                "get_resources" => await HandleGetResourcesAsync(command),
                "get_dependencies" => await HandleGetDependenciesAsync(command),
                "update_task_fields" => await HandleUpdateTaskFieldsAsync(command),
                "query_block_status" => await HandleQueryBlockStatusAsync(command),

                // CAD commands
                "get_mining_blocks" => await HandleGetMiningBlocksAsync(command),
                "get_routes" => await HandleGetRoutesAsync(command),

                // LHS commands
                "get_dump_destinations" => await HandleGetDumpDestinationsAsync(command),
                "calculate_route" => await HandleCalculateRouteAsync(command),

                // System commands
                "ping" => McpResponse.Ok(command.Id, new { message = "pong", timestamp = DateTime.UtcNow }),
                "get_available_actions" => McpResponse.Ok(command.Id, GetAvailableActions()),

                _ => McpResponse.Fail(command.Id, $"Unknown action: {command.Action}", "UNKNOWN_ACTION")
            };
        }
        catch (Exception ex)
        {
            return McpResponse.Fail(command.Id, ex.Message, "INTERNAL_ERROR");
        }
    }

    private async Task<McpResponse> HandleGetScheduleInfoAsync(McpCommand command)
    {
        var info = _schedulerService.GetScheduleInfo();
        return McpResponse.Ok(command.Id, info);
    }

    private async Task<McpResponse> HandleGetTasksAsync(McpCommand command)
    {
        var limit = GetIntParam(command, "limit", 100);
        var offset = GetIntParam(command, "offset", 0);
        var filter = GetStringParam(command, "filter");

        var tasks = _schedulerService.GetTasks(limit, offset, filter);
        return McpResponse.Ok(command.Id, tasks);
    }

    private async Task<McpResponse> HandleGetTaskDetailsAsync(McpCommand command)
    {
        var taskId = GetRequiredStringParam(command, "taskId");
        var details = _schedulerService.GetTaskDetails(taskId);

        if (details == null)
            return McpResponse.Fail(command.Id, $"Task not found: {taskId}", "NOT_FOUND");

        return McpResponse.Ok(command.Id, details);
    }

    private async Task<McpResponse> HandleGetResourcesAsync(McpCommand command)
    {
        var resources = _schedulerService.GetResources();
        return McpResponse.Ok(command.Id, resources);
    }

    private async Task<McpResponse> HandleGetDependenciesAsync(McpCommand command)
    {
        var taskId = GetStringParam(command, "taskId");
        var dependencies = _schedulerService.GetDependencies(taskId);
        return McpResponse.Ok(command.Id, dependencies);
    }

    private async Task<McpResponse> HandleUpdateTaskFieldsAsync(McpCommand command)
    {
        var taskId = GetRequiredStringParam(command, "taskId");
        var fieldsJson = GetRequiredStringParam(command, "fields");
        var fields = JsonSerializer.Deserialize<Dictionary<string, object>>(fieldsJson)
            ?? new Dictionary<string, object>();

        var success = _schedulerService.UpdateTaskFields(taskId, fields);
        return McpResponse.Ok(command.Id, new { success });
    }

    private async Task<McpResponse> HandleQueryBlockStatusAsync(McpCommand command)
    {
        var blockId = GetRequiredStringParam(command, "blockId");
        var status = _schedulerService.QueryBlockStatus(blockId);
        return McpResponse.Ok(command.Id, status);
    }

    private async Task<McpResponse> HandleGetMiningBlocksAsync(McpCommand command)
    {
        var blocks = _cadService.GetMiningBlocks();
        return McpResponse.Ok(command.Id, blocks);
    }

    private async Task<McpResponse> HandleGetRoutesAsync(McpCommand command)
    {
        var routes = _cadService.GetRoutes();
        return McpResponse.Ok(command.Id, routes);
    }

    private async Task<McpResponse> HandleGetDumpDestinationsAsync(McpCommand command)
    {
        var destinations = _lhsService.GetDumpDestinations();
        return McpResponse.Ok(command.Id, destinations);
    }

    private async Task<McpResponse> HandleCalculateRouteAsync(McpCommand command)
    {
        var from = GetRequiredStringParam(command, "from");
        var to = GetRequiredStringParam(command, "to");
        var route = _lhsService.CalculateRoute(from, to);

        if (route == null)
            return McpResponse.Fail(command.Id, "Could not calculate route", "ROUTE_NOT_FOUND");

        return McpResponse.Ok(command.Id, route);
    }

    private static string? GetStringParam(McpCommand command, string name)
    {
        if (command.Params?.TryGetValue(name, out var value) == true)
            return value?.ToString();
        return null;
    }

    private static int GetIntParam(McpCommand command, string name, int defaultValue)
    {
        if (command.Params?.TryGetValue(name, out var value) == true)
        {
            if (value is JsonElement je)
                return je.GetInt32();
            if (int.TryParse(value?.ToString(), out var result))
                return result;
        }
        return defaultValue;
    }

    private static string GetRequiredStringParam(McpCommand command, string name)
    {
        var value = GetStringParam(command, name);
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"Missing required parameter: {name}");
        return value;
    }

    private static List<string> GetAvailableActions()
    {
        return new List<string>
        {
            // Scheduler
            "get_schedule_info",
            "get_tasks",
            "get_task_details",
            "get_resources",
            "get_dependencies",
            "update_task_fields",
            "query_block_status",
            // CAD
            "get_mining_blocks",
            "get_routes",
            // LHS
            "get_dump_destinations",
            "calculate_route",
            // System
            "ping",
            "get_available_actions"
        };
    }
}
