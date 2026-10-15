using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class CrashReportConsentTests
{
    private readonly FakePreferences _preferences = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));

    private CrashReportConsent Consent(bool isAvailable = true) => new(_preferences, _clock, isAvailable);

    [Fact]
    public void Reports_are_off_until_the_user_agrees()
    {
        Consent().Enabled.Should().BeFalse();
    }

    [Fact]
    public void The_question_comes_once_a_week_after_first_use()
    {
        var consent = Consent();

        consent.ShouldAskNow().Should().BeFalse("the first call starts the week");
        _clock.Advance(TimeSpan.FromDays(CrashReportConsent.AskAfterDays) - TimeSpan.FromMinutes(1));
        consent.ShouldAskNow().Should().BeFalse();
        _clock.Advance(TimeSpan.FromMinutes(1));
        consent.ShouldAskNow().Should().BeTrue();

        consent.Enabled = false;

        consent.ShouldAskNow().Should().BeFalse();
        consent.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Agreeing_enables_reports_and_is_remembered()
    {
        Consent().Enabled = true;

        var later = Consent();
        later.Enabled.Should().BeTrue();
        later.ShouldAskNow().Should().BeFalse();
    }

    [Fact]
    public void A_choice_in_Settings_means_the_question_never_comes()
    {
        var consent = Consent();
        consent.ShouldAskNow();

        consent.Enabled = true;
        consent.Enabled = false;
        _clock.Advance(TimeSpan.FromDays(30));

        consent.ShouldAskNow().Should().BeFalse();
        consent.Enabled.Should().BeFalse();
    }

    [Fact]
    public void A_build_without_a_DSN_never_asks_and_never_sends()
    {
        var consent = Consent(isAvailable: false);
        consent.ShouldAskNow();
        _clock.Advance(TimeSpan.FromDays(30));

        consent.ShouldAskNow().Should().BeFalse();

        // Even with a choice left over from a build that could send
        _preferences.Set("CrashReports.Enabled", true);
        consent.Enabled.Should().BeFalse();
    }
}
