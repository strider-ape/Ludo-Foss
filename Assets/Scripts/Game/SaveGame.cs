using System;
using LudoGame.Core;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// Keeps one unfinished game on the device (saved at the start of every turn) so it can be resumed after the
    /// app is closed. Stored locally in PlayerPrefs; nothing leaves the phone.
    /// </summary>
    public static class SaveGame
    {
        const string Key = "savedGame";
        const int Version = 1;

        [Serializable]
        sealed class Data
        {
            public int version;
            public int[] seats;
            public int[] progress;
            public int current;
            public int sixes;
            public int[] finished;
        }

        public static bool Exists => PlayerPrefs.HasKey(Key) && Load() != null;

        public static void Save(GameState s)
        {
            PlayerPrefs.SetString(Key, ToJson(s));
            PlayerPrefs.Save();
        }

        public static GameState Load() => PlayerPrefs.HasKey(Key) ? FromJson(PlayerPrefs.GetString(Key)) : null;

        public static void Clear()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        public static string ToJson(GameState s)
        {
            var data = new Data
            {
                version = Version,
                seats = new int[GameState.Players],
                progress = new int[GameState.Players * GameState.TokensPerPlayer],
                current = (int)s.Current,
                sixes = s.ConsecutiveSixes,
                finished = new int[s.Finished.Count],
            };
            for (int p = 0; p < GameState.Players; p++)
            {
                data.seats[p] = (int)s.Seats[p];
                for (int t = 0; t < GameState.TokensPerPlayer; t++)
                {
                    data.progress[p * GameState.TokensPerPlayer + t] = s.Get((PlayerColor)p, t);
                }
            }
            for (int i = 0; i < s.Finished.Count; i++) data.finished[i] = (int)s.Finished[i];
            return JsonUtility.ToJson(data);
        }

        /// <summary>Rebuilds a game from JSON, or returns null if the data is missing, old or inconsistent.</summary>
        public static GameState FromJson(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<Data>(json);
                if (data == null || data.version != Version || data.seats == null || data.seats.Length != GameState.Players
                    || data.progress == null || data.progress.Length != GameState.Players * GameState.TokensPerPlayer
                    || data.finished == null || data.current < 0 || data.current >= GameState.Players)
                {
                    return null;
                }
                var seats = new SeatType[GameState.Players];
                for (int p = 0; p < GameState.Players; p++) seats[p] = (SeatType)data.seats[p];
                var s = new GameState(seats);
                for (int p = 0; p < GameState.Players; p++)
                {
                    for (int t = 0; t < GameState.TokensPerPlayer; t++)
                    {
                        int value = data.progress[p * GameState.TokensPerPlayer + t];
                        if (value < BoardLayout.InBase || value > BoardLayout.HomeProgress) return null;
                        s.Set((PlayerColor)p, t, value);
                    }
                }
                foreach (int f in data.finished) s.Finished.Add((PlayerColor)f);
                s.Current = (PlayerColor)data.current;
                s.ConsecutiveSixes = Mathf.Clamp(data.sixes, 0, 2);
                if (s.IsOver || !s.IsActive(s.Current) || s.HasFinished(s.Current)) return null;
                return s;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
