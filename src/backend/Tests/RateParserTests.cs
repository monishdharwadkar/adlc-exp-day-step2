using OuterloopLabApi.Providers;
using System.Reflection;
using Xunit;

namespace Tests;

public class RateParserTests
{
    [Fact]
    public void ParseQuote_ExtractsRateAndDate_FromV2Shape()
    {
        var json = "{ \"date\": \"2026-01-02\", \"base\": \"EUR\", \"quote\": \"USD\", \"rate\": 1.2345 }";

        var method = typeof(ExternalCurrencyRateClient).GetMethod("ParseQuote", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var quote = method!.Invoke(null, new object[] { json, "USD" });
        Assert.NotNull(quote);

        var rateProp = quote!.GetType().GetProperty("Rate");
        var dateProp = quote!.GetType().GetProperty("ProviderDate");
        Assert.NotNull(rateProp);
        Assert.NotNull(dateProp);

        Assert.Equal(1.2345m, (decimal)rateProp!.GetValue(quote)!);
        Assert.Equal("2026-01-02", (string?)dateProp!.GetValue(quote));
    }
}
