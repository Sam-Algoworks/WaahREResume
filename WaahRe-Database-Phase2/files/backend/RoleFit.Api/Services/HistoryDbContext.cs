using Microsoft.EntityFrameworkCore;

namespace RoleFit.Api.Services;

// SQL Server-backed metadata records. Original file bytes remain encrypted in App_Data/secure-originals.
public sealed class HistoryDbContext(DbContextOptions<HistoryDbContext> options) : DbContext(options)
{
    public DbSet<HistoryJobRow> Jobs => Set<HistoryJobRow>();
    public DbSet<HistoryAnalysisRow> Analyses => Set<HistoryAnalysisRow>();
    public DbSet<HistoryDraftRow> Drafts => Set<HistoryDraftRow>();
    public DbSet<HistoryArchiveRow> Archives => Set<HistoryArchiveRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HistoryJobRow>().HasKey(x => x.Id);
        modelBuilder.Entity<HistoryAnalysisRow>().HasKey(x => x.Id);
        modelBuilder.Entity<HistoryDraftRow>().HasKey(x => x.Id);
        modelBuilder.Entity<HistoryArchiveRow>().HasKey(x => x.Id);
        modelBuilder.Entity<HistoryJobRow>().Property(x => x.Payload).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<HistoryAnalysisRow>().Property(x => x.Payload).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<HistoryDraftRow>().Property(x => x.Payload).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<HistoryArchiveRow>().Property(x => x.Payload).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<HistoryArchiveRow>().Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false);
        modelBuilder.Entity<HistoryArchiveRow>().HasIndex(x => x.Sha256).IsUnique();
        modelBuilder.Entity<HistoryDraftRow>().HasIndex(x => x.UpdatedAt);
        modelBuilder.Entity<HistoryAnalysisRow>().HasIndex(x => x.CreatedAt);
    }
}

public sealed class HistoryJobRow { public Guid Id { get; set; } public string Payload { get; set; } = ""; }
public sealed class HistoryAnalysisRow { public Guid Id { get; set; } public DateTimeOffset CreatedAt { get; set; } public string Payload { get; set; } = ""; }
public sealed class HistoryDraftRow { public Guid Id { get; set; } public DateTimeOffset UpdatedAt { get; set; } public string Payload { get; set; } = ""; }
public sealed class HistoryArchiveRow { public Guid Id { get; set; } public string Sha256 { get; set; } = ""; public string Payload { get; set; } = ""; }
