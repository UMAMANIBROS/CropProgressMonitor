using CropProgressMonitor.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Data;

/// <summary>
/// Entity Framework Core database context for CropProgress Monitor.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<Farmer>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Field> Fields => Set<Field>();
    public DbSet<Crop> Crops => Set<Crop>();
    public DbSet<CropStage> CropStages => Set<CropStage>();
    public DbSet<FieldCrop> FieldCrops => Set<FieldCrop>();
    public DbSet<WeatherRecord> WeatherRecords => Set<WeatherRecord>();
    public DbSet<GrowthEstimate> GrowthEstimates => Set<GrowthEstimate>();
    public DbSet<Advisory> Advisories => Set<Advisory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Farmer>(entity =>
        {
            entity.ToTable("Farmers");
        });

        builder.Entity<Field>(entity =>
        {
            entity.ToTable("Fields");
            entity.HasOne(f => f.Farmer)
                .WithMany(u => u.Fields)
                .HasForeignKey(f => f.FarmerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Crop>(entity =>
        {
            entity.ToTable("Crops");
            entity.HasIndex(c => c.Name).IsUnique();
        });

        builder.Entity<CropStage>(entity =>
        {
            entity.ToTable("CropStages");
            entity.HasOne(s => s.Crop)
                .WithMany(c => c.CropStages)
                .HasForeignKey(s => s.CropId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.CropId, s.StageOrder }).IsUnique();
        });

        builder.Entity<FieldCrop>(entity =>
        {
            entity.ToTable("FieldCrops");
            entity.Property(fc => fc.SowingDate).HasColumnType("date");

            entity.HasOne(fc => fc.Field)
                .WithMany(f => f.FieldCrops)
                .HasForeignKey(fc => fc.FieldId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(fc => fc.Crop)
                .WithMany(c => c.FieldCrops)
                .HasForeignKey(fc => fc.CropId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WeatherRecord>(entity =>
        {
            entity.ToTable("WeatherRecords");
            entity.Property(w => w.Date).HasColumnType("date");

            entity.HasOne(w => w.Field)
                .WithMany(f => f.WeatherRecords)
                .HasForeignKey(w => w.FieldId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(w => new { w.FieldId, w.Date }).IsUnique();
        });

        builder.Entity<GrowthEstimate>(entity =>
        {
            entity.ToTable("GrowthEstimates");
            entity.Property(g => g.EstimateDate).HasColumnType("date");
            entity.Property(g => g.NextStageDate).HasColumnType("date");

            entity.HasOne(g => g.FieldCrop)
                .WithMany(fc => fc.GrowthEstimates)
                .HasForeignKey(g => g.FieldCropId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(g => g.CurrentStage)
                .WithMany()
                .HasForeignKey(g => g.CurrentStageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(g => g.NextStage)
                .WithMany()
                .HasForeignKey(g => g.NextStageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(g => new { g.FieldCropId, g.EstimateDate }).IsUnique();
        });

        builder.Entity<Advisory>(entity =>
        {
            entity.ToTable("Advisories");
            entity.HasOne(a => a.CropStage)
                .WithMany(s => s.Advisories)
                .HasForeignKey(a => a.CropStageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
