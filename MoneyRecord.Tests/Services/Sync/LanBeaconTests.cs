using System.Net;
using AwesomeAssertions;
using MoneyRecord.Services.Sync.Lan;
using Xunit;

namespace MoneyRecord.Tests.Services.Sync;

/// <summary>Stage 26.3a-3: finding the computer again by a UDP probe (over loopback, on a free port).</summary>
public sealed class LanBeaconTests
{
    private static IPEndPoint Target(LanBeacon beacon) => new(IPAddress.Loopback, beacon.Port);

    [Fact]
    public async Task ThePhone_GetsTheComputersPort_WhenItNamesTheComputersId()
    {
        await using var beacon = new LanBeacon("device-a", () => 48731);
        beacon.Start(0, IPAddress.Loopback).Should().BeTrue();

        var found = await LanBeacon.FindAsync("device-a", TimeSpan.FromSeconds(3), Target(beacon));

        found.Should().Be(("127.0.0.1", 48731));
    }

    [Fact]
    public async Task AProbeForAnotherDevice_GetsNoAnswer()
    {
        await using var beacon = new LanBeacon("device-a", () => 48731);
        beacon.Start(0, IPAddress.Loopback);

        var found = await LanBeacon.FindAsync("device-b", TimeSpan.FromMilliseconds(500), Target(beacon));

        found.Should().BeNull();
    }

    [Fact]
    public async Task NobodyListening_ReturnsNullAfterTheTimeout()
    {
        await using var silent = new LanBeacon("device-a", () => 1);
        silent.Start(0, IPAddress.Loopback);
        var port = silent.Port;
        await silent.DisposeAsync();

        var found = await LanBeacon.FindAsync("device-a", TimeSpan.FromMilliseconds(500), new IPEndPoint(IPAddress.Loopback, port));

        found.Should().BeNull();
    }
}
