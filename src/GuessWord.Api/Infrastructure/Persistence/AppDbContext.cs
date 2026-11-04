using GuessWord.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<RoomMember> RoomMembers { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<GamePlayer> GamePlayers { get; set; }
        public DbSet<GameAttempt> GameAttempts { get; set; }
        public DbSet<Word> Words { get; set; }

        public DbSet<ActiveGameParticipation> ActiveGameParticipations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Login)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Name)
                .IsUnique();

            modelBuilder.Entity<Room>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<Room>()
                .Property(x => x.Status)
                .HasDefaultValue(GuessWord.Contracts.Enums.RoomStatus.Waiting);

            modelBuilder.Entity<Word>()
                .HasIndex(x => x.Text)
                .IsUnique();

            modelBuilder.Entity<Word>()
                .Property(x => x.Embedding)
                .HasColumnType("vector(800)");

            modelBuilder.Entity<GamePlayer>()
                .HasIndex(x => new { x.UserId, x.IsActiveSingleGame })
                .IsUnique()
                .HasFilter("\"IsActiveSingleGame\" = true");

            modelBuilder.Entity<GameAttempt>()
                .HasIndex(a => new { a.GameId, a.UserId, a.Word })
                .IsUnique().HasFilter("\"IsValid\" = true");

            modelBuilder.Entity<GamePlayer>()
                .HasAlternateKey(p => new { p.GameId, p.UserId });

            modelBuilder.Entity<ActiveGameParticipation>().HasKey(p => p.UserId);
            modelBuilder.Entity<ActiveGameParticipation>()
                .HasOne(p => p.Player).WithMany()
                .HasForeignKey(p => new { p.GameId, p.UserId })
                .HasPrincipalKey(p => new { p.GameId, p.UserId });

            modelBuilder.Entity<Game>()
                .HasOne(g => g.SecretWord)
                .WithMany(w => w.SecretWordGames)
                .HasForeignKey(g => g.SecretWordId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Room>()
                .HasOne(r => r.HostUser)
                .WithMany(u => u.HostedRooms)
                .HasForeignKey(r => r.HostUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RoomMember>().HasIndex(m => new { m.RoomId, m.UserId }).IsUnique();
            modelBuilder.Entity<RoomMember>().HasOne(m => m.Room).WithMany(r => r.Members)
                .HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RoomMember>().HasOne(m => m.User).WithMany()
                .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Game>().HasOne(g => g.Room).WithMany()
                .HasForeignKey(g => g.RoomId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Room>()
                .HasOne(r => r.Game)
                .WithMany()
                .HasForeignKey(r => r.GameId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
