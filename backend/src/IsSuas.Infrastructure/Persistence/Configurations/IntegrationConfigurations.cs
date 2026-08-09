using IsSuas.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationConnectorConfiguration : IEntityTypeConfiguration<IntegrationConnector>
{
    public void Configure(EntityTypeBuilder<IntegrationConnector> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameSk).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ConnectorType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EndpointUrl).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ScheduleCron).HasMaxLength(128);
        builder.Property(x => x.SecretRef).HasMaxLength(256);
        builder.Property(x => x.LastRunResult).HasMaxLength(500);
        builder.Property(x => x.LastError).HasMaxLength(500);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
