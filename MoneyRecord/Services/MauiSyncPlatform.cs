using MoneyRecord.Services.Sync;

namespace MoneyRecord.Services
{
    /// <summary>The desktop app listens (it shows the code); the phone connects to it.</summary>
    public sealed class MauiSyncPlatform : ISyncPlatform
    {
#if WINDOWS
        public bool CanListen => true;
#else
        public bool CanListen => false;
#endif

        public string DeviceName => string.IsNullOrWhiteSpace(DeviceInfo.Current.Name) ? DeviceInfo.Current.Model : DeviceInfo.Current.Name;
    }
}
