using DroHub.Areas.Identity.Data;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
        public DbSet<DroneBatteryLevel> DroneBatteryLevels { get; set; }

        public DbSet<DroneRadioSignal> DroneRadioSignals { get; set; }

        public DbSet<DroneFlyingState> DroneFlyingStates { get; set; }
        public DbSet<DroneReply> DroneReplies { get; set; }
        public DbSet<DroneVideoStateResult> DroneVideoStatesResults { get; set; }
        //public DbSet<DeviceSettings> DeviceSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // builder.Ignore<DroneBatteryLevel.Isset>();

            // foreach (var property in builder.Model.GetEntityTypes()
            //     .SelectMany(t => t.GetProperties())
            //     .Where(p => p.ClrType == typeof(string)))
            // {
            //     if (property.GetMaxLength() == null)
            //         property.SetMaxLength(450);
            // }]

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

            builder.Entity<DroneBatteryLevel>()
                .HasKey(d => d.Id);

            builder.Entity<DroneBatteryLevel>()
                .HasIndex(d => d.Serial);

            builder.Entity<DroneRadioSignal>()
                .HasKey(d => d.Id);

            builder.Entity<DroneRadioSignal>()
                .HasIndex(d => d.Serial);

            builder.Entity<DroneFlyingState>()
                .HasKey(d => d.Id);

            builder.Entity<DroneFlyingState>()
                .HasIndex(d => d.Serial);

            builder.Entity<DroneReply>()
                .HasIndex(d => d.Serial);

            builder.Entity<DroneVideoStateResult>()
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

            builder.Entity<DroneBatteryLevel>()
                .HasOne(p => p.Device)
                .WithMany(d => d.battery_levels)
                .HasForeignKey(p => p.Serial)
                .HasPrincipalKey(d => d.SerialNumber);

            builder.Entity<DroneRadioSignal>()
                .HasOne(p => p.Device)
                .WithMany(d => d.radio_signals)
                .HasForeignKey(p => p.Serial)
                .HasPrincipalKey(d => d.SerialNumber);

            builder.Entity<DroneFlyingState>()
                .HasOne(p => p.Device)
                .WithMany(d => d.flying_states)
                .HasForeignKey(p => p.Serial)
                .HasPrincipalKey(d => d.SerialNumber);

            builder.Entity<DroneReply>()
                .HasOne(p => p.Device)
                .WithMany(d => d.drone_replies)
                .HasForeignKey(p => p.Serial)
                .HasPrincipalKey(d => d.SerialNumber);

            builder.Entity<DroneVideoStateResult>()
                .HasOne(p => p.Device)
                .WithMany(d => d.drone_video_states)
                .HasForeignKey(p => p.Serial)
                .HasPrincipalKey(d => d.SerialNumber);

            builder.Entity<Device>()
                .HasMany(d => d.positions)
                .WithOne(p => p.Device)
                .IsRequired();

            builder.Entity<Device>()
                .HasMany(d => d.battery_levels)
                .WithOne(p => p.Device)
                .IsRequired();

            builder.Entity<Device>()
                .HasMany(d => d.radio_signals)
                .WithOne(p => p.Device)
                .IsRequired();

            builder.Entity<Device>()
                .HasMany(d => d.flying_states)
                .WithOne(p => p.Device)
                .IsRequired();

            builder.Entity<Device>()
                .HasMany(d => d.drone_replies)
                .WithOne(p => p.Device)
                .IsRequired();

            builder.Entity<Device>()
                .HasMany(d => d.drone_video_states)
                .WithOne(p => p.Device)
                .IsRequired();
        }
    }
}