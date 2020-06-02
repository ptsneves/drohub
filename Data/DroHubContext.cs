using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
        public DbSet<DeviceConnection> DeviceConnections { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {

            base.OnModelCreating(builder);

            builder.Entity<Device>(entity => entity.Property(m => m.SerialNumber).HasMaxLength(256));
            // ----- KEYS AND INDEXES -------------------------------
            builder.Entity<Device>()
                .HasIndex(d => d.SerialNumber)
                .IsUnique();

            builder.Entity<DeviceConnection>()
                .HasKey(d => d.Id);

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

            builder.Entity<Subscription>()
                .HasKey(d => d.OrganizationName);

            builder.Entity<Subscription>()
                .Property(s => s.AllowedFlightTime)
                .IsConcurrencyToken();

            builder.Entity<Subscription>()
                .Property(s => s.AllowedFlightTime)
                .HasColumnType("bigint")
                .HasConversion(new TimeSpanToTicksConverter());

            // ----- REQUIRED ATTRIBUTES ----------------------------
            builder.Entity<Device>()
                .Property(d => d.SerialNumber)
                .IsRequired();

            builder.Entity<DronePosition>()
                .HasOne(p => p.Connection)
                .WithMany(d => d.positions);

            builder.Entity<DroneBatteryLevel>()
                .HasOne(p => p.Connection)
                .WithMany(d => d.battery_levels);

            builder.Entity<DroneRadioSignal>()
                .HasOne(p => p.Connection)
                .WithMany(d => d.radio_signals);

            builder.Entity<DroneFlyingState>()
                .HasOne(p => p.Connection)
                .WithMany(d => d.flying_states);

            builder.Entity<DroneReply>()
                .HasOne(p => p.Connection)
                .WithMany(d => d.drone_replies);

            builder.Entity<DroneLiveVideoStateResult>()
                .HasOne(p => p.Connection)
                .WithMany(d => d.drone_video_states);

            builder.Entity<DeviceConnection>()
                .HasMany(d => d.positions)
                .WithOne(p => p.Connection);

            builder.Entity<DeviceConnection>()
                .HasMany(d => d.battery_levels)
                .WithOne(p => p.Connection);

            builder.Entity<DeviceConnection>()
                .HasMany(d => d.radio_signals)
                .WithOne(p => p.Connection);

            builder.Entity<DeviceConnection>()
                .HasMany(d => d.flying_states)
                .WithOne(p => p.Connection);

            builder.Entity<DeviceConnection>()
                .HasMany(d => d.drone_replies)
                .WithOne(p => p.Connection);

            builder.Entity<DeviceConnection>()
                .HasMany(d => d.drone_video_states)
                .WithOne(p => p.Connection);

            builder.Entity<DeviceConnection>()
                .HasOne(cd => cd.Device)
                .WithMany(d => d.DeviceConnections);

            builder.Entity<Subscription>()
                .HasMany(d => d.Users)
                .WithOne(u => u.Subscription);

            builder.Entity<Subscription>()
                .HasMany(d => d.Devices)
                .WithOne(u => u.Subscription);

            builder.Entity<Subscription>()
                .HasMany(s => s.DeviceConnections)
                .WithOne(dc => dc.Subscription);
        }
    }
}