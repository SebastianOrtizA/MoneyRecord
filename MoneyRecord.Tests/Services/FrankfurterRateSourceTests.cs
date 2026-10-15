using AwesomeAssertions;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using System.Net;
using System.Text;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>The Frankfurter v2 request and response (as checked against the live API on 2026-10-15), with no network.</summary>
public class FrankfurterRateSourceTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? Requested { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static (FrankfurterRateSource Source, StubHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        return (new FrankfurterRateSource(new HttpClient(handler) { BaseAddress = FrankfurterRateSource.BaseAddress }), handler);
    }

    [Fact]
    public async Task Asks_for_the_pair_and_days_and_reads_one_rate_per_row()
    {
        var (source, handler) = Create(HttpStatusCode.OK,
            """[{"date":"2026-10-01","base":"USD","quote":"COP","rate":3317.65},{"date":"2026-10-02","base":"USD","quote":"COP","rate":3307.69}]""");

        var rates = await source.GetRatesAsync("USD", "COP", new DateTime(2026, 10, 1), new DateTime(2026, 10, 2));

        handler.Requested!.ToString().Should().Be("https://api.frankfurter.dev/v2/rates?base=USD&quotes=COP&from=2026-10-01&to=2026-10-02");
        rates.Select(r => (r.Date, r.BaseCode, r.QuoteCode, r.Rate)).Should().Equal(
            (new DateTime(2026, 10, 1), "USD", "COP", 3317.65), (new DateTime(2026, 10, 2), "USD", "COP", 3307.69));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task An_unknown_currency_is_unavailable(HttpStatusCode status)
    {
        var (source, _) = Create(status, """{"status":422,"message":"invalid currency: XTS"}""");

        var act = () => source.GetRatesAsync("USD", "XTS", DateTime.Today, DateTime.Today);

        (await act.Should().ThrowAsync<ExchangeRateUnavailableException>()).Which.CurrencyCode.Should().Be("XTS");
    }

    [Fact]
    public async Task A_server_error_is_an_http_failure()
    {
        var (source, _) = Create(HttpStatusCode.ServiceUnavailable, "");

        var act = () => source.GetRatesAsync("USD", "COP", DateTime.Today, DateTime.Today);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
