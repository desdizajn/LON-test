using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        
        builder.HasOne(u => u.Employee)
            .WithOne(e => e.User)
            .HasForeignKey<User>(u => u.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(r => r.Description)
            .HasMaxLength(500);
        
        builder.HasIndex(r => r.Name).IsUnique();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(p => p.Description)
            .HasMaxLength(500);
        
        builder.Property(p => p.Category)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.HasIndex(p => p.Name).IsUnique();
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        
        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        
        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(s => s.Description)
            .HasMaxLength(500);
        
        builder.HasIndex(s => s.Code).IsUnique();
    }
}
