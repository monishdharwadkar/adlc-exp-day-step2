namespace OuterloopLabApi.Exceptions;

public class CurrencyConversionException : Exception
{
    public CurrencyConversionException(string message) : base(message) { }
}

public sealed class CurrencyRateProviderException : CurrencyConversionException
{
    public CurrencyRateProviderException(string message) : base(message) { }
}
