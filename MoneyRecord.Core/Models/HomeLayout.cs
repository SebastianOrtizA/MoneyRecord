namespace MoneyRecord.Models
{
    /// <summary>
    /// Which Home cards show and in what order, saved as one string ("Balance,Recent,...;hidden=Goals,Trip").
    /// Unknown names are dropped; cards a saved layout lacks (added by a later version) are appended with their default
    /// visibility; Balance, the app's main figure, is never hidden.
    /// </summary>
    public sealed record HomeLayout(IReadOnlyList<HomeCard> Order, IReadOnlySet<HomeCard> Hidden)
    {
        private const string HiddenMarker = ";hidden=";

        /// <summary>Every card in its declared order, all shown.</summary>
        public static HomeLayout Default => new(Enum.GetValues<HomeCard>(), new HashSet<HomeCard>());

        public bool IsShown(HomeCard card) => !Hidden.Contains(card);

        public string Serialize() =>
            string.Join(',', Order) + (Hidden.Count == 0 ? string.Empty : HiddenMarker + string.Join(',', Order.Where(Hidden.Contains)));

        public static HomeLayout Parse(string? text)
        {
            var parts = (text ?? string.Empty).Split(HiddenMarker);
            var order = ParseCards(parts[0]);
            var hidden = parts.Length > 1 ? ParseCards(parts[1]).ToHashSet() : [];
            hidden.Remove(HomeCard.Balance);

            // Cards this saved layout predates: at the end, shown
            order.AddRange(Enum.GetValues<HomeCard>().Where(c => !order.Contains(c)));
            return new HomeLayout(order, hidden);
        }

        private static List<HomeCard> ParseCards(string text)
        {
            var cards = new List<HomeCard>();
            foreach (var name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<HomeCard>(name, out var card) && Enum.IsDefined(card) && !cards.Contains(card))
                    cards.Add(card);
            }
            return cards;
        }
    }
}
