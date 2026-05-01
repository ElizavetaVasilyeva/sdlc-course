using Microsoft.EntityFrameworkCore;

namespace Api;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.Property(t => t.Status).HasConversion<string>();
            e.Property(t => t.DueDate).HasConversion(
                d => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : null,
                s => s != null ? DateOnly.Parse(s) : (DateOnly?)null
            );
        });
    }
}
