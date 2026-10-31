using CommunityToolkit.Mvvm.Input;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// What a <c>PeriodStepper</c> (‹ October 2026 ›) shows and does: the shared period of the tabs
    /// (<see cref="PeriodViewModel"/>) and a savings challenge's calendar month use the same control.
    /// </summary>
    public interface IPeriodStepperSource
    {
        /// <summary>The text between the arrows.</summary>
        string Label { get; }

        /// <summary>Whether the arrows show at all, and whether a step back / forward is possible.</summary>
        bool CanStepBack { get; }

        bool CanStepForward { get; }

        IRelayCommand PreviousCommand { get; }

        IRelayCommand NextCommand { get; }

        /// <summary>A tap on the label (opens the period sheet, or goes back to the current month).</summary>
        IRelayCommand PickCommand { get; }

        /// <summary>The chevron beside the label: the tap opens a list.</summary>
        bool HasPicker { get; }

        /// <summary>The label's accessibility description (what a tap on it does).</summary>
        string PickHint { get; }
    }
}
