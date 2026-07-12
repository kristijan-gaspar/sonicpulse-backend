using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Infrastructure.Configurations;

public sealed class DetectionConfiguration : IEntityTypeConfiguration<Detection>
{
    public void Configure(EntityTypeBuilder<Detection> builder)
    {
        builder.ToTable("detections");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SequenceNumber)
            .HasColumnName("sequence_number")
            .ValueGeneratedOnAdd();

        builder.HasIndex(x => x.SequenceNumber).IsUnique();

        builder.Property(x => x.DeviceId)
            .HasConversion(id => id.Value, value => DeviceId.From(value))
            .HasColumnName("device_id");

        builder.ComplexProperty(x => x.Location, location =>
        {
            location.Property(c => c.Latitude).HasColumnName("latitude");
            location.Property(c => c.Longitude).HasColumnName("longitude");
        });

        builder.Property(x => x.PeakDbfs).HasColumnName("peak_dbfs");
        builder.Property(x => x.GpsAccuracy).HasColumnName("gps_accuracy");
        builder.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc");
        builder.Property(x => x.PeakTimeClient).HasColumnName("peak_time_client");

        builder.Property(x => x.HotspotId).HasColumnName("hotspot_id");
        builder.HasOne<Hotspot>().WithMany()
            .HasForeignKey(x => x.HotspotId).IsRequired(false);

        builder.Property(x => x.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasConversion<string>();

        builder.Property<Point>("LocationPoint")
            .HasColumnName("location")
            .HasColumnType("geography(Point, 4326)");

        builder.HasIndex("LocationPoint").HasMethod("gist");

        builder.HasIndex(x => new { x.DeviceId, x.SequenceNumber });
        builder.HasIndex(x => x.ReceivedAtUtc);
    }
}
