using CommunityToolkit.Mvvm.Input;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// What a page's filter button and filter sheet (<c>FilterButton</c>, <c>FilterSheet</c>) need: open and close, how many
    /// filters narrow the page, and a way to reset them. The filters themselves are the page's own, placed inside the sheet.
    /// </summary>
    public interface IFilterSheetSource
    {
        bool IsFilterOpen { get; }

        /// <summary>How many filters are on (the badge on the button); a search box beside the button is not counted.</summary>
        int ActiveFilterCount { get; }

        bool HasActiveFilterCount { get; }

        IRelayCommand OpenFilterCommand { get; }

        IRelayCommand CloseFilterCommand { get; }

        /// <summary>Every filter back to its default.</summary>
        IRelayCommand ClearFiltersCommand { get; }
    }
}
