using BloodLink.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Api.Data;

public class BloodLinkDbContext : DbContext
{
    public BloodLinkDbContext(DbContextOptions<BloodLinkDbContext> options) : base(options)
    {
    }

    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<DonationAppointment> DonationAppointments => Set<DonationAppointment>();
    public DbSet<BloodUnit> BloodUnits => Set<BloodUnit>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();
    public DbSet<RequestAllocation> RequestAllocations => Set<RequestAllocation>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Donor>(entity =>
        {
            entity.Property(d => d.BloodType).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(d => d.FullName);
        });

        modelBuilder.Entity<DonationAppointment>(entity =>
        {
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(a => a.Donor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DonorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.ResultingBloodUnit)
                .WithMany()
                .HasForeignKey(a => a.ResultingBloodUnitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BloodUnit>(entity =>
        {
            entity.Property(u => u.BloodType).HasConversion<string>().HasMaxLength(20);
            entity.Property(u => u.Component).HasConversion<string>().HasMaxLength(20);
            entity.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(u => u.Version).IsConcurrencyToken();
            entity.HasIndex(u => u.Code).IsUnique();
            entity.HasOne(u => u.Donor)
                .WithMany()
                .HasForeignKey(u => u.DonorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Hospital>(entity =>
        {
            entity.HasIndex(h => h.Name);
        });

        modelBuilder.Entity<BloodRequest>(entity =>
        {
            entity.Property(r => r.BloodType).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.Component).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(r => r.Hospital)
                .WithMany()
                .HasForeignKey(r => r.HospitalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RequestAllocation>(entity =>
        {
            entity.HasOne(a => a.BloodRequest)
                .WithMany(r => r.Allocations)
                .HasForeignKey(a => a.BloodRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.BloodUnit)
                .WithMany()
                .HasForeignKey(a => a.BloodUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasOne(u => u.Hospital)
                .WithMany()
                .HasForeignKey(u => u.HospitalId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
