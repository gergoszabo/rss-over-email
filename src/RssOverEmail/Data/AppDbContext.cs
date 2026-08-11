using Microsoft.EntityFrameworkCore;
using RssOverEmail.Models;

namespace RssOverEmail.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Feed> Feeds => Set<Feed>();
    public DbSet<FeedItem> FeedItems => Set<FeedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Feed>(entity =>
        {
            entity.ToTable("Feeds");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2048);
        });

        modelBuilder.Entity<FeedItem>(entity =>
        {
            entity.ToTable("FeedItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.Link).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.Hash).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => e.Hash).IsUnique();
            entity.Property(e => e.Comments).HasMaxLength(2048);
            entity.Property(e => e.PubDate).HasMaxLength(64);
            entity.HasOne(e => e.Feed)
                  .WithMany(f => f.Items)
                  .HasForeignKey(e => e.FeedId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
