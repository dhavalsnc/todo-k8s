using Microsoft.EntityFrameworkCore;
using todo_api.Models;

namespace todo_api.Data;

public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
}
