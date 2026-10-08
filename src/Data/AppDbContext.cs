using Microsoft.EntityFrameworkCore;
using ServerMain.Game;

namespace ServerMain.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ClubData> ClubData { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Props> Props { get; set; } = null!;
        public DbSet<Story> Stories { get; set; } = null!;
        public DbSet<Spinlog> Spinlogs { get; set; } = null!;
        public DbSet<Multlog> Multlogs { get; set; } = null!;
        public DbSet<Walletlog> Walletlogs { get; set; } = null!;
        public DbSet<Banklog> Banklogs { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ClubData>(entity =>
            {
                entity.ToTable("ClubData");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).ValueGeneratedOnAdd();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Secret).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.GAL).HasConversion<uint>();
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<Props>(entity =>
            {
                entity.ToTable("Props");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).ValueGeneratedOnAdd();
                entity.Property(e => e.Access).HasConversion<uint>();
                entity.HasIndex(e => new { e.CID, e.UID }).IsUnique();
                entity.HasIndex(e => e.CID);
                entity.HasIndex(e => e.UID);
            });

            modelBuilder.Entity<Story>(entity =>
            {
                entity.ToTable("Story");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).ValueGeneratedOnAdd();
                entity.Property(e => e.Game).HasConversion<uint>();
                entity.HasIndex(e => e.CID);
                entity.HasIndex(e => e.UID);
            });

            modelBuilder.Entity<Spinlog>(entity =>
            {
                entity.ToTable("Spinlog");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.GID);
            });

            modelBuilder.Entity<Multlog>(entity =>
            {
                entity.ToTable("Multlog");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.GID);
            });

            modelBuilder.Entity<Walletlog>(entity =>
            {
                entity.ToTable("Walletlog");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.CID);
                entity.HasIndex(e => e.UID);
            });

            modelBuilder.Entity<Banklog>(entity =>
            {
                entity.ToTable("Banklog");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.CID);
            });
        }
    }
}
