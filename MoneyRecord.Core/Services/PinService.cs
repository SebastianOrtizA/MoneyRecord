using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public enum PinResult
    {
        Correct,
        Wrong,
        LockedOut
    }

    /// <param name="TriesLeft">Free tries left after a wrong one (zero once the waits start).</param>
    /// <param name="LockedUntil">When the pad opens again: set with <see cref="PinResult.LockedOut"/>, and with a wrong try that started a wait.</param>
    public readonly record struct PinCheck(PinResult Result, int TriesLeft = 0, DateTimeOffset? LockedUntil = null);

    /// <summary>
    /// The app PIN: set, remove, and check with the lockout. The wrong-try counter and the "locked until" time are kept in
    /// the same secure store as the hash, so a restart (or a cleared preferences file) does not reset them.
    /// </summary>
    public sealed class PinService(IPinStore store, IPrivacySettings settings, TimeProvider clock)
    {
        public bool IsSupported => store.IsSupported;

        /// <summary>A PIN is set (a flag in the preferences, so the lock can decide without asking the secure store).</summary>
        public bool HasPin => settings.HasPin;

        public async Task SetAsync(string pin)
        {
            if (PinPolicy.Check(pin) != PinProblem.None)
                throw new ArgumentException("The PIN breaks the PIN rules.", nameof(pin));
            await store.SetHashAsync(PinHash.Create(pin));
            await store.SetLockoutAsync(null);
            settings.HasPin = true;
        }

        public async Task RemoveAsync()
        {
            settings.HasPin = false;
            await store.SetHashAsync(null);
            await store.SetLockoutAsync(null);
            // Only the PIN opened the app: back to the phone's lock
            if (settings.LockMethod == LockMethod.Pin)
                settings.LockMethod = LockMethod.DeviceLock;
        }

        /// <summary>The time the pad opens again, or null when it is open.</summary>
        public async Task<DateTimeOffset?> LockedUntilAsync()
        {
            var (_, until) = Parse(await store.GetLockoutAsync());
            return until > clock.GetUtcNow() ? until : null;
        }

        public async Task<PinCheck> VerifyAsync(string pin)
        {
            var (failures, until) = Parse(await store.GetLockoutAsync());
            var now = clock.GetUtcNow();
            if (until > now)
                return new PinCheck(PinResult.LockedOut, 0, until);

            if (PinHash.Verify(pin, await store.GetHashAsync()))
            {
                if (failures != 0)
                    await store.SetLockoutAsync(null);
                return new PinCheck(PinResult.Correct);
            }

            failures++;
            var delay = LockoutPolicy.DelayAfter(failures);
            DateTimeOffset? lockedUntil = delay > TimeSpan.Zero ? now + delay : null;
            await store.SetLockoutAsync($"{failures};{lockedUntil?.UtcTicks ?? 0}");
            return new PinCheck(PinResult.Wrong, LockoutPolicy.FreeTriesLeft(failures), lockedUntil);
        }

        private static (int Failures, DateTimeOffset? Until) Parse(string? state)
        {
            var parts = state?.Split(';');
            if (parts is not { Length: 2 } || !int.TryParse(parts[0], out var failures) || !long.TryParse(parts[1], out var ticks))
                return (0, null);
            return (failures, ticks > 0 ? new DateTimeOffset(ticks, TimeSpan.Zero) : null);
        }
    }
}
