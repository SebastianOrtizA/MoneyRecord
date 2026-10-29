namespace MoneyRecord.Models
{
    /// <summary>One tip the Support screen offers (Play build): the store's product and its price in the user's currency.</summary>
    /// <param name="ProductId">One of <see cref="Helpers.SupportLinks.TipProducts"/>.</param>
    /// <param name="Price">As the store shows it ("$0.99", "0,99 €"); never parsed or added up.</param>
    public sealed record TipOption(string ProductId, string Price);
}
