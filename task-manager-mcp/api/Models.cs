using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api;

[JsonConverter(typeof(WorkStatusJsonConverter))]
public enum WorkStatus { Todo, InProgress, Completed }

public class WorkStatusJsonConverter : JsonConverter<WorkStatus>
{
    public override WorkStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? throw new JsonException("status cannot be null");
        return value switch
        {
            "todo"        => WorkStatus.Todo,
            "in-progress" => WorkStatus.InProgress,
            "completed"   => WorkStatus.Completed,
            _ => throw new JsonException($"Invalid status '{value}'. Must be one of: todo, in-progress, completed")
        };
    }

    public override void Write(Utf8JsonWriter writer, WorkStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            WorkStatus.Todo       => "todo",
            WorkStatus.InProgress => "in-progress",
            WorkStatus.Completed  => "completed",
            _ => value.ToString().ToLower()
        });
}

public class TaskItem
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public WorkStatus Status { get; set; } = WorkStatus.Todo;

    public int Priority { get; set; }

    public DateOnly? DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public record TaskCreateDto(
    string Title,
    string? Description,
    int Priority,
    string? DueDate,
    string Status = "todo"
);

public record TaskUpdateDto(
    string? Title,
    string? Description,
    int? Priority,
    string? DueDate,
    string? Status
);

public class TaskReadDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static TaskReadDto From(TaskItem t) => new()
    {
        Id          = t.Id,
        Title       = t.Title,
        Description = t.Description,
        Status      = t.Status switch
        {
            WorkStatus.Todo       => "todo",
            WorkStatus.InProgress => "in-progress",
            WorkStatus.Completed  => "completed",
            _                     => t.Status.ToString().ToLower()
        },
        Priority    = t.Priority,
        DueDate     = t.DueDate?.ToString("yyyy-MM-dd"),
        CreatedAt   = t.CreatedAt,
        UpdatedAt   = t.UpdatedAt,
    };
}
