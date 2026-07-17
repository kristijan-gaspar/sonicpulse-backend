using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonicPulse.Domain.Entities;

namespace SonicPulse.Infrastructure.Configurations;

public sealed class HotspotConfiguration : IEntityTypeConfiguration<Hotspot>
{
    public void Configure(EntityTypeBuilder<Hotspot> builder)
    {
        builder.ToTable("hotspots");
        builder.HasKey(x => x.Id);

        builder.ComplexProperty(x => x.Centroid, centroid =>
        {
            centroid.Property(c => c.Latitude).HasColumnName("centroid_latitude");
            centroid.Property(c => c.Longitude).HasColumnName("centroid_longitude");
        });

        builder.Property(x => x.RadiusMeters).HasColumnName("radius_meters");
        builder.Property(x => x.Confidence).HasColumnName("confidence");
        builder.Property(x => x.DeviceCount).HasColumnName("device_count");
        builder.Property(x => x.FirstReceivedAtUtc).HasColumnName("first_received_at_utc");
        builder.Property(x => x.LastReceivedAtUtc).HasColumnName("last_received_at_utc");

        builder.HasIndex(x => x.LastReceivedAtUtc);
    }
}
