using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new TaskItemId(value))
            .ValueGeneratedNever();

        builder.Property(t => t.Title).HasMaxLength(TaskItem.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(TaskItem.DescriptionMaxLength);
        builder.Property(t => t.AssignedTo).HasMaxLength(TaskItem.AssignedToMaxLength);

        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.Ignore(t => t.DomainEvents);

        // Soft delete: deleted tasks are invisible to every query.
        // Use IgnoreQueryFilters() explicitly if deleted rows are ever needed (e.g. audit/restore).
        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Priority);
        builder.HasIndex(t => t.AssignedTo);
        builder.HasIndex(t => t.DueDateUtc);
    }
}
