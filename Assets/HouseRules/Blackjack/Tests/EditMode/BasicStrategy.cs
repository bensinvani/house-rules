using System.Collections.Generic;

namespace HouseRules.Blackjack.Tests
{
    /// <summary>
    /// Test-only basic strategy for 6 decks, dealer stands soft 17, double after split
    /// allowed, no surrender. Used to drive the statistical house-edge test.
    /// VERIFY AGAINST A PUBLISHED CHART before diagnosing a house-edge failure.
    /// </summary>
    public static class BasicStrategy
    {
        public static PlayerAction Decide(
            Hand hand,
            Card dealerUpcard,
            IReadOnlyList<PlayerAction> legal)
        {
            int up = UpcardIndex(dealerUpcard);

            if (hand.IsPair && Contains(legal, PlayerAction.Split) && ShouldSplit(hand, up))
            {
                return PlayerAction.Split;
            }

            HandValue value = hand.Value;

            if (value.IsSoft)
            {
                return SoftDecision(value.Total, up, legal);
            }

            return HardDecision(value.Total, up, legal);
        }

        /// <summary>Maps a dealer upcard to a column index: 0 = 2 … 8 = 10, 9 = ace.</summary>
        private static int UpcardIndex(Card card)
        {
            if (card.Rank == Rank.Ace)
            {
                return 9;
            }

            return card.BaseValue - 2;
        }

        private static bool ShouldSplit(Hand hand, int up)
        {
            Rank rank = hand.Cards[0].Rank;

            switch (rank)
            {
                case Rank.Ace:
                    return true;
                case Rank.Eight:
                    return true;
                case Rank.Ten:
                case Rank.Jack:
                case Rank.Queen:
                case Rank.King:
                    return false;
                case Rank.Nine:
                    // Split against 2-6 and 8-9; stand against 7, 10, ace.
                    return (up >= 0 && up <= 4) || up == 6 || up == 7;
                case Rank.Seven:
                    return up <= 5;
                case Rank.Six:
                    return up <= 4;
                case Rank.Five:
                    return false;
                case Rank.Four:
                    return up == 3 || up == 4;
                case Rank.Three:
                case Rank.Two:
                    return up <= 5;
                default:
                    return false;
            }
        }

        private static PlayerAction HardDecision(int total, int up, IReadOnlyList<PlayerAction> legal)
        {
            if (total >= 17)
            {
                return PlayerAction.Stand;
            }

            if (total >= 13 && total <= 16)
            {
                return up <= 4 ? PlayerAction.Stand : Hit(legal);
            }

            if (total == 12)
            {
                return (up >= 2 && up <= 4) ? PlayerAction.Stand : Hit(legal);
            }

            if (total == 11)
            {
                return up <= 8 ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
            }

            if (total == 10)
            {
                return up <= 7 ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
            }

            if (total == 9)
            {
                return (up >= 1 && up <= 4) ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
            }

            return Hit(legal);
        }

        private static PlayerAction SoftDecision(int total, int up, IReadOnlyList<PlayerAction> legal)
        {
            switch (total)
            {
                case 20:
                case 21:
                    return PlayerAction.Stand;
                case 19:
                    return PlayerAction.Stand;
                case 18:
                    // S17 soft 18: stand vs 2, double-else-stand vs 3-6,
                    // stand vs 7-8, hit vs 9-A. Standing vs 2 is the S17/H17
                    // difference — under H17 this cell doubles instead.
                    if (up == 0)
                    {
                        return PlayerAction.Stand;
                    }

                    if (up <= 4)
                    {
                        return DoubleOr(PlayerAction.Stand, legal);
                    }

                    if (up == 5 || up == 6)
                    {
                        return PlayerAction.Stand;
                    }

                    return Hit(legal);
                case 17:
                    return (up >= 1 && up <= 4) ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
                case 16:
                case 15:
                    return (up >= 2 && up <= 4) ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
                case 14:
                case 13:
                    return (up >= 3 && up <= 4) ? DoubleOr(PlayerAction.Hit, legal) : Hit(legal);
                default:
                    return Hit(legal);
            }
        }

        /// <summary>Double if it is legal right now, otherwise fall back.</summary>
        private static PlayerAction DoubleOr(PlayerAction fallback, IReadOnlyList<PlayerAction> legal)
        {
            if (Contains(legal, PlayerAction.Double))
            {
                return PlayerAction.Double;
            }

            return fallback == PlayerAction.Hit ? Hit(legal) : PlayerAction.Stand;
        }

        private static PlayerAction Hit(IReadOnlyList<PlayerAction> legal)
        {
            return Contains(legal, PlayerAction.Hit) ? PlayerAction.Hit : PlayerAction.Stand;
        }

        private static bool Contains(IReadOnlyList<PlayerAction> legal, PlayerAction action)
        {
            for (int i = 0; i < legal.Count; i++)
            {
                if (legal[i] == action)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
