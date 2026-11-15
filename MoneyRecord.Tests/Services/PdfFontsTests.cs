using MoneyRecord.Resources.Strings;
using System.Globalization;
using AwesomeAssertions;
using MoneyRecord.Services.Pdf;
using Xunit;

namespace MoneyRecord.Tests.Services;

[Collection("Language")]
public class PdfFontsTests
{
    [Fact]
    public void The_character_map_is_read_from_the_embedded_font()
    {
        PdfFonts.CanShow('a').Should().BeTrue();
        PdfFonts.CanShow('ñ').Should().BeTrue();
        PdfFonts.CanShow('€').Should().BeTrue();
        PdfFonts.CanShow('▲').Should().BeFalse();
    }

    [Fact]
    public void A_script_the_font_lacks_gets_English_labels_while_the_pdf_is_built_and_is_restored_after()
    {
        var before = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture, AppResources.Culture);
        try
        {
            var hindi = new CultureInfo("hi-IN");
            CultureInfo.CurrentCulture = hindi;
            CultureInfo.CurrentUICulture = hindi;
            AppResources.Culture = hindi;

            using (PdfFonts.LatinScope())
            {
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Should().Be("en");
                CultureInfo.CurrentCulture.Name.Should().Be("en-IN");
                AppResources.Culture!.TwoLetterISOLanguageName.Should().Be("en");
            }

            CultureInfo.CurrentUICulture.Name.Should().Be("hi-IN");
            AppResources.Culture!.Name.Should().Be("hi-IN");

            // Latin and Cyrillic languages are drawn by the font itself: no change
            CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
            using (PdfFonts.LatinScope())
                CultureInfo.CurrentUICulture.Name.Should().Be("ru-RU");
        }
        finally
        {
            CultureInfo.CurrentCulture = before.CurrentCulture;
            CultureInfo.CurrentUICulture = before.CurrentUICulture;
            AppResources.Culture = before.Culture;
        }
    }

    [Theory]
    [InlineData("Plain text $1,200.00 · ñ", "Plain text $1,200.00 · ñ")]
    [InlineData("▲ 12% vs previous period", "+12% vs previous period")]
    [InlineData("▼ 5% vs previous period", "−5% vs previous period")]
    [InlineData("Bank → Wallet", "Bank › Wallet")]
    [InlineData("₹1,200.00", "INR\u00A01,200.00")]
    [InlineData("1.200,00 ₽", "1.200,00 RUB")]
    [InlineData("12\u202F000,00 €", "12\u00A0000,00 €")]
    [InlineData("Savings 🏦", "Savings ")]
    public void What_the_font_lacks_is_replaced_or_left_out(string text, string printable) =>
        PdfFonts.Printable(text).Should().Be(printable);
}
