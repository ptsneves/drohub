using DroHub.Areas.Identity.Data;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
        public DbSet<DroneLiveVideoStateResult> DroneVideoStatesResults { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {

            base.OnModelCreating(builder);

            builder.Entity<Device>(entity => entity.Property(m => m.SerialNumber).HasMaxLength(256));
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

            builder.Entity<DroneLiveVideoStateResult>()
                .HasIndex(d => d.Serial);

            builder.Entity<LogEntry>()
                .HasKey(d => d.Id);

            // ----- REQUIRED ATTRIBUTES ----------------------------
            builder.Entity<Device>()
                .Property(d => d.SerialNumber)
                .IsRequired();

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

            builder.Entity<DroneLiveVideoStateResult>()
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

            builder.Entity<UserDevice>()
                .HasKey(ud => new { ud.DeviceId, ud.DroHubUserId });

            builder.Entity<UserDevice>()
                .HasOne<Device>(ud => ud.Device)
                .WithMany(d => d.UserDevices)
                .HasForeignKey(ud => ud.DeviceId);

            builder.Entity<UserDevice>()
                .HasOne<DroHubUser>(ud => ud.DroHubUser)
                .WithMany(u => u.UserDevices)
                .HasForeignKey(ud => ud.DroHubUserId);
        }
    }
}