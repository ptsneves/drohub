﻿// <auto-generated />
using System;
using DroHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DroHub.Data.Migrations
{
    [DbContext(typeof(DroHubContext))]
    [Migration("20200918114408_AddGimbalState")]
    partial class AddGimbalState
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("CameraState", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<long>("ConnectionId")
                        .HasColumnType("bigint");

                    b.Property<double>("MaxZoom")
                        .HasColumnType("double");

                    b.Property<double>("MinZoom")
                        .HasColumnType("double");

                    b.Property<int>("Mode")
                        .HasColumnType("int");

                    b.Property<string>("Serial")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<long>("Timestamp")
                        .HasColumnType("bigint");

                    b.Property<double>("ZoomLevel")
                        .HasColumnType("double");

                    b.HasKey("Id");

                    b.HasIndex("ConnectionId");

                    b.ToTable("CameraStates");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.Device", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Apperture")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<DateTime>("CreationDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("FocusMode")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("ISO")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("SerialNumber")
                        .IsRequired()
                        .HasColumnType("varchar(256) CHARACTER SET utf8mb4")
                        .HasMaxLength(256);

                    b.Property<string>("SubscriptionOrganizationName")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.HasKey("Id");

                    b.HasIndex("SerialNumber")
                        .IsUnique();

                    b.HasIndex("SubscriptionOrganizationName");

                    b.ToTable("Devices");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.DeviceConnection", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    b.Property<int>("DeviceId")
                        .HasColumnType("int");

                    b.Property<DateTime>("EndTime")
                        .HasColumnType("datetime(6)");

                    b.Property<DateTime>("StartTime")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("SubscriptionOrganizationName")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.HasKey("Id");

                    b.HasIndex("DeviceId");

                    b.HasIndex("SubscriptionOrganizationName");

                    b.ToTable("DeviceConnections");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.LogEntry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("EventId")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Exception")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Level")
                        .IsRequired()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("SourceContext")
                        .IsRequired()
                        .HasColumnType("varchar(100) CHARACTER SET utf8mb4")
                        .HasMaxLength(100);

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.ToTable("Logs");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.MediaObject", b =>
                {
                    b.Property<string>("MediaPath")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<DateTime>("CaptureDateTimeUTC")
                        .HasColumnType("datetime(6)");

                    b.Property<long>("DeviceConnectionId")
                        .HasColumnType("bigint");

                    b.Property<string>("SubscriptionOrganizationName")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.HasKey("MediaPath");

                    b.HasIndex("DeviceConnectionId");

                    b.HasIndex("SubscriptionOrganizationName");

                    b.ToTable("MediaObjects");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.MediaObjectTag", b =>
                {
                    b.Property<string>("TagName")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<string>("MediaPath")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<string>("SubscriptionOrganizationName")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<DateTime?>("Timestamp")
                        .HasColumnType("datetime(6)");

                    b.HasKey("TagName", "MediaPath");

                    b.HasIndex("MediaPath");

                    b.HasIndex("SubscriptionOrganizationName");

                    b.ToTable("MediaObjectTags");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.Subscription", b =>
                {
                    b.Property<string>("OrganizationName")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<long>("AllowedFlightTime")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint");

                    b.Property<int>("AllowedUserCount")
                        .HasColumnType("int");

                    b.Property<string>("BillingPlanName")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.HasKey("OrganizationName");

                    b.ToTable("Subscriptions");
                });

            modelBuilder.Entity("DroHub.Areas.Identity.Data.DroHubUser", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<string>("BaseActingType")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<DateTime>("CreationDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Email")
                        .HasColumnType("varchar(256) CHARACTER SET utf8mb4")
                        .HasMaxLength(256);

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTime>("LastLogin")
                        .HasColumnType("datetime(6)");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("NormalizedEmail")
                        .HasColumnType("varchar(256) CHARACTER SET utf8mb4")
                        .HasMaxLength(256);

                    b.Property<string>("NormalizedUserName")
                        .HasColumnType("varchar(256) CHARACTER SET utf8mb4")
                        .HasMaxLength(256);

                    b.Property<string>("PasswordHash")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("SubscriptionOrganizationName")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("UserName")
                        .HasColumnType("varchar(256) CHARACTER SET utf8mb4")
                        .HasMaxLength(256);

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasName("UserNameIndex");

                    b.HasIndex("SubscriptionOrganizationName");

                    b.ToTable("AspNetUsers");
                });

            modelBuilder.Entity("DroneBatteryLevel", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<double>("BatteryLevelPercent")
                        .HasColumnType("double");

                    b.Property<long>("ConnectionId")
                        .HasColumnType("bigint");

                    b.Property<string>("Serial")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<long>("Timestamp")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ConnectionId");

                    b.HasIndex("Serial");

                    b.ToTable("DroneBatteryLevels");
                });

            modelBuilder.Entity("DroneFlyingState", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<long>("ConnectionId")
                        .HasColumnType("bigint");

                    b.Property<string>("Serial")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<int>("State")
                        .HasColumnType("int");

                    b.Property<long>("Timestamp")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ConnectionId");

                    b.HasIndex("Serial");

                    b.ToTable("DroneFlyingStates");
                });

            modelBuilder.Entity("DroneLiveVideoStateResult", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<long>("ConnectionId")
                        .HasColumnType("bigint");

                    b.Property<string>("Serial")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<int>("State")
                        .HasColumnType("int");

                    b.Property<long>("Timestamp")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ConnectionId");

                    b.HasIndex("Serial");

                    b.ToTable("DroneVideoStatesResults");
                });

            modelBuilder.Entity("DronePosition", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<double>("Altitude")
                        .HasColumnType("double");

                    b.Property<long>("ConnectionId")
                        .HasColumnType("bigint");

                    b.Property<double>("Latitude")
                        .HasColumnType("double");

                    b.Property<double>("Longitude")
                        .HasColumnType("double");

                    b.Property<string>("Serial")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<long>("Timestamp")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ConnectionId");

                    b.HasIndex("Serial");

                    b.ToTable("Positions");
                });

            modelBuilder.Entity("DroneRadioSignal", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<long>("ConnectionId")
                        .HasColumnType("bigint");

                    b.Property<double>("Rssi")
                        .HasColumnType("double");

                    b.Property<string>("Serial")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<double>("SignalQuality")
                        .HasColumnType("double");

                    b.Property<long>("Timestamp")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ConnectionId");

                    b.HasIndex("Serial");

                    b.ToTable("DroneRadioSignals");
                });

            modelBuilder.Entity("DroneReply", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("ActionName")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<long>("ConnectionId")
                        .HasColumnType("bigint");

                    b.Property<bool>("Result")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Serial")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<long>("Timestamp")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("ConnectionId");

                    b.HasIndex("Serial");

                    b.ToTable("DroneReplies");
                });

            modelBuilder.Entity("GimbalState", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("CalibrationState")
                        .HasColumnType("int");

                    b.Property<long>("ConnectionId")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsPitchStabilized")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsRollStastabilized")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsYawStabilized")
                        .HasColumnType("tinyint(1)");

                    b.Property<double>("MaxPitch")
                        .HasColumnType("double");

                    b.Property<double>("MaxRoll")
                        .HasColumnType("double");

                    b.Property<double>("MaxYaw")
                        .HasColumnType("double");

                    b.Property<double>("MinPitch")
                        .HasColumnType("double");

                    b.Property<double>("MinRoll")
                        .HasColumnType("double");

                    b.Property<double>("MinYaw")
                        .HasColumnType("double");

                    b.Property<double>("Pitch")
                        .HasColumnType("double");

                    b.Property<double>("Roll")
                        .HasColumnType("double");

                    b.Property<string>("Serial")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<long>("Timestamp")
                        .HasColumnType("bigint");

                    b.Property<double>("Yaw")
                        .HasColumnType("double");

                    b.HasKey("Id");

                    b.HasIndex("ConnectionId");

                    b.HasIndex("Serial");

                    b.ToTable("GimbalStates");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("Name")
                        .HasColumnType("varchar(256) CHARACTER SET utf8mb4")
                        .HasMaxLength(256);

                    b.Property<string>("NormalizedName")
                        .HasColumnType("varchar(256) CHARACTER SET utf8mb4")
                        .HasMaxLength(256);

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasName("RoleNameIndex");

                    b.ToTable("AspNetRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("ClaimType")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("RoleId")
                        .IsRequired()
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("ClaimType")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasColumnType("varchar(128) CHARACTER SET utf8mb4")
                        .HasMaxLength(128);

                    b.Property<string>("ProviderKey")
                        .HasColumnType("varchar(128) CHARACTER SET utf8mb4")
                        .HasMaxLength(128);

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<string>("RoleId")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("varchar(255) CHARACTER SET utf8mb4");

                    b.Property<string>("LoginProvider")
                        .HasColumnType("varchar(128) CHARACTER SET utf8mb4")
                        .HasMaxLength(128);

                    b.Property<string>("Name")
                        .HasColumnType("varchar(128) CHARACTER SET utf8mb4")
                        .HasMaxLength(128);

                    b.Property<string>("Value")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens");
                });

            modelBuilder.Entity("CameraState", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "Connection")
                        .WithMany("camera_states")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.Device", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.Subscription", "Subscription")
                        .WithMany("Devices")
                        .HasForeignKey("SubscriptionOrganizationName");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.DeviceConnection", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.Device", "Device")
                        .WithMany("DeviceConnections")
                        .HasForeignKey("DeviceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DroHub.Areas.DHub.Models.Subscription", "Subscription")
                        .WithMany("DeviceConnections")
                        .HasForeignKey("SubscriptionOrganizationName");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.MediaObject", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "DeviceConnection")
                        .WithMany("MediaObjects")
                        .HasForeignKey("DeviceConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DroHub.Areas.DHub.Models.Subscription", "Subscription")
                        .WithMany()
                        .HasForeignKey("SubscriptionOrganizationName");
                });

            modelBuilder.Entity("DroHub.Areas.DHub.Models.MediaObjectTag", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.MediaObject", "MediaObject")
                        .WithMany("MediaObjectTags")
                        .HasForeignKey("MediaPath")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DroHub.Areas.DHub.Models.Subscription", "Subscription")
                        .WithMany("MediaObjectTags")
                        .HasForeignKey("SubscriptionOrganizationName");
                });

            modelBuilder.Entity("DroHub.Areas.Identity.Data.DroHubUser", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.Subscription", "Subscription")
                        .WithMany("Users")
                        .HasForeignKey("SubscriptionOrganizationName");
                });

            modelBuilder.Entity("DroneBatteryLevel", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "Connection")
                        .WithMany("battery_levels")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("DroneFlyingState", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "Connection")
                        .WithMany("flying_states")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("DroneLiveVideoStateResult", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "Connection")
                        .WithMany("drone_video_states")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("DronePosition", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "Connection")
                        .WithMany("positions")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("DroneRadioSignal", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "Connection")
                        .WithMany("radio_signals")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("DroneReply", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "Connection")
                        .WithMany("drone_replies")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("GimbalState", b =>
                {
                    b.HasOne("DroHub.Areas.DHub.Models.DeviceConnection", "Connection")
                        .WithMany("gimbal_states")
                        .HasForeignKey("ConnectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("DroHub.Areas.Identity.Data.DroHubUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("DroHub.Areas.Identity.Data.DroHubUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DroHub.Areas.Identity.Data.DroHubUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("DroHub.Areas.Identity.Data.DroHubUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
