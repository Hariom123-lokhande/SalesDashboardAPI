using Microsoft.EntityFrameworkCore;
using SalesDashboardAPI.Models;

namespace SalesDashboardAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Sales> Sales { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sales>()
                .Property(s => s.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Sales>()
                .Property(s => s.TotalSales)
                .HasPrecision(18, 2);
        }
    }
}