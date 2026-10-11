using System;
using System.Collections.Generic;

namespace LudoGame.Core
{
    /// <summary>Plays a game to the end with no animation: used for "Skip to results" and the self-test.</summary>
    public static class AutoPlay
    {
        /// <summary>
        /// Lets the bot play every remaining seat until the game is decided, then returns the final ranking.
        /// Changes <paramref name="s"/>.
        /// </summary>
        public static List<PlayerColor> PlayOut(GameState s, Bot bot, Random rng, bool threeSixesRule, bool playForPlaces, int maxRolls = 200000)
        {
            int rolls = 0;
            while (!s.IsOver && rolls++ < maxRolls)
            {
                var p = s.Current;
                int roll = rng.Next(1, 7);
                s.ConsecutiveSixes = roll == 6 ? s.ConsecutiveSixes + 1 : 0;
                if (threeSixesRule && s.ConsecutiveSixes == 3)
                {
                    s.PassTurn();
                    continue;
                }
                var moves = Rules.LegalMoves(s, p, roll);
                if (moves.Count == 0)
                {
                    if (roll != 6) s.PassTurn();
                    continue;
                }
                var result = Rules.Apply(s, bot.Choose(s, moves, roll));
                if (result.Finished)
                {
                    if (!playForPlaces) break;
                    s.PassTurn();
                    continue;
                }
                if (!Rules.GrantsExtraTurn(roll, result)) s.PassTurn();
            }
            return s.Ranking();
        }
    }
}
