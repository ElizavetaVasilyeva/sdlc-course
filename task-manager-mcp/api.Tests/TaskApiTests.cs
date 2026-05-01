using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api.Tests;

public class TaskApiFactory : WebApplicationFactory<Program>
{
    // Keep connection open so the in-memory SQLite database persists for all tests
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public const string TestApiKey = "test-api-key";

    public TaskApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("TASK_API_KEY", TestApiKey);

        // ConfigureTestServices runs after app services — cleanly overrides the DbContext
        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection));
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", TestApiKey);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public class TaskApiTests : IClassFixture<TaskApiFactory>
{
    private readonly HttpClient _auth;
    private readonly HttpClient _anon;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TaskApiTests(TaskApiFactory factory)
    {
        _auth = factory.CreateAuthenticatedClient();
        _anon = factory.CreateClient();
    }

    // ── Health ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Returns200_WithoutApiKey()
    {
        var r = await _anon.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    // ── Authentication ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetTasks_Returns401_WhenKeyMissing()
    {
        var r = await _anon.GetAsync("/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task GetTasks_Returns401_WhenKeyInvalid()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/tasks");
        req.Headers.Add("X-API-Key", "wrong-key");
        var r = await _anon.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTask_Returns201_WithValidPayload()
    {
        var r = await _auth.PostAsJsonAsync("/tasks",
            new { title = "My Task", priority = 3, status = "todo" }, Json);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var t = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(t.GetProperty("id").GetInt32() > 0);
        Assert.Equal("My Task", t.GetProperty("title").GetString());
        Assert.Equal("todo", t.GetProperty("status").GetString());
        Assert.Equal(3, t.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task CreateTask_Returns201_WithInProgressStatus()
    {
        var r = await _auth.PostAsJsonAsync("/tasks",
            new { title = "In Progress Task", priority = 2, status = "in-progress" }, Json);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var t = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("in-progress", t.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateTask_Returns422_WhenTitleIsEmpty()
    {
        var r = await _auth.PostAsJsonAsync("/tasks", new { title = "", priority = 3 }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task CreateTask_Returns422_WhenTitleIsWhitespace()
    {
        var r = await _auth.PostAsJsonAsync("/tasks", new { title = "   ", priority = 3 }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task CreateTask_Returns422_WhenTitleExceeds120Chars()
    {
        var r = await _auth.PostAsJsonAsync("/tasks",
            new { title = new string('x', 121), priority = 3 }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task CreateTask_Returns422_WhenPriorityTooLow()
    {
        var r = await _auth.PostAsJsonAsync("/tasks", new { title = "Task", priority = 0 }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task CreateTask_Returns422_WhenPriorityTooHigh()
    {
        var r = await _auth.PostAsJsonAsync("/tasks", new { title = "Task", priority = 6 }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task CreateTask_Returns400_WhenStatusIsInvalid()
    {
        var r = await _auth.PostAsJsonAsync("/tasks",
            new { title = "Task", priority = 3, status = "unknown" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTask_Returns404_WhenNotFound()
    {
        var r = await _auth.GetAsync("/tasks/999999");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task GetTask_ReturnsTask_WhenExists()
    {
        var created = await CreateTask("Read Test Task", 4);
        var id = created.GetProperty("id").GetInt32();

        var r = await _auth.GetAsync($"/tasks/{id}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var t = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Read Test Task", t.GetProperty("title").GetString());
        Assert.Equal(4, t.GetProperty("priority").GetInt32());
    }

    [Fact]
    public async Task GetTasks_ReturnsList()
    {
        await CreateTask("List Task A", 1);
        await CreateTask("List Task B", 2);

        var r = await _auth.GetAsync("/tasks");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var tasks = await r.Content.ReadFromJsonAsync<JsonElement[]>(Json);
        Assert.NotNull(tasks);
        Assert.True(tasks.Length >= 2);
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTask_UpdatesOnlySuppliedFields()
    {
        var created = await CreateTask("Update Task", 3, "todo");
        var id = created.GetProperty("id").GetInt32();

        var r = await _auth.PatchAsJsonAsync($"/tasks/{id}", new { status = "in-progress" }, Json);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var t = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Update Task", t.GetProperty("title").GetString());   // unchanged
        Assert.Equal("in-progress", t.GetProperty("status").GetString());  // updated
        Assert.Equal(3, t.GetProperty("priority").GetInt32());             // unchanged
    }

    [Fact]
    public async Task UpdateTask_Returns404_WhenNotFound()
    {
        var r = await _auth.PatchAsJsonAsync("/tasks/999999", new { title = "X" }, Json);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTask_ReturnsDeletedTrue_AndTaskIsGone()
    {
        var created = await CreateTask("Delete Task", 1);
        var id = created.GetProperty("id").GetInt32();

        var r = await _auth.DeleteAsync($"/tasks/{id}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("deleted").GetBoolean());
        Assert.Equal(id, body.GetProperty("id").GetInt32());

        var gone = await _auth.GetAsync($"/tasks/{id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    // ── Filters ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTasks_FiltersByStatus()
    {
        await CreateTask("Filter Todo Task", 3, "todo");
        await CreateTask("Filter Done Task", 3, "completed");

        var r = await _auth.GetAsync("/tasks?status=completed");
        var tasks = await r.Content.ReadFromJsonAsync<JsonElement[]>(Json);
        Assert.NotNull(tasks);
        Assert.All(tasks, t => Assert.Equal("completed", t.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task GetTasks_FiltersByDueDate()
    {
        var date = "2099-12-25";
        await CreateTask("Due Date Match", 3, "todo", date);
        await CreateTask("Due Date Miss",  3, "todo", "2099-12-24");

        var r = await _auth.GetAsync($"/tasks?due_date={date}");
        var tasks = await r.Content.ReadFromJsonAsync<JsonElement[]>(Json);
        Assert.NotNull(tasks);
        Assert.Contains(tasks, t => t.GetProperty("title").GetString() == "Due Date Match");
        Assert.DoesNotContain(tasks, t => t.GetProperty("title").GetString() == "Due Date Miss");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private async Task<JsonElement> CreateTask(
        string title, int priority, string status = "todo", string? dueDate = null)
    {
        var r = await _auth.PostAsJsonAsync("/tasks",
            new { title, priority, status, due_date = dueDate }, Json);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<JsonElement>(Json);
    }
}
