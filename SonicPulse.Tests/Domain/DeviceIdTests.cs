using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Domain;

public class DeviceIdTests
{
    [Fact]
    public void From_EmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(() => DeviceId.From(Guid.Empty));
    }

    [Fact]
    public void From_ValidGuid_PreservesValue()
    {
        var guid = Guid.NewGuid();

        var deviceId = DeviceId.From(guid);

        Assert.Equal(guid, deviceId.Value);
    }

    [Fact]
    public void TwoDeviceIds_WithSameGuid_AreEqual()
    {
        var guid = Guid.NewGuid();

        var first = DeviceId.From(guid);
        var second = DeviceId.From(guid);

        Assert.Equal(first, second);
    }
}
