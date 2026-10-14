using Microsoft.EntityFrameworkCore;

namespace TaskApi
{
    public sealed class TaskDbContext(DbContextOptions<TaskDbContext> options)
        : DbContext(options)
    {
        public DbSet<TaskItem> Tasks => Set<TaskItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.ToTable("tasks");
                entity.HasKey(task => task.Id);

                entity.Property(task => task.Id).HasColumnName("id");
                entity.Property(task => task.Title)
                    .HasColumnName("title")
                    .HasMaxLength(200)
                    .IsRequired();
                entity.Property(task => task.IsCompleted).HasColumnName("is_completed");
                entity.Property(task => task.CreatedAt).HasColumnName("created_at");
                entity.Property(task => task.CompletedAt).HasColumnName("completed_at");
                entity.Property(task => task.Priority)
                    .HasColumnName("priority")
                    .HasConversion<string>()
                    .HasMaxLength(6);
                entity.Property(task => task.RowVersion)
                    .HasColumnName("xmin")
                    .IsRowVersion();
            });
        }
    }
}
