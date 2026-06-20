using Microsoft.EntityFrameworkCore;
using todo_api.Data;

var builder = WebApplication.CreateBuilder(args);

var apiVersion = builder.Configuration["ApiVersion"]
    ?? throw new InvalidOperationException("Configuration key 'ApiVersion' is not set.");

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Environment variable 'DB_CONNECTION_STRING' is not set.");

var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD")
    ?? throw new InvalidOperationException("Environment variable 'DB_PASSWORD' is not set.");

connectionString = $"{connectionString}Password={dbPassword};";

builder.Services.AddDbContext<TodoDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { apiVersion }));

app.MapGet("/todoitems", async (TodoDbContext db) =>
{
    var items = await db.TodoItems.ToListAsync();
    return Results.Ok(new { apiVersion, items });
});

app.Run();
