using System.IO;
using HouseRules.Blackjack.Presentation;
using NUnit.Framework;

namespace HouseRules.Blackjack.Tests
{
    public class WalletStoreTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"houserules-wallet-test-{Path.GetRandomFileName()}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        [Test]
        public void Load_WithNoSaveFile_ReturnsTheStartingBalance()
        {
            var store = new WalletStore(_path);
            Wallet wallet = store.Load();

            Assert.AreEqual(store.StartingBalanceDefault, wallet.Balance);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsTheBalance()
        {
            var store = new WalletStore(_path);
            var wallet = new Wallet(4242);

            store.Save(wallet);
            Wallet reloaded = store.Load();

            Assert.AreEqual(4242, reloaded.Balance);
        }

        [Test]
        public void Save_WritesJsonContainingTheBalance()
        {
            var store = new WalletStore(_path);
            store.Save(new Wallet(777));

            Assert.IsTrue(File.Exists(_path));
            StringAssert.Contains("777", File.ReadAllText(_path));
        }

        [Test]
        public void Save_Twice_OverwritesRatherThanAppends()
        {
            var store = new WalletStore(_path);

            store.Save(new Wallet(100));
            store.Save(new Wallet(200));

            Assert.AreEqual(200, store.Load().Balance);
        }

        [Test]
        public void Load_WithCorruptFile_FallsBackToTheStartingBalance()
        {
            File.WriteAllText(_path, "this is not json {{{");

            var store = new WalletStore(_path);
            Wallet wallet = store.Load();

            Assert.AreEqual(store.StartingBalanceDefault, wallet.Balance);
        }

        [Test]
        public void Load_WithNegativeBalance_FallsBackToTheStartingBalance()
        {
            File.WriteAllText(_path, "{\"balance\":-500}");

            var store = new WalletStore(_path);

            Assert.AreEqual(store.StartingBalanceDefault, store.Load().Balance);
        }

        [Test]
        public void Load_WithZeroBalance_IsPreserved()
        {
            // Busting out is a real state, not corruption.
            var store = new WalletStore(_path);
            store.Save(new Wallet(0));

            Assert.AreEqual(0, store.Load().Balance);
        }

        [Test]
        public void Delete_RemovesTheSaveFile()
        {
            var store = new WalletStore(_path);
            store.Save(new Wallet(500));
            Assert.IsTrue(File.Exists(_path));

            store.Delete();

            Assert.IsFalse(File.Exists(_path));
            Assert.AreEqual(store.StartingBalanceDefault, store.Load().Balance);
        }

        [Test]
        public void Delete_WithNoFile_DoesNotThrow()
        {
            var store = new WalletStore(_path);
            Assert.DoesNotThrow(() => store.Delete());
        }
    }
}
