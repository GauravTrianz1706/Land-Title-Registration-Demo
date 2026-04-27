using Microsoft.EntityFrameworkCore;
using LandTitleRegistration.Models;

namespace LandTitleRegistration.Data
{
    public class LandTitleDbContext : DbContext
    {
        public LandTitleDbContext(DbContextOptions<LandTitleDbContext> options)
            : base(options)
        {
        }

        public DbSet<TitleRegistration> TitleRegistrations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TitleRegistration>(entity =>
            {
                entity.HasIndex(e => e.ParcelId);
                entity.HasIndex(e => e.TitleRef).IsUnique();
            });
        }
    }
}
