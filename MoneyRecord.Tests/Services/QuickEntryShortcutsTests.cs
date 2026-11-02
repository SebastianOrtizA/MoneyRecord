using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services
{
    public class QuickEntryShortcutsTests
    {
        private static QuickEntryItem Item(int id, long cents = 350, int uses = 0, int order = 0, string category = "Food") =>
            new(new QuickEntry { Id = id, Name = $"Q{id}", AmountCents = cents, UseCount = uses, SortOrder = order }, category, "F0770",
                "Cash", "USD");

        [Fact]
        public void The_most_used_come_first_and_the_users_order_breaks_ties()
        {
            var top = QuickEntryShortcuts.Top([Item(1, uses: 1, order: 0), Item(2, uses: 5, order: 1), Item(3, uses: 1, order: 2)], 3);

            top.Select(i => i.Id).Should().Equal(2, 1, 3);
        }

        [Fact]
        public void Entries_that_ask_the_amount_or_are_broken_are_left_out()
        {
            var top = QuickEntryShortcuts.Top([Item(1, cents: 0), Item(2, category: ""), Item(3)], 3);

            top.Select(i => i.Id).Should().Equal(3);
        }

        [Fact]
        public void No_more_than_asked_for_and_nothing_for_no_room()
        {
            var items = Enumerable.Range(1, 5).Select(i => Item(i)).ToList();

            QuickEntryShortcuts.Top(items, 2).Should().HaveCount(2);
            QuickEntryShortcuts.Top(items, 0).Should().BeEmpty();
        }
    }
}
