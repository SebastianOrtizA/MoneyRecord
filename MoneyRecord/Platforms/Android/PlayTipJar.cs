#if PLAY_STORE
using Microsoft.Extensions.Logging;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using Plugin.InAppBilling;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// Tips through Google Play Billing (the Play build only): three consumable products, consumed right after the
    /// payment so the same tip can be given again. Nothing is stored in the app: the store keeps the purchase history.
    /// </summary>
    public sealed class PlayTipJar(ILogger<PlayTipJar> logger) : ITipJar
    {
        // One billing connection at a time: the start-up consume must not close the one a tip is using
        private readonly SemaphoreSlim _gate = new(1, 1);

        public bool IsAvailable => true;

        public async Task<IReadOnlyList<TipOption>> GetTipsAsync()
        {
            try
            {
                return await WithBillingAsync<IReadOnlyList<TipOption>>(async billing =>
                {
                    var products = await billing.GetProductInfoAsync(ItemType.InAppPurchase, [.. SupportLinks.TipProducts]);
                    return products
                        .OrderBy(p => p.MicrosPrice)
                        .Select(p => new TipOption(p.ProductId, p.LocalizedPrice))
                        .ToList();
                }, []);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load the tips from Google Play");
                return [];
            }
        }

        public async Task<TipOutcome> TipAsync(string productId)
        {
            try
            {
                return await WithBillingAsync(async billing =>
                {
                    // A tip left unconsumed (the app died after paying) blocks buying it again: consume it, then retry once
                    for (var attempt = 0; ; attempt++)
                    {
                        try
                        {
                            var purchase = await billing.PurchaseAsync(productId, ItemType.InAppPurchase);
                            if (purchase == null)
                                return TipOutcome.Failed;
                            if (purchase.State is PurchaseState.PaymentPending or PurchaseState.Deferred)
                                return TipOutcome.Pending;
                            if (purchase.State != PurchaseState.Purchased)
                                return TipOutcome.Failed;

                            // Already paid: a consume that fails must not report a failed tip (the user may pay again).
                            // ConsumePendingAsync retries at the next start, inside the 3 days before Play refunds it
                            try
                            {
                                await billing.ConsumePurchaseAsync(purchase.ProductId, purchase.PurchaseToken);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Tip {ProductId} paid but not consumed yet", purchase.ProductId);
                            }
                            return TipOutcome.Thanked;
                        }
                        catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.AlreadyOwned && attempt == 0)
                        {
                            await ConsumeOwnedAsync(billing);
                        }
                    }
                }, TipOutcome.Failed);
            }
            catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.UserCancelled)
            {
                return TipOutcome.Cancelled;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to pay the tip {ProductId}", productId);
                return TipOutcome.Failed;
            }
        }

        public async Task ConsumePendingAsync()
        {
            try
            {
                await WithBillingAsync(async billing =>
                {
                    await ConsumeOwnedAsync(billing);
                    return true;
                }, false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to consume the tips paid earlier");
            }
        }

        /// <summary>Consumes every paid tip still held; a payment still pending is left for the store to settle.</summary>
        private static async Task ConsumeOwnedAsync(IInAppBilling billing)
        {
            var owned = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
            foreach (var purchase in owned.Where(p => p.State == PurchaseState.Purchased && SupportLinks.TipProducts.Contains(p.ProductId)))
                await billing.ConsumePurchaseAsync(purchase.ProductId, purchase.PurchaseToken);
        }

        private async Task<T> WithBillingAsync<T>(Func<IInAppBilling, Task<T>> work, T whenOffline)
        {
            await _gate.WaitAsync();
            var billing = CrossInAppBilling.Current;
            try
            {
                return await billing.ConnectAsync() ? await work(billing) : whenOffline;
            }
            finally
            {
                try
                {
                    await billing.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to close the Google Play billing connection");
                }
                _gate.Release();
            }
        }
    }
}
#endif
