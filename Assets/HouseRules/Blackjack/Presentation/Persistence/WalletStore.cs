using System;
using System.IO;
using HouseRules.Blackjack;
using UnityEngine;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Persists only the chip balance, as JSON. A round is atomic and is never saved
    /// mid-play: quitting mid-round abandons it and refunds the stake instead.
    /// </summary>
    public sealed class WalletStore
    {
        private const long DefaultStartingBalance = 1000;
        private const string FileName = "wallet.json";

        private readonly string _filePath;

        public WalletStore(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public long StartingBalanceDefault => DefaultStartingBalance;

        public Wallet Load()
        {
            if (!File.Exists(_filePath))
            {
                return new Wallet(DefaultStartingBalance);
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                // A missing or negative balance means the file is damaged. Chips are
                // play money, so recovering to the default beats refusing to start.
                if (data == null || data.balance < 0)
                {
                    return new Wallet(DefaultStartingBalance);
                }

                return new Wallet(data.balance);
            }
            catch (Exception)
            {
                return new Wallet(DefaultStartingBalance);
            }
        }

        public void Save(Wallet wallet)
        {
            if (wallet == null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }

            var data = new SaveData { balance = wallet.Balance };
            string directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonUtility.ToJson(data));
        }

        public void Delete()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        /// <summary>
        /// Serializable carrier. JsonUtility requires a concrete type with public
        /// fields — it cannot serialize properties or anonymous types.
        /// </summary>
        [Serializable]
        private sealed class SaveData
        {
            public long balance;
        }
    }
}
