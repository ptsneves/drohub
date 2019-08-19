using DroHub.Areas.Identity.Data;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DroHub.Data
{
    public class DroHubContext : IdentityDbContext<DroHubUser>
    {
        public DroHubContext(DbContextOptions<DroHubContext> options)
            : base(options)
        {
        }

        public DbSet<DroHubUser> DroHubUsers { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<LogEntry> Logs { get; set; }

        public DbSet<DronePosition> Positions { get; set; }
        //public DbSet<DeviceSettings> DeviceSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);

            // ----- KEYS AND INDEXES -------------------------------
            builder.Entity<Device>()
                .HasIndex(d => d.SerialNumber)
                .IsUnique();

            builder.Entity<Device>()
                .HasKey(d => d.Id);

            builder.Entity<DronePosition>()
                .HasKey(d => d.Id);

            builder.Entity<DronePosition>()
                .HasIndex(d => d.Serial);

            builder.Entity<LogEntry>()
                .HasKey(d => d.Id);

            // ----- REQUIRED ATTRIBUTES ----------------------------
            builder.Entity<Device>()
                .Property(d => d.SerialNumber)
                .IsRequired();

            // ----- RELATIONSHIPS ----------------------------------
            // --- One to Many (1 User <-> n Devices)
            builder.Entity<DroHubUser>()
                .HasMany(d => d.Devices)
                .WithOne(u => u.User);

            builder.Entity<DronePosition>()
                .HasOne(p => p.Device)
                .WithMany(d => d.positions)
                .HasForeignKey(p => p.Serial)
                .HasPrincipalKey(d => d.SerialNumber);

            builder.Entity<Device>()
                .HasMany(d => d.positions)
                .WithOne(p => p.Device)
                .IsRequired();

        }
    }
}