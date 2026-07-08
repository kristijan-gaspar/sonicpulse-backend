namespace SonicPulse.Domain.ValueObjects;

public readonly record struct DeviceId
{
    public Guid Value { get; }

    private DeviceId(Guid value) => Value = value;

    public static DeviceId New() => new(Guid.NewGuid());

    public static DeviceId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("DeviceId must not be an empty Guid.", nameof(value));
        return new DeviceId(value);
    }
}
