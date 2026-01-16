using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Data
{
    // Entity와 DB를 매핑 및 접근 시켜주는 DbContext
    public class ApplicationDBContext : DbContext
    {
        public ApplicationDBContext(DbContextOptions<ApplicationDBContext> dbContextOptions)
        : base(dbContextOptions)
        {

        }

        // 모델 커스텀
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // AccountId 컬럼에 Unique Index 부여해서 빠른 조회 및 race condition 방어
            modelBuilder.Entity<User>()
            .HasIndex(u => u.AccountId)
            .IsUnique();

            // Nickname 컬럼에 Unique Index 부여해서 빠른 조회 및 race condition 방어
            modelBuilder.Entity<User>()
            .HasIndex(u => u.Nickname)
            .IsUnique();

            // 승/패자는 각각 본인 UserId를 찾고 FK로 상대 UserId 참조
            modelBuilder.Entity<BattleRecord>()
            .HasOne(r => r.WinnerUser)
            .WithMany()
            .HasForeignKey(r => r.WinnerId)
            .OnDelete(DeleteBehavior.Restrict); // User 삭제 시 전적 데이터는 함께 삭제되지 않도록 제한

            modelBuilder.Entity<BattleRecord>()
            .HasOne(r => r.LoserUser)
            .WithMany()
            .HasForeignKey(r => r.LoserId)
            .OnDelete(DeleteBehavior.Restrict); // User 삭제 시 전적 데이터는 함께 삭제되지 않도록 제한

            // 재발급 토큰 모델 설정
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(r => r.UserId)
                    .IsUnique();

                entity.HasIndex(r => r.Token)
                    .IsUnique();

                entity.HasOne(r => r.User)
                    .WithOne()
                    .HasForeignKey<RefreshToken>(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
        
        // DbContext에 포함되는 엔티티 집합
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshToken { get; set; }
        public DbSet<BattleRecord> BattleRecords { get; set; }
    }
}