using System;
using System.Collections.Generic;
using System.IO;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using UnityEngine;

namespace Bouncer.Run
{
    [Serializable]
    public sealed class RunSaveData
    {
        public int version = 1;
        public string appVersion;
        public string scene;
        public int arenaIndex;
        public int variant;
        public int danger;
        public int kid;
        public float pastTime;
        public int pastKills;
        public int coinsEarned;
        public float runClock;
        public int lives;
        public int coins;
        public float coinCarry;
        public bool secondWindUsed;
        public string[] cards = Array.Empty<string>();
        public string[] locked = Array.Empty<string>();
        public int[] carried = Array.Empty<int>();
        public int backpack;
    }

    /// <summary>
    /// Сохранение одиночной прогулки. Записывается, когда игрок выходит с очищенной арены (карточки, монетки, жизни,
    /// следующая арена), стирается, когда прогулка кончилась победой или поражением. «Продолжить» начинает следующую
    /// арену с начала, как будто игрок только что вышел на неё.
    /// </summary>
    public static class RunSave
    {
        const int Version = 1;
        const string FileName = "run_save.json";

        static RunSaveData s_cached;
        static bool s_loaded;

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists => Peek() != null;

        public static RunSaveData Peek()
        {
            if (s_loaded)
                return s_cached;
            s_loaded = true;
            s_cached = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath));
                    if (data != null && data.version == Version && !string.IsNullOrEmpty(data.scene))
                        s_cached = data;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RunSave] Не прочитал сохранение: {exception.Message}");
            }
            return s_cached;
        }

        /// <summary>Игрок вышел с арены: RunState уже указывает на следующую.</summary>
        public static void Write(string nextScene)
        {
            if (!Applicable || string.IsNullOrEmpty(nextScene))
                return;
            var local = Players.Local;
            if (local == null || !local.TryGetComponent(out PlayerCards cards))
                return;
            int slot = cards.Slot;
            var offers = new List<OfferKind>();
            cards.GetOpenOffers(offers);
            offers.RemoveAll(kind => kind == OfferKind.Start);
            var carried = new int[offers.Count];
            for (int i = 0; i < carried.Length; i++)
                carried[i] = (int)offers[i];
            var data = new RunSaveData
            {
                version = Version,
                appVersion = Application.version,
                scene = nextScene,
                arenaIndex = RunState.ArenaIndex,
                variant = RunState.ArenaVariant,
                danger = RunState.Danger,
                kid = GameSettings.Kid,
                pastTime = RunState.PastTime,
                pastKills = RunState.PastKills,
                coinsEarned = RunState.CoinsEarned,
                runClock = RunState.RunClock,
                lives = RunState.LivesOf(slot),
                coins = RunState.CoinsOf(slot),
                coinCarry = RunState.CoinCarryOf(slot),
                secondWindUsed = RunState.SecondWindUsed(slot),
                cards = Names(RunCards.Taken(slot)),
                locked = Names(RunCards.Locked(slot)),
                carried = carried,
                backpack = RunCards.BackpackOf(slot),
            };
            try
            {
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                File.Move(temp, FilePath);
                s_cached = data;
                s_loaded = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RunSave] Не записал сохранение: {exception.Message}");
            }
        }

        public static void Delete()
        {
            s_cached = null;
            s_loaded = true;
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RunSave] Не удалил сохранение: {exception.Message}");
            }
        }

        /// <summary>Выставляет RunState и карточки по сохранению. Дальше остаётся загрузить сцену арены.</summary>
        public static string Resume()
        {
            var data = Peek();
            if (data == null)
                return null;
            var deck = CardDeck.Active;
            if (deck == null)
                foreach (var found in Resources.FindObjectsOfTypeAll<CardDeck>())
                    if (found.deck.Count > 0)
                    {
                        deck = found;
                        break;
                    }
            if (deck == null)
                return null;

            GameSettings.Kid = Mathf.Max(0, data.kid);
            RunState.ResumeSolo(data.danger, data.arenaIndex, data.variant, data.pastTime, data.pastKills, data.coinsEarned,
                data.runClock);
            var taken = Cards(deck, data.cards);
            var locked = Cards(deck, data.locked);
            var carried = new List<OfferKind>();
            foreach (int kind in data.carried)
                if (Enum.IsDefined(typeof(OfferKind), kind) && (OfferKind)kind != OfferKind.Start)
                    carried.Add((OfferKind)kind);
            RunCards.Restore(0, taken, locked, carried, data.backpack);
            RunState.RestorePlayer(0, data.coins, data.coinCarry, data.secondWindUsed, data.lives, int.MaxValue, false);
            return data.scene;
        }

        static bool Applicable => !Online.Active && !Tutorial.Active && !BallLab.Active && RunState.Active;

        static string[] Names(IReadOnlyList<UpgradeCard> cards)
        {
            var names = new List<string>(cards.Count);
            foreach (var card in cards)
                if (card)
                    names.Add(card.name);
            return names.ToArray();
        }

        static List<UpgradeCard> Cards(CardDeck deck, string[] names)
        {
            var result = new List<UpgradeCard>();
            if (names == null)
                return result;
            foreach (string name in names)
            {
                UpgradeCard match = deck.filler && deck.filler.name == name ? deck.filler : null;
                if (match == null)
                    foreach (var card in deck.deck)
                        if (card && card.name == name)
                        {
                            match = card;
                            break;
                        }
                if (match)
                    result.Add(match);
                else
                    Debug.LogWarning($"[RunSave] Карточки «{name}» больше нет в колоде — пропускаю.");
            }
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Hook()
        {
            GameEvents.RunFinished -= OnRunFinished;
            GameEvents.RunFinished += OnRunFinished;
        }

        static void OnRunFinished(bool victory)
        {
            if (Applicable)
                Delete();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_cached = null;
            s_loaded = false;
        }
    }
}
