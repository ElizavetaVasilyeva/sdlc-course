using System.Text.Json;
using System.Text.Json.Serialization;
using Api;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

// Only load .env when no environment variables are already set (env vars beat .env file)
if (Environment.GetEnvironmentVariable("TASK_API_KEY") is null)
    Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ───────────────────────────────────────────────────────────
var apiKey = Environment.GetEnvironmentVariable("TASK_API_KEY")
    ?? throw new InvalidOperationException("TASK_API_KEY environment variable is required");

var dbUrl = Environment.GetEnvironmentVariable("TASK_DB_URL") ?? "sqlite:///./task_manager.db";
if (dbUrl.StartsWith("sqlite:///"))
    dbUrl = "Data Source=" + dbUrl["sqlite:///".Length..];

// ── Services ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(dbUrl));
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title   = "Task Manager API";
        doc.Info.Version = "v1";
        doc.Info.Description = "Simple task manager REST API with API key authentication.";
        return Task.CompletedTask;
    });
});

builder.Services.ConfigureHttpJsonOptions(opt =>
{
    opt.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// ── OpenAPI / Scalar UI ──────────────────────────────────────────────────────
app.MapOpenApi();
app.MapScalarApiReference(opt => opt.WithTitle("Task Manager API"));

// ── DB init ─────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

// ── Auth helper ─────────────────────────────────────────────────────────────
bool HasValidKey(HttpContext ctx)
{
    if (!ctx.Request.Headers.TryGetValue("X-API-Key", out var key) || key.Count == 0)
        return false;
    var a = key[0] ?? string.Empty;
    var b = apiKey;
    if (a.Length != b.Length) return false;
    var diff = 0;
    for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
    return diff == 0;
}

// ── Validation helpers ──────────────────────────────────────────────────────
static WorkStatus? ParseStatus(string s) => s switch
{
    "todo"        => WorkStatus.Todo,
    "in-progress" => WorkStatus.InProgress,
    "completed"   => WorkStatus.Completed,
    _             => null
};

static IResult? ValidateCreate(TaskCreateDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.Title))
        return Results.UnprocessableEntity(new { detail = "title must not be blank" });
    if (dto.Title.Trim().Length > 120)
        return Results.UnprocessableEntity(new { detail = "title must not exceed 120 characters" });
    if (dto.Description?.Length > 2000)
        return Results.UnprocessableEntity(new { detail = "description must not exceed 2000 characters" });
    if (dto.Priority < 1 || dto.Priority > 5)
        return Results.UnprocessableEntity(new { detail = "priority must be between 1 and 5" });
    if (dto.Status is not null && ParseStatus(dto.Status) is null)
        return Results.BadRequest(new { detail = $"Invalid status '{dto.Status}'. Must be one of: todo, in-progress, completed" });
    if (dto.DueDate is not null && !DateOnly.TryParse(dto.DueDate, out _))
        return Results.BadRequest(new { detail = "due_date must be in YYYY-MM-DD format" });
    return null;
}

// ── Endpoints ────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/tasks", async (string? status, string? due_date, HttpContext ctx, AppDbContext db) =>
{
    if (!HasValidKey(ctx)) return Results.Unauthorized();

    if (status is not null && ParseStatus(status) is null)
        return Results.BadRequest(new { detail = $"Invalid status '{status}'" });

    if (due_date is not null && !DateOnly.TryParse(due_date, out _))
        return Results.BadRequest(new { detail = "due_date must be in YYYY-MM-DD format" });

    var query = db.Tasks.AsQueryable();
    if (status is not null) { var s = ParseStatus(status)!.Value; query = query.Where(t => t.Status == s); }
    if (due_date is not null) { var d = DateOnly.Parse(due_date); query = query.Where(t => t.DueDate == d); }

    var tasks = await query
        .OrderBy(t => t.DueDate == null ? 1 : 0)
        .ThenBy(t => t.DueDate)
        .ThenByDescending(t => t.Priority)
        .ThenBy(t => t.Id)
        .ToListAsync();

    return Results.Ok(tasks.Select(TaskReadDto.From));
});

app.MapGet("/tasks/{id:int}", async (int id, HttpContext ctx, AppDbContext db) =>
{
    if (!HasValidKey(ctx)) return Results.Unauthorized();
    var task = await db.Tasks.FindAsync(id);
    return task is null ? Results.NotFound(new { detail = "Task not found" }) : Results.Ok(TaskReadDto.From(task));
});

app.MapPost("/tasks", async (TaskCreateDto dto, HttpContext ctx, AppDbContext db) =>
{
    if (!HasValidKey(ctx)) return Results.Unauthorized();
    if (ValidateCreate(dto) is { } err) return err;

    var task = new TaskItem
    {
        Title       = dto.Title.Trim(),
        Description = dto.Description,
        Status      = ParseStatus(dto.Status ?? "todo")!.Value,
        Priority    = dto.Priority,
        DueDate     = dto.DueDate is not null ? DateOnly.Parse(dto.DueDate) : null,
    };
    db.Tasks.Add(task);
    await db.SaveChangesAsync();
    return Results.Created($"/tasks/{task.Id}", TaskReadDto.From(task));
});

app.MapPatch("/tasks/{id:int}", async (int id, TaskUpdateDto dto, HttpContext ctx, AppDbContext db) =>
{
    if (!HasValidKey(ctx)) return Results.Unauthorized();

    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound(new { detail = "Task not found" });

    if (dto.Title is not null)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Results.UnprocessableEntity(new { detail = "title must not be blank" });
        if (dto.Title.Trim().Length > 120)
            return Results.UnprocessableEntity(new { detail = "title must not exceed 120 characters" });
        task.Title = dto.Title.Trim();
    }
    if (dto.Description is not null) task.Description = dto.Description;
    if (dto.Priority is not null)
    {
        if (dto.Priority < 1 || dto.Priority > 5)
            return Results.UnprocessableEntity(new { detail = "priority must be between 1 and 5" });
        task.Priority = dto.Priority.Value;
    }
    if (dto.Status is not null)
    {
        var parsed = ParseStatus(dto.Status);
        if (parsed is null) return Results.BadRequest(new { detail = $"Invalid status '{dto.Status}'" });
        task.Status = parsed.Value;
    }
    if (dto.DueDate is not null)
    {
        if (!DateOnly.TryParse(dto.DueDate, out var d))
            return Results.BadRequest(new { detail = "due_date must be in YYYY-MM-DD format" });
        task.DueDate = d;
    }

    task.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(TaskReadDto.From(task));
});

app.MapDelete("/tasks/{id:int}", async (int id, HttpContext ctx, AppDbContext db) =>
{
    if (!HasValidKey(ctx)) return Results.Unauthorized();
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound(new { detail = "Task not found" });
    db.Tasks.Remove(task);
    await db.SaveChangesAsync();
    return Results.Ok(new { deleted = true, id });
});

app.Run("http://127.0.0.1:8000");

// Required for WebApplicationFactory in tests
public partial class Program { }
