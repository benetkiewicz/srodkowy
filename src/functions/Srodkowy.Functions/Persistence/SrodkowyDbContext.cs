using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Models;

namespace Srodkowy.Functions.Persistence;

public sealed class SrodkowyDbContext(DbContextOptions<SrodkowyDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public DbSet<Source> Sources => Set<Source>();

    public DbSet<Article> Articles => Set<Article>();

    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();

    public DbSet<ClusterRun> ClusterRuns => Set<ClusterRun>();

    public DbSet<CandidateCluster> CandidateClusters => Set<CandidateCluster>();

    public DbSet<CandidateClusterArticle> CandidateClusterArticles => Set<CandidateClusterArticle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var sourceBuilder = modelBuilder.Entity<Source>();
        sourceBuilder.ToTable("Sources");
        sourceBuilder.HasKey(source => source.Id);
        sourceBuilder.Property(source => source.Name).HasMaxLength(200).IsRequired();
        sourceBuilder.Property(source => source.BaseUrl).HasMaxLength(500).IsRequired();
        sourceBuilder.Property(source => source.DiscoveryUrl).HasMaxLength(500).IsRequired();
        sourceBuilder.Property(source => source.DiscoveryIncludeTags).HasColumnType("nvarchar(max)");
        sourceBuilder.Property(source => source.DiscoveryExcludeTags).HasColumnType("nvarchar(max)");
        sourceBuilder.Property(source => source.Camp).HasMaxLength(20).IsRequired();
        sourceBuilder.HasIndex(source => source.Name).IsUnique();
        sourceBuilder.HasIndex(source => source.BaseUrl).IsUnique();
        sourceBuilder.HasData(SourceRegistry.All.Select(definition => new Source
        {
            Id = definition.Id,
            Name = definition.Name,
            BaseUrl = definition.BaseUrl,
            DiscoveryUrl = definition.DiscoveryUrl,
            DiscoveryIncludeTags = SerializeTags(definition.DiscoveryIncludeTags),
            DiscoveryExcludeTags = SerializeTags(definition.DiscoveryExcludeTags),
            Camp = definition.Camp,
            Active = definition.Active
        }));

        var articleBuilder = modelBuilder.Entity<Article>();
        articleBuilder.ToTable("Articles");
        articleBuilder.HasKey(article => article.Id);
        articleBuilder.Property(article => article.Url).HasMaxLength(1000).IsRequired();
        articleBuilder.Property(article => article.Title).HasMaxLength(500).IsRequired();
        articleBuilder.Property(article => article.ContentMarkdown).HasColumnType("nvarchar(max)").IsRequired();
        articleBuilder.Property(article => article.ContentText).HasColumnType("nvarchar(max)").IsRequired();
        articleBuilder.Property(article => article.CleanedContentText).HasColumnType("nvarchar(max)");
        articleBuilder.Property(article => article.CleanupStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(ArticleCleanupStatus.Pending)
            .IsRequired();
        articleBuilder.Property(article => article.CleanedAt).HasColumnType("datetimeoffset");
        articleBuilder.Property(article => article.CleanupStartedAt).HasColumnType("datetimeoffset");
        articleBuilder.Property(article => article.CleanupRunId).HasColumnType("uniqueidentifier");
        articleBuilder.Property(article => article.CleanupProcessor).HasMaxLength(100);
        articleBuilder.Property(article => article.CleanupError).HasColumnType("nvarchar(max)");
        articleBuilder.Property(article => article.CleanupInputHash).HasMaxLength(64);
        articleBuilder.Property(article => article.CleanupFlagsJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]").IsRequired();
        articleBuilder.Property(article => article.QualityScore).HasDefaultValue(0).IsRequired();
        articleBuilder.Property(article => article.NeedsReview).HasDefaultValue(false).IsRequired();
        articleBuilder.Property(article => article.IsProbablyNonArticle).HasDefaultValue(false).IsRequired();
        articleBuilder.Property(article => article.Embedding).HasColumnType("vector(1536)");
        articleBuilder.Property(article => article.EmbeddingModel).HasMaxLength(100);
        articleBuilder.Property(article => article.EmbeddingStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(ArticleEmbeddingStatus.Pending)
            .IsRequired();
        articleBuilder.Property(article => article.EmbeddedAt).HasColumnType("datetimeoffset");
        articleBuilder.Property(article => article.EmbeddingStartedAt).HasColumnType("datetimeoffset");
        articleBuilder.Property(article => article.EmbeddingRunId).HasColumnType("uniqueidentifier");
        articleBuilder.Property(article => article.EmbeddingError).HasColumnType("nvarchar(max)");
        articleBuilder.Property(article => article.EmbeddingTextHash).HasMaxLength(64);
        articleBuilder.Property(article => article.MetadataJson).HasColumnType("nvarchar(max)").IsRequired();
        articleBuilder.Property(article => article.PublishedAt).HasColumnType("datetimeoffset");
        articleBuilder.Property(article => article.ScrapedAt).HasColumnType("datetimeoffset");
        articleBuilder.HasIndex(article => article.Url).IsUnique();
        articleBuilder.HasIndex(article => article.SourceId);
        articleBuilder.HasIndex(article => article.ScrapedAt);
        articleBuilder.HasIndex(article => article.CleanupStatus);
        articleBuilder.HasIndex(article => new { article.CleanupStatus, article.ScrapedAt });
        articleBuilder.HasIndex(article => article.CleanupRunId);
        articleBuilder.HasIndex(article => article.EmbeddingStatus);
        articleBuilder.HasIndex(article => new { article.EmbeddingStatus, article.ScrapedAt });
        articleBuilder.HasIndex(article => article.EmbeddingRunId);
        articleBuilder.HasOne(article => article.Source)
            .WithMany(source => source.Articles)
            .HasForeignKey(article => article.SourceId)
            .OnDelete(DeleteBehavior.Cascade);

        var ingestionRunBuilder = modelBuilder.Entity<IngestionRun>();
        ingestionRunBuilder.ToTable("IngestionRuns");
        ingestionRunBuilder.HasKey(run => run.Id);
        ingestionRunBuilder.Property(run => run.StartedAt).HasColumnType("datetimeoffset");
        ingestionRunBuilder.Property(run => run.CompletedAt).HasColumnType("datetimeoffset");
        ingestionRunBuilder.Property(run => run.Status).HasMaxLength(40).IsRequired();
        ingestionRunBuilder.Property(run => run.TriggeredBy).HasMaxLength(100).IsRequired();
        ingestionRunBuilder.Property(run => run.ErrorSummary).HasColumnType("nvarchar(max)");
        ingestionRunBuilder.HasIndex(run => run.StartedAt);

        var clusterRunBuilder = modelBuilder.Entity<ClusterRun>();
        clusterRunBuilder.ToTable("ClusterRuns");
        clusterRunBuilder.HasKey(run => run.Id);
        clusterRunBuilder.Property(run => run.StartedAt).HasColumnType("datetimeoffset");
        clusterRunBuilder.Property(run => run.CompletedAt).HasColumnType("datetimeoffset");
        clusterRunBuilder.Property(run => run.Status).HasMaxLength(40).IsRequired();
        clusterRunBuilder.Property(run => run.TriggeredBy).HasMaxLength(100).IsRequired();
        clusterRunBuilder.Property(run => run.ErrorSummary).HasColumnType("nvarchar(max)");
        clusterRunBuilder.HasIndex(run => run.StartedAt);

        var candidateClusterBuilder = modelBuilder.Entity<CandidateCluster>();
        candidateClusterBuilder.ToTable("CandidateClusters");
        candidateClusterBuilder.HasKey(cluster => cluster.Id);
        candidateClusterBuilder.Property(cluster => cluster.Status).HasMaxLength(20).IsRequired();
        candidateClusterBuilder.Property(cluster => cluster.SelectionVersion).HasMaxLength(50).IsRequired();
        candidateClusterBuilder.Property(cluster => cluster.WindowStartAt).HasColumnType("datetimeoffset");
        candidateClusterBuilder.Property(cluster => cluster.WindowEndAt).HasColumnType("datetimeoffset");
        candidateClusterBuilder.HasIndex(cluster => cluster.ClusterRunId);
        candidateClusterBuilder.HasIndex(cluster => cluster.Rank);
        candidateClusterBuilder.HasIndex(cluster => cluster.Status);
        candidateClusterBuilder.HasOne(cluster => cluster.ClusterRun)
            .WithMany(run => run.CandidateClusters)
            .HasForeignKey(cluster => cluster.ClusterRunId)
            .OnDelete(DeleteBehavior.Cascade);
        candidateClusterBuilder.HasOne(cluster => cluster.RepresentativeArticle)
            .WithMany()
            .HasForeignKey(cluster => cluster.RepresentativeArticleId)
            .OnDelete(DeleteBehavior.NoAction);

        var candidateClusterArticleBuilder = modelBuilder.Entity<CandidateClusterArticle>();
        candidateClusterArticleBuilder.ToTable("CandidateClusterArticles");
        candidateClusterArticleBuilder.HasKey(clusterArticle => new { clusterArticle.CandidateClusterId, clusterArticle.ArticleId });
        candidateClusterArticleBuilder.Property(clusterArticle => clusterArticle.Camp).HasMaxLength(20).IsRequired();
        candidateClusterArticleBuilder.HasIndex(clusterArticle => clusterArticle.ArticleId);
        candidateClusterArticleBuilder.HasOne(clusterArticle => clusterArticle.CandidateCluster)
            .WithMany(cluster => cluster.Articles)
            .HasForeignKey(clusterArticle => clusterArticle.CandidateClusterId)
            .OnDelete(DeleteBehavior.Cascade);
        candidateClusterArticleBuilder.HasOne(clusterArticle => clusterArticle.Article)
            .WithMany()
            .HasForeignKey(clusterArticle => clusterArticle.ArticleId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static string? SerializeTags(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(values, SerializerOptions);
    }
}
