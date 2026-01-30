using MoneyRecord.Resources.Strings;
using System.ComponentModel;

namespace MoneyRecord.Extensions
{
    [ContentProperty(nameof(Key))]
    public class LocalizeExtension : IMarkupExtension<BindingBase>
    {
        public string Key { get; set; } = string.Empty;

        public BindingBase ProvideValue(IServiceProvider serviceProvider)
        {
            return new Binding
            {
                Mode = BindingMode.OneWay,
                Path = $"[{Key}]",
                Source = LocalizedStrings.Instance
            };
        }

        object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        {
            return ProvideValue(serviceProvider);
        }
    }

    public class LocalizedStrings : INotifyPropertyChanged
    {
        private static LocalizedStrings? _instance;
        public static LocalizedStrings Instance => _instance ??= new LocalizedStrings();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string this[string key]
        {
            get => AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key;
        }

        public void RaisePropertyChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}
