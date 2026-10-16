using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class StatementMappingStoreTests
{
    // Two date columns and two amount columns: the guess picks "Fecha" and "Importe", the user may pick the others
    private const string Bank = "Fecha valor;Fecha;Descripción;Referencia;Importe;USD\n" +
                                "01/10/2026;30/09/2026;Compra;R1;-45.000,00;-11.50\n" +
                                "02/10/2026;02/10/2026;Nómina;R2;2.000.000,00;500.25\n";

    private readonly FakePreferences _preferences = new();
    private readonly StatementMappingStore _sut;

    public StatementMappingStoreTests() => _sut = new StatementMappingStore(_preferences);

    private static (CsvTable Table, StatementGuess Guess) Read(string csv)
    {
        var table = CsvTable.Parse(csv);
        return (table, StatementMapping.Guess(table)!);
    }

    [Fact]
    public void A_new_layout_has_nothing_saved()
    {
        var (table, guess) = Read(Bank);
        _sut.Find(table, guess).Should().BeNull();
    }

    [Fact]
    public void The_next_file_of_a_bank_gets_the_last_choices()
    {
        var (table, guess) = Read(Bank);
        var chosen = (guess.Mapping with { DateColumn = 0, DescriptionColumn = 3, PositiveIsExpense = true })
            .WithAmountColumn(table, 5);
        _sut.Remember(table, chosen, accountId: 7);

        // Next month's file: same header (case and accents aside), other rows
        var (next, nextGuess) = Read(Bank.Replace("Descripción", "DESCRIPCION").Replace("01/10/2026", "01/11/2026"));
        var saved = _sut.Find(next, nextGuess)!;

        saved.AccountId.Should().Be(7);
        saved.Mapping.Should().Be(chosen);
        saved.Mapping.DecimalSeparator.Should().Be('.', "the USD column's separator, read from the new file");
    }

    [Fact]
    public void Another_header_is_another_layout()
    {
        var (table, guess) = Read(Bank);
        _sut.Remember(table, guess.Mapping, accountId: 7);

        var (other, otherGuess) = Read(Bank.Replace("Referencia", "Oficina"));
        _sut.Find(other, otherGuess).Should().BeNull();
    }

    [Fact]
    public void Saved_choices_that_no_longer_fit_the_file_are_ignored()
    {
        var (table, guess) = Read(Bank);
        _sut.Remember(table, guess.Mapping.WithAmountColumn(table, 5), accountId: 7);

        // Same header, but the USD column is now text: the saved amount column isn't one any more
        var (next, nextGuess) = Read(Bank.Replace("-11.50", "n/a").Replace("500.25", "n/a"));
        _sut.Find(next, nextGuess).Should().BeNull();
    }

    [Fact]
    public void Files_without_a_header_are_matched_by_their_column_count()
    {
        var (table, guess) = Read("2026-10-01,Coffee,-3.50\n2026-10-02,Salary,1500.00\n");
        _sut.Remember(table, guess.Mapping with { PositiveIsExpense = true }, accountId: 3);

        var (next, nextGuess) = Read("2026-11-01,Bakery,-4.00\n2026-11-02,Rent,-900.00\n");
        _sut.Find(next, nextGuess)!.Mapping.PositiveIsExpense.Should().BeTrue();
    }

    [Fact]
    public void Only_the_most_recent_layouts_are_kept()
    {
        for (var i = 0; i <= StatementMappingStore.MaxLayouts; i++)
        {
            var (table, guess) = Read($"Date,Amount,Note{i}\n2026-10-01,-3.50,x\n");
            _sut.Remember(table, guess.Mapping, accountId: i);
        }

        var (first, firstGuess) = Read("Date,Amount,Note0\n2026-10-01,-3.50,x\n");
        _sut.Find(first, firstGuess).Should().BeNull("the least recently used layout was dropped");
        var (last, lastGuess) = Read($"Date,Amount,Note{StatementMappingStore.MaxLayouts}\n2026-10-01,-3.50,x\n");
        _sut.Find(last, lastGuess)!.AccountId.Should().Be(StatementMappingStore.MaxLayouts);
    }

    [Fact]
    public void Unreadable_preferences_start_over()
    {
        _preferences.Set(StatementMappingStore.PreferenceKey, "{not json");
        var (table, guess) = Read(Bank);

        _sut.Find(table, guess).Should().BeNull();
        _sut.Remember(table, guess.Mapping, accountId: 1);
        _sut.Find(table, guess)!.AccountId.Should().Be(1);
    }
}
