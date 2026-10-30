using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class SwipeHintSettings(IPreferencesService preferences) : ISwipeHintSettings
    {
        public const int MaxVisits = 3;

        private const string SeenKey = "Hints.SwipeSeen";
        private const string VisitsKey = "Hints.SwipeVisits";

        public bool ShouldShow => !preferences.Get(SeenKey, false) && preferences.Get(VisitsKey, 0) < MaxVisits;

        public void RecordVisit() => preferences.Set(VisitsKey, preferences.Get(VisitsKey, 0) + 1);

        public void MarkSeen() => preferences.Set(SeenKey, true);
    }
}
