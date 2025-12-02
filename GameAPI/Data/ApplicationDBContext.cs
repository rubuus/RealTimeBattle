using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Data
{
    public class ApplicationDBContext : DbContext
    {
        public ApplicationDBContext(DbContextOptions<ApplicationDBContext> dbContextOptions)
        : base(dbContextOptions)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BattleRecord>()
                .HasOne(r => r.WinnerUser)
                .WithMany()
                .HasForeignKey(r => r.WinnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BattleRecord>()
                .HasOne(r => r.LoserUser)
                .WithMany()
                .HasForeignKey(r => r.LoserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
        
        public DbSet<User> Users { get; set; }
        public DbSet<BattleRecord> BattleRecords { get; set; }
    }
}