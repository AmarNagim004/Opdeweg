using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using Opdeweg.Domain.Entities;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.AvatarUrl).HasMaxLength(User.AvatarUrlMaxLength);
        builder.Property(u => u.ShareDisplayName).HasDefaultValue(true);
        builder.HasMany(u => u.Logins).WithOne(l => l.User).HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        builder.ToTable("user_logins");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Provider).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.ProviderKey).HasMaxLength(320).IsRequired();
        builder.Property(l => l.PasswordHash).HasMaxLength(512);
        builder.HasIndex(l => new { l.Provider, l.ProviderKey }).IsUnique();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DrivingSessionConfiguration : IEntityTypeConfiguration<DrivingSession>
{
    public void Configure(EntityTypeBuilder<DrivingSession> builder)
    {
        builder.ToTable("driving_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Handle).HasMaxLength(32).IsRequired();
        builder.HasIndex(s => s.Handle).IsUnique();
        builder.Property(s => s.EndReason).HasConversion<string>().HasMaxLength(32);
        builder.Ignore(s => s.IsActive);

        // PostGIS geography point; only ever a grid-snapped cell, never a precise fix.
        builder.Property(s => s.CoarseStartArea)
            .HasConversion(new GeoPointToPointConverter())
            .HasColumnType("geography (point, 4326)");
        builder.HasIndex(s => s.CoarseStartArea).HasMethod("gist");

        // At most one active session per user, enforced by the database.
        builder.HasIndex(s => s.UserId).IsUnique().HasFilter("ended_at IS NULL").HasDatabaseName("ix_driving_sessions_user_id_active");
        builder.HasIndex(s => s.StartedAt);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GeoPointToPointConverter() : ValueConverter<GeoPoint, Point>(
    p => new Point(p.Longitude, p.Latitude) { SRID = 4326 },
    p => new GeoPoint(p.Y, p.X));
