using AwesomeAssertions;
using MoneyRecord.Helpers;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class MoneyFormatterTests
{
    [Fact]
    public void EnUs_Negative_PutsSignBeforeSymbol()
    {
        MoneyFormatter.Format(-50m, "USD", new CultureInfo("en-US")).Should().Be("-$50.00");
    }

    [Fact]
    public void EnUs_Positive_HasNoSign()
    {
        MoneyFormatter.Format(1250.5m, "USD", new CultureInfo("en-US")).Should().Be("$1,250.50");
    }

    [Fact]
    public void EsCo_UsesPesoSymbolAndLocalSeparators()
    {
        var text = MoneyFormatter.Format(-50000m, "COP", new CultureInfo("es-CO"));

        text.Should().StartWith("-$");
        text.Should().Contain("50.000");
        text.Should().NotContain("¤");
    }

    [Fact]
    public void EsEs_Negative_KeepsSymbolAfterAmount()
    {
        var text = MoneyFormatter.Format(-50m, "EUR", new CultureInfo("es-ES"));

        text.Should().StartWith("-50,00");
        text.Should().EndWith("€");
    }

    [Fact]
    public void JaJp_HasNoDecimals()
    {
        var text = MoneyFormatter.Format(-500m, "JPY", new CultureInfo("ja-JP"));

        text.Should().StartWith("-");
        text.Should().EndWith("500");
    }

    [Fact]
    public void NeutralCulture_NeverShowsGenericCurrencySign()
    {
        MoneyFormatter.Format(10m, culture: new CultureInfo("es")).Should().NotContain("¤");
    }
}
