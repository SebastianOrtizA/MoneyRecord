namespace MoneyRecord.Helpers
{
    /// <summary>Labels for a pick list that answers with the chosen text (an action sheet).</summary>
    public static class PickList
    {
        /// <summary>Repeated names get a number ("Other", "Other (2)"), so each label maps back to one option.</summary>
        public static List<string> UniqueLabels(IEnumerable<string> names)
        {
            var labels = new List<string>();
            foreach (var name in names)
            {
                var label = name;
                for (var n = 2; labels.Contains(label); n++)
                    label = $"{name} ({n})";
                labels.Add(label);
            }

            return labels;
        }
    }
}
