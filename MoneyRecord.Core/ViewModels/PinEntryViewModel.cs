using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;

namespace MoneyRecord.ViewModels
{
    /// <summary>The digits typed on the PIN pad (the lock screen and the PIN setup share it): shown as dots, never as text.</summary>
    public abstract partial class PinEntryViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Dots), nameof(DigitCountText), nameof(CanSubmit))]
        private string digits = string.Empty;

        /// <summary>The pad takes digits (false while it is locked out or busy).</summary>
        public virtual bool IsEntryEnabled => true;

        /// <summary>One dot per digit, spaced; empty while nothing is typed.</summary>
        public string Dots => string.Join(' ', Enumerable.Repeat('●', Digits.Length));

        /// <summary>For screen readers: the dots say how many digits, never which.</summary>
        public string DigitCountText => string.Format(Resources.Strings.AppResources.PinDigitsTyped, Digits.Length);

        public bool CanSubmit => IsEntryEnabled && Digits.Length >= PinPolicy.MinLength;

        [RelayCommand]
        private void PressDigit(string digit)
        {
            if (IsEntryEnabled && Digits.Length < PinPolicy.MaxLength && digit.Length == 1 && char.IsAsciiDigit(digit[0]))
                Digits += digit;
        }

        [RelayCommand]
        private void Backspace()
        {
            if (Digits.Length > 0)
                Digits = Digits[..^1];
        }

        protected void ClearDigits() => Digits = string.Empty;
    }
}
