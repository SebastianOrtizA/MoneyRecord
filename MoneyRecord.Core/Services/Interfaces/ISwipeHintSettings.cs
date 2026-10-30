namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The "Swipe a row left to edit or delete" caption on Transactions: shown on a few visits, never again after a swipe.</summary>
    public interface ISwipeHintSettings
    {
        /// <summary>The caption is still due (no swipe yet, fewer than <see cref="SwipeHintSettings.MaxVisits"/> visits shown).</summary>
        bool ShouldShow { get; }

        /// <summary>The caption was shown on one more visit.</summary>
        void RecordVisit();

        /// <summary>The user swiped a row: the caption has done its job.</summary>
        void MarkSeen();
    }
}
