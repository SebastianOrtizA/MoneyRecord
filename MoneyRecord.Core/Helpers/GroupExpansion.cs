using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Which groups of a list are open, kept across reloads (groups are rebuilt on every edit, filter or search). Groups
    /// start closed, or open while a search or filter is on so the matches show; only the groups the user flipped from
    /// that default are remembered, by <see cref="TransactionGroup.Key"/>.
    /// </summary>
    public class GroupExpansion
    {
        private readonly HashSet<string> _flipped = [];
        private bool _openByDefault;

        /// <summary>Sets each group's state; a change of default (a filter turned on or off) forgets the flips.</summary>
        public void Apply(IEnumerable<TransactionGroup> groups, bool openByDefault)
        {
            if (openByDefault != _openByDefault)
            {
                _openByDefault = openByDefault;
                _flipped.Clear();
            }

            foreach (var group in groups)
                group.IsExpanded = _openByDefault ^ _flipped.Contains(group.Key);
        }

        /// <summary>Opens or closes one group and remembers it.</summary>
        public void Toggle(TransactionGroup group)
        {
            group.ToggleExpanded();
            Remember(group);
        }

        /// <summary>Opens or closes every group.</summary>
        public void SetAll(IEnumerable<TransactionGroup> groups, bool open)
        {
            foreach (var group in groups)
            {
                group.IsExpanded = open;
                Remember(group);
            }
        }

        /// <summary>Forgets the flips (another grouping or period: the keys no longer match).</summary>
        public void Reset() => _flipped.Clear();

        public static bool AnyCollapsed(IEnumerable<TransactionGroup> groups) => groups.Any(g => !g.IsExpanded);

        private void Remember(TransactionGroup group)
        {
            if (group.IsExpanded != _openByDefault)
                _flipped.Add(group.Key);
            else
                _flipped.Remove(group.Key);
        }
    }
}
