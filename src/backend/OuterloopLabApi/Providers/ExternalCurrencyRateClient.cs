using System.Text.Json;
using OuterloopLabApi.Exceptions;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Providers;

public sealed class ExternalCurrencyRateClient
{
    private readonly HttpClient _http;

    public ExternalCurrencyRateClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<CurrencyRateQuote> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        var baseUrl = Environment.GetEnvironmentVariable("CURRENCY_API_BASE_URL") ?? "https://frankfurter.dev";
        var candidates = BuildCandidateUrls(baseUrl, fromCurrency, toCurrency);

        Exception? lastError = null;
        foreach (var url in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _http.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // Try next candidate (best-effort URL normalization for Frankfurter).
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                        continue;

                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new CurrencyRateProviderException($"Currency rate provider failed: {(int)response.StatusCode} {body}");
                }

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                try
                {
                    return ParseQuote(payload, toCurrency);
                }
                catch (JsonException je)
                {
                    throw new CurrencyRateProviderException($"Currency rate provider returned invalid JSON: {je.Message}");
                }
            }

            catch (CurrencyRateProviderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                // Best-effort: try next candidate.
            }
        }

        throw new CurrencyRateProviderException($"Currency rate provider returned no usable result. Last error: {lastError?.Message}");
    }

    private static List<string> BuildCandidateUrls(string baseUrl, string fromCurrency, string toCurrency)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        var candidates = new List<string>
        {
            $"{normalizedBase}/v2/rate/{fromCurrency}/{toCurrency}"
        };

        // Frankfurter's public API lives under api.frankfurter.dev.
        if (normalizedBase.Equals("https://frankfurter.dev", StringComparison.OrdinalIgnoreCase))
            candidates.Add($"https://api.frankfurter.dev/v2/rate/{fromCurrency}/{toCurrency}");

        return candidates;
    }

    private static CurrencyRateQuote ParseQuote(string json, string toCurrency)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        decimal? rate = TryExtractDecimal(root, toCurrency, new[] { "rate", "conversion_rate", "conversionRate" });
        if (rate is null)
            rate = TryExtractDecimalFromMap(root, toCurrency, new[] { "rates", "conversion_rates", "conversionRates" });

        if (rate is null)
            throw new CurrencyRateProviderException("Provider response did not include a recognizable rate value.");

        var providerDate = TryExtractString(root, new[] { "date", "timestamp", "rateDate", "providerDate" });

        return new CurrencyRateQuote(rate.Value, providerDate);
    }

    private static decimal? TryExtractDecimal(JsonElement root, string toCurrency, IEnumerable<string> propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (root.TryGetProperty(name, out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d)) return d;
                if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out var d2)) return d2;
            }
        }

        // Some providers may respond with a single-key object like { "USD": 1.1 }
        // but those are handled by map extraction.
        return null;
    }

    private static decimal? TryExtractDecimalFromMap(JsonElement root, string toCurrency, IEnumerable<string> mapNames)
    {
        foreach (var mapName in mapNames)
        {
            if (!root.TryGetProperty(mapName, out var mapEl))
                continue;
            if (mapEl.ValueKind != JsonValueKind.Object)
                continue;
            if (mapEl.TryGetProperty(toCurrency, out var rateEl))
            {
                if (rateEl.ValueKind == JsonValueKind.Number && rateEl.TryGetDecimal(out var d)) return d;
                if (rateEl.ValueKind == JsonValueKind.String && decimal.TryParse(rateEl.GetString(), out var d2)) return d2;
            }
        }

        return null;
    }

    private static string? TryExtractString(JsonElement root, IEnumerable<string> propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (root.TryGetProperty(name, out var el))
            {
                if (el.ValueKind == JsonValueKind.String) return el.GetString();
                if (el.ValueKind == JsonValueKind.Number) return el.GetRawText();
            }
        }
        return null;
    }
}
