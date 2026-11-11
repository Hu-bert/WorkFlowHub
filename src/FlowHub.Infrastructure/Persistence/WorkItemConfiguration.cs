using FlowHub.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowHub.Infrastructure.Persistence;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => item.Key).IsUnique();

        builder.HasMany(item => item.Repos).WithOne().HasForeignKey(repo => repo.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Steps).WithOne().HasForeignKey(step => step.WorkItemId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(item => item.OrderedSteps);
        builder.Ignore(item => item.CurrentStep);
        builder.Ignore(item => item.IsFinished);
        builder.Ignore(item => item.Usage);
    }
}

internal sealed class TaskRepoConfiguration : IEntityTypeConfiguration<TaskRepo>
{
    public void Configure(EntityTypeBuilder<TaskRepo> builder)
    {
        builder.HasKey(repo => repo.Id);
        builder.Property(repo => repo.Id).ValueGeneratedNever();
    }
}

internal sealed class StepRunConfiguration : IEntityTypeConfiguration<StepRun>
{
    public void Configure(EntityTypeBuilder<StepRun> builder)
    {
        builder.HasKey(step => step.Id);
        builder.Property(step => step.Id).ValueGeneratedNever();

        builder.ComplexProperty(step => step.Usage, usage => usage.Ignore(value => value.TotalTokens));

        builder.Ignore(step => step.IsFinished);

    }
}
