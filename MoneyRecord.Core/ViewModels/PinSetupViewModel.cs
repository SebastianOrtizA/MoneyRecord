using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;

namespace MoneyRecord.ViewModels
{
    /// <summary>Choosing an app PIN: typed once, then repeated. <see cref="Finished"/> says whether it was saved.</summary>
    public partial class PinSetupViewModel(PinService pins) : PinEntryViewModel
    {
        private string? _first;

        public event Action<bool>? Finished;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        private bool isConfirming;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string error = string.Empty;

        public bool HasError => Error.Length > 0;

        public string Title => IsConfirming ? AppResources.PinRepeatTitle : AppResources.PinSetTitle;

        public string Message => AppResources.PinSetMessage;

        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (!CanSubmit)
                return;

            if (!IsConfirming)
            {
                var problem = PinPolicy.Check(Digits);
                if (problem != PinProblem.None)
                {
                    Error = problem == PinProblem.Repeated ? AppResources.PinRepeated : AppResources.PinLength;
                    ClearDigits();
                    return;
                }
                _first = Digits;
                IsConfirming = true;
                Error = string.Empty;
                ClearDigits();
                return;
            }

            if (Digits != _first)
            {
                // Start again from the first entry
                _first = null;
                IsConfirming = false;
                Error = AppResources.PinMismatch;
                ClearDigits();
                return;
            }

            await pins.SetAsync(Digits);
            ClearDigits();
            Finished?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            ClearDigits();
            Finished?.Invoke(false);
        }
    }
}
