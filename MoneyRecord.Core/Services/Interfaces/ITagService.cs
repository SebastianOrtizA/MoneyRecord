using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Tags (labels typed in the entry form): the ones in use, renamed, merged and removed across all transactions.</summary>
    public interface ITagService
    {
        /// <summary>Every tag in use with its transaction count and latest use (names ignoring case, alphabetically).</summary>
        Task<List<TagSummary>> GetAllAsync();

        /// <summary>
        /// Gives every transaction with <paramref name="tag"/> the new name; onto a tag that already exists it merges the two.
        /// Returns how many transactions changed.
        /// </summary>
        /// <exception cref="InvalidOperationException">The new name is empty.</exception>
        Task<int> RenameAsync(string tag, string newName);

        /// <summary>Takes the tag off every transaction; they stay. Returns how many changed.</summary>
        Task<int> DeleteAsync(string tag);
    }
}
