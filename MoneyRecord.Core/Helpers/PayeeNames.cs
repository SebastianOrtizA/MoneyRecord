using System.Globalization;
using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>Payee names: how a typed name is cleaned up, when two names are the same payee, and the form's suggestions.</summary>
    public static class PayeeNames
    {
        private const CompareOptions Loose = ListSearch.Loose;

        /// <summary>Trimmed, inner runs of spaces collapsed to one; empty when nothing was typed.</summary>
        public static string Normalize(string? name) =>
            string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// The same payee: equal after <see cref="Normalize"/>, ignoring case ("éxito" = "Éxito", not "Exito"). Invariant, so
        /// two names compare the same way in every app language.
        /// </summary>
        public static bool Same(string? a, string? b) =>
            string.Equals(Normalize(a), Normalize(b), StringComparison.InvariantCultureIgnoreCase);

        /// <summary>The payee whose name is typed in full (<see cref="Same"/>), if there is one.</summary>
        public static PayeeSummary? Find(IEnumerable<PayeeSummary> payees, string? text) =>
            Normalize(text) is { Length: > 0 } typed ? payees.FirstOrDefault(p => Same(p.Name, typed)) : null;

        /// <summary>
        /// Payees matching what is typed (ignoring case and accents): names starting with it first, then a word starting with
        /// it, then containing it; the most used first within each. None for empty text or once the name is typed in full.
        /// </summary>
        public static List<PayeeSummary> Suggest(IEnumerable<PayeeSummary> payees, string? text, int max = 5)
        {
            var typed = Normalize(text);
            if (typed.Length == 0)
                return [];

            var all = payees.ToList();
            if (Find(all, typed) != null)
                return [];

            return all
                .Select(p => (Payee: p, Rank: Rank(p.Name, typed)))
                .Where(m => m.Rank >= 0)
                .OrderBy(m => m.Rank)
                .ThenByDescending(m => m.Payee.TransactionCount)
                .ThenBy(m => m.Payee.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(max)
                .Select(m => m.Payee)
                .ToList();
        }

        private static CompareInfo Compare => CultureInfo.CurrentCulture.CompareInfo;

        // 0 = the name starts with the text, 1 = one of its words does, 2 = it contains it, -1 = no match
        private static int Rank(string name, string typed)
        {
            if (Compare.IsPrefix(name, typed, Loose))
                return 0;
            if (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(word => Compare.IsPrefix(word, typed, Loose)))
                return 1;
            return Compare.IndexOf(name, typed, Loose) >= 0 ? 2 : -1;
        }
    }
}
