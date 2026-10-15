using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Daily rates from the Frankfurter API v2 (frankfurter.dev): no key, no quota, blended rates free to use. Only
    /// currency codes and dates are sent.
    /// </summary>
    public sealed class FrankfurterRateSource(HttpClient http) : IExchangeRateSource
    {
        public static readonly Uri BaseAddress = new("https://api.frankfurter.dev/");

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public async Task<List<ExchangeRate>> GetRatesAsync(string baseCode, string quoteCode, DateTime from, DateTime to,
            CancellationToken cancellationToken = default)
        {
            var url = $"v2/rates?base={Uri.EscapeDataString(baseCode)}&quotes={Uri.EscapeDataString(quoteCode)}" +
                      $"&from={Day(from)}&to={Day(to)}";
            using var response = await http.GetAsync(url, cancellationToken);
            // 404 / 422: an unknown currency ("invalid currency: ABC")
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
                throw new ExchangeRateUnavailableException(quoteCode);
            response.EnsureSuccessStatusCode();

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var rows = await JsonSerializer.DeserializeAsync<List<Row>>(body, JsonOptions, cancellationToken) ?? [];
            return rows
                .Where(r => r.Rate > 0 && !string.IsNullOrEmpty(r.Base) && !string.IsNullOrEmpty(r.Quote))
                .Select(r => new ExchangeRate
                {
                    Date = DateTime.ParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    BaseCode = r.Base.ToUpperInvariant(),
                    QuoteCode = r.Quote.ToUpperInvariant(),
                    Rate = r.Rate
                })
                .ToList();
        }

        private static string Day(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>One row: {"date":"2026-10-01","base":"USD","quote":"COP","rate":3317.65}.</summary>
        private sealed class Row
        {
            [JsonPropertyName("date")]
            public string Date { get; set; } = string.Empty;

            [JsonPropertyName("base")]
            public string Base { get; set; } = string.Empty;

            [JsonPropertyName("quote")]
            public string Quote { get; set; } = string.Empty;

            [JsonPropertyName("rate")]
            public double Rate { get; set; }
        }
    }
}
