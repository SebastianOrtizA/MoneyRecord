using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class FontScaleLayoutTests
{
    [Theory]
    [InlineData(0.85, 4)]
    [InlineData(1.0, 4)]
    [InlineData(1.1, 4)]
    [InlineData(1.15, 3)]
    [InlineData(1.3, 3)]
    [InlineData(1.49, 3)]
    [InlineData(1.5, 2)]
    [InlineData(2.0, 2)]
    public void Categories_get_fewer_cells_per_row_as_the_font_grows(double fontScale, int columns)
    {
        FontScaleLayout.CategoryColumns(fontScale).Should().Be(columns);
    }

    [Theory]
    [InlineData(1.0, false)]
    [InlineData(1.3, false)]
    [InlineData(1.5, true)]
    [InlineData(2.0, true)]
    public void The_period_tiles_stack_from_a_large_font(double fontScale, bool stacks)
    {
        FontScaleLayout.StacksPeriodTiles(fontScale).Should().Be(stacks);
    }
}
