using System.Text.Json;
using Deswik.Scheduler.API;
using DeswikTaskQuery = Deswik.Scheduler.API.Contracts.ITaskQuery;
using DeswikResourceQuery = Deswik.Scheduler.API.Contracts.IResourceQuery;
using DeswikDepQuery = Deswik.Scheduler.API.Contracts.IDependencyQuery;

namespace Deswik.Bridge.Services;

/// <summary>
/// DTO for task information exposed to MCP.
/// </summary>
public class TaskInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime Finish { get; set; }
    public TimeSpan Duration { get; set; }
    public int UniqueId { get; set; }
    public int DistributionCount { get; set; }
    public Dictionary<string, object> CustomFields { get; set; } = new();
}

/// <summary>
/// DTO for resource information exposed to MCP.
/// </summary>
public class ResourceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPool { get; set; }
    public int MaxUnits { get; set; }
    public string? Rate { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO for dependency information exposed to MCP.
/// </summary>
public class DependencyInfo
{
    public string Id { get; set; } = string.Empty;
    public string FromTaskId { get; set; } = string.Empty;
    public string ToTaskId { get; set; } = string.Empty;
    public string LinkType { get; set; } = string.Empty;
    public string? Lag { get; set; }
    public double PercentageOverlap { get; set; }
    public int Layer { get; set; }
}

/// <summary>
/// DTO for schedule summary information.
/// </summary>
public class ScheduleInfo
{
    public string FileName { get; set; } = string.Empty;
    public string DirectoryName { get; set; } = string.Empty;
    public DateTime? TargetStart { get; set; }
    public int TotalTasks { get; set; }
    public int TotalResources { get; set; }
    public int TotalDependencies { get; set; }
    public string Version { get; set; } = "2024.2";
}

/// <summary>
/// DTO for block status query result.
/// </summary>
public class BlockStatusResult
{
    public string BlockId { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public double ScheduledVolume { get; set; }
    public DateTime? ScheduledPeriod { get; set; }
}

/// <summary>
/// Service wrapping Deswik Scheduler API for MCP access.
/// </summary>
public class SchedulerService
{
    private ScheduleQuery? _scheduleQuery;
    private bool _isInitialized;

    public SchedulerService()
    {
        // Will be initialized when Deswik application context is available
    }

    /// <summary>
    /// Initializes the service with the current scheduler instance.
    /// This should be called from the Deswik Addin when the application starts.
    /// </summary>
    public void Initialize(object schedulerInstance)
    {
        if (schedulerInstance is Deswik.Scheduler.Engine.Schedule schedule)
        {
            _scheduleQuery = new ScheduleQuery(schedule);
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Gets basic information about the current schedule.
    /// </summary>
    public ScheduleInfo GetScheduleInfo()
    {
        EnsureInitialized();

        return new ScheduleInfo
        {
            FileName = _scheduleQuery?.FileName ?? "No schedule loaded",
            DirectoryName = _scheduleQuery?.DirectoryName ?? "",
            TargetStart = _scheduleQuery?.TargetStart,
            TotalTasks = _scheduleQuery?.Tasks?.Count() ?? 0,
            TotalResources = _scheduleQuery?.Resources?.Length ?? 0,
            TotalDependencies = _scheduleQuery?.Dependencies?.Count() ?? 0
        };
    }

    /// <summary>
    /// Gets a list of tasks from the schedule.
    /// </summary>
    public IEnumerable<TaskInfo> GetTasks(int limit = 100, int offset = 0, string? filter = null)
    {
        EnsureInitialized();

        var tasks = _scheduleQuery?.Tasks ?? Enumerable.Empty<DeswikTaskQuery>();

        var filtered = string.IsNullOrEmpty(filter)
            ? tasks
            : tasks.Where(t => GetTaskName(t).Contains(filter, StringComparison.OrdinalIgnoreCase));

        return filtered
            .Skip(offset)
            .Take(limit)
            .Select(MapToTaskInfo);
    }

    /// <summary>
    /// Gets detailed information about a specific task.
    /// </summary>
    public TaskInfo? GetTaskDetails(string taskId)
    {
        EnsureInitialized();

        var task = _scheduleQuery?.Tasks?.FirstOrDefault(t => t.Id == taskId);
        return task != null ? MapToTaskInfo(task) : null;
    }

    /// <summary>
    /// Gets all resources from the schedule.
    /// </summary>
    public IEnumerable<ResourceInfo> GetResources()
    {
        EnsureInitialized();

        return _scheduleQuery?.Resources?.Select(r => new ResourceInfo
        {
            Id = r.Id,
            Name = r.Name,
            IsPool = r.IsPool,
            MaxUnits = r.MaxUnits,
            Rate = r.Rate,
            Description = r.Description
        }) ?? Enumerable.Empty<ResourceInfo>();
    }

    /// <summary>
    /// Gets dependencies, optionally filtered by task ID.
    /// </summary>
    public IEnumerable<DependencyInfo> GetDependencies(string? taskId = null)
    {
        EnsureInitialized();

        var deps = _scheduleQuery?.Dependencies ?? Enumerable.Empty<DeswikDepQuery>();

        if (!string.IsNullOrEmpty(taskId))
        {
            deps = deps.Where(d => d.From.Id == taskId || d.To.Id == taskId);
        }

        return deps.Select(MapToDependencyInfo);
    }

    /// <summary>
    /// Updates field values on a task.
    /// </summary>
    public bool UpdateTaskFields(string taskId, Dictionary<string, object> fields)
    {
        EnsureInitialized();

        try
        {
            // Convert object values to the format expected by the API
            var fieldDict = new Dictionary<string, object>();
            foreach (var kvp in fields)
            {
                if (kvp.Value is JsonElement je)
                {
                    fieldDict[kvp.Key] = je.ValueKind switch
                    {
                        JsonValueKind.String => je.GetString() ?? "",
                        JsonValueKind.Number => je.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => je.ToString()
                    };
                }
                else
                {
                    fieldDict[kvp.Key] = kvp.Value ?? "";
                }
            }

            return _scheduleQuery?.UpdateFieldValues(taskId, fieldDict) ?? false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SchedulerService] UpdateFieldValues error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Queries the status of a mining block.
    /// </summary>
    public BlockStatusResult? QueryBlockStatus(string blockId)
    {
        EnsureInitialized();

        try
        {
            // Create MaterialBlockId from blockId string
            var materialBlockId = new Deswik.Scheduler.API.Contracts.MaterialBlockId(blockId);

            // Query the schedule for block status
            // Note: The actual query would need the schedule context to return results
            return new BlockStatusResult
            {
                BlockId = blockId,
                Material = "",
                ScheduledVolume = 0,
                ScheduledPeriod = null
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SchedulerService] QueryBlockStatus error: {ex.Message}");
            return null;
        }
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "SchedulerService not initialized. Call Initialize() with a valid scheduler instance first.");
        }
    }

    private static string GetTaskName(DeswikTaskQuery task)
    {
        // Try to get name from custom field, fallback to ID
        try
        {
            var nameValue = task.GetCustomFieldValue("Name");
            return nameValue?.ToString() ?? task.Id;
        }
        catch
        {
            return task.Id;
        }
    }

    private static TaskInfo MapToTaskInfo(DeswikTaskQuery task)
    {
        var taskInfo = new TaskInfo
        {
            Id = task.Id,
            Name = GetTaskName(task),
            Start = task.Start,
            Finish = task.Finish,
            Duration = task.Duration,
            UniqueId = task.UniqueId,
            DistributionCount = task.DistributionCount
        };

        // Try to load custom fields
        try
        {
            // Get all custom field values
            var customFields = new Dictionary<string, object>();
            // Note: This is a simplified approach. In production, you would enumerate
            // all custom field definitions and retrieve their values.
            taskInfo.CustomFields = customFields;
        }
        catch
        {
            // Ignore errors loading custom fields
        }

        return taskInfo;
    }

    private static DependencyInfo MapToDependencyInfo(DeswikDepQuery dep)
    {
        return new DependencyInfo
        {
            Id = $"{dep.From?.Id}_TO_{dep.To?.Id}",
            FromTaskId = dep.From?.Id ?? "",
            ToTaskId = dep.To?.Id ?? "",
            LinkType = dep.LinkType.ToString(),
            Lag = dep.Lag,
            PercentageOverlap = dep.PercentageOverlap,
            Layer = dep.Layer
        };
    }
}
