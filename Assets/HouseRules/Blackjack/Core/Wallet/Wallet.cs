using System;

namespace HouseRules.Blackjack
{
    /// <summary>
    /// Chip balance. Integer only — floating point money loses a chip on a 3:2 payout
    /// of an odd wager, and the error compounds invisibly across thousands of rounds.
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(long startingBalance)
        {
            if (startingBalance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingBalance), "Balance cannot start negative.");
            }

            Balance = startingBalance;
        }

        public long Balance { get; private set; }

        public bool CanAfford(long amount) => amount <= Balance;

        public void Debit(long amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Debit must be positive.");
            }

            if (amount > Balance)
            {
                throw new InvalidOperationException($"Cannot debit {amount} from a balance of {Balance}.");
            }

            Balance -= amount;
        }

        public void Credit(long amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Credit must be positive.");
            }

            Balance += amount;
        }
    }
}
