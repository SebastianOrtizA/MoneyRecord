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

        /// <summary>Cards that start hidden (opt-in).</summary>
        private static readonly HomeCard[] HiddenByDefault = [HomeCard.Debts];

        /// <summary>Every card in its declared order, Get started and Quick entries under Balance; the opt-in ones hidden.</summary>
        public static HomeLayout Default => new(
            [HomeCard.Balance, HomeCard.GetStarted, HomeCard.QuickEntries,
             .. Enum.GetValues<HomeCard>().Where(c => c is not (HomeCard.Balance or HomeCard.GetStarted or HomeCard.QuickEntries))],
            HiddenByDefault.ToHashSet());

        public bool IsShown(HomeCard card) => !Hidden.Contains(card);

        public string Serialize() =>
            string.Join(',', Order) + (Hidden.Count == 0 ? string.Empty : HiddenMarker + string.Join(',', Order.Where(Hidden.Contains)));

        public static HomeLayout Parse(string? text)
        {
            var parts = (text ?? string.Empty).Split(HiddenMarker);
            var order = ParseCards(parts[0]);
            if (order.Count == 0 && parts.Length == 1)
                return Default;

            var hidden = parts.Length > 1 ? ParseCards(parts[1]).ToHashSet() : [];
            hidden.Remove(HomeCard.Balance);

            // Cards this saved layout predates: at the end, with their default visibility
            var added = Enum.GetValues<HomeCard>().Where(c => !order.Contains(c)).ToList();
            order.AddRange(added);
            hidden.UnionWith(added.Where(HiddenByDefault.Contains));
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
