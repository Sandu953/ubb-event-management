using Microsoft.EntityFrameworkCore;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers
        => Set<OrganizationMember>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations
        => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(x => x.Email).IsUnique();
            b.Property(x => x.GlobalRole).HasConversion<string>();
        });

        modelBuilder.Entity<Organization>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<OrganizationMember>(b =>
        {
            b.HasKey(x => new { x.UserId, x.OrganizationId });
            b.Property(x => x.OrgRole).HasConversion<string>();

            b.HasOne<User>().WithMany()
                .HasForeignKey(x => x.UserId);

            b.HasOne<Organization>().WithMany()
                .HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<Event>(b =>
        {
            b.Property(x => x.Status).HasConversion<string>();
            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasOne<Organization>().WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>().WithMany()
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Events_Time",
                    "\"EndTime\" > \"StartTime\""
                );

                t.HasCheckConstraint(
                    "CK_Events_Capacity",
                    "\"MaxCapacity\" > 0"
                );
            });
        });

        modelBuilder.Entity<Registration>(b =>
        {
            b.HasIndex(x => new { x.EventId, x.UserId })
                .IsUnique();

            b.Property(x => x.Status).HasConversion<string>();

            b.HasOne<Event>().WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>().WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}