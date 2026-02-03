using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.EF;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .HasConversion(
                name => name.Value,
                value => new Name(value))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Email)
            .HasConversion(
                email => email.Value,
                value => new Email(value))
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(s => s.Email)
            .IsUnique();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt);
    }
}
