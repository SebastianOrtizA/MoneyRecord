using AwesomeAssertions;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// PRIVACY.md promises the app cannot read other apps' notifications or the user's SMS: statements only come in as files the
/// user picks. The manifest must never declare what would break that promise.
/// </summary>
public class AndroidManifestPrivacyTests
{
    [Theory]
    [InlineData("BIND_NOTIFICATION_LISTENER_SERVICE")]
    [InlineData("READ_SMS")]
    [InlineData("RECEIVE_SMS")]
    [InlineData("RECEIVE_MMS")]
    [InlineData("RECEIVE_WAP_PUSH")]
    [InlineData("NotificationListenerService")]
    public void The_manifest_declares_no_notification_or_sms_access(string forbidden)
    {
        var manifest = File.ReadAllText(TestRepo.PathOf("MoneyRecord", "Platforms", "Android", "AndroidManifest.xml"));

        manifest.Should().NotContain(forbidden);
    }
}
