using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The Home cards' order and visibility (Settings → Home screen), saved in the preferences.</summary>
    public interface IHomeLayoutSettings
    {
        HomeLayout Layout { get; set; }
    }
}
