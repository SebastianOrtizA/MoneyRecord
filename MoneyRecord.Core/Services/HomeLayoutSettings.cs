using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class HomeLayoutSettings(IPreferencesService preferences) : IHomeLayoutSettings
    {
        private const string LayoutKey = "Home.Layout";

        public HomeLayout Layout
        {
            get => HomeLayout.Parse(preferences.Get(LayoutKey, string.Empty));
            set => preferences.Set(LayoutKey, value.Serialize());
        }
    }
}
