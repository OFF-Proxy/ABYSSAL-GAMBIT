using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoChessBossRush.Save
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private ISaveStore store;
        public SaveData Data { get; private set; }

        // === セーブスロット ===
        public const int SlotCount = 3;
        private const string ActiveSlotKey = "save.activeSlot";
        public int ActiveSlot { get; private set; }

        private static string SlotFileName(int slot) => $"save_{slot}.json";

        // スロット表示用の要約情報。
        public struct SlotInfo
        {
            public bool exists;
            public int highestClearedChapter;
            public int bestScore;
            public int bossAllyCount;
            public long lastSavedUnixSec;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
        }

        public static SaveManager EnsureExists()
        {
            if (Instance != null)
                return Instance;

            SaveManager existing = FindObjectOfType<SaveManager>(true);
            if (existing != null)
            {
                Instance = existing;
                Instance.EnsureInitialized();
                return Instance;
            }

            GameObject managerObject = new GameObject("SaveManager", typeof(SaveManager));
            DontDestroyOnLoad(managerObject);
            Instance = managerObject.GetComponent<SaveManager>();
            Instance.EnsureInitialized();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (Data != null) return;
            MigrateLegacySaveIfNeeded();
            ActiveSlot = Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 0), 0, SlotCount - 1);
            if (store == null) store = new LocalJsonSaveStore(Application.persistentDataPath, SlotFileName(ActiveSlot));
            Data = store.Load();
            if (Data == null) Data = new SaveData();
        }

        // 旧・単一 save.json をスロット0へ一度だけ引き継ぐ（既存プレイヤーの進捗を失わないため）。
        private static void MigrateLegacySaveIfNeeded()
        {
            try
            {
                string legacy = System.IO.Path.Combine(Application.persistentDataPath, "save.json");
                string slot0 = System.IO.Path.Combine(Application.persistentDataPath, SlotFileName(0));
                if (System.IO.File.Exists(legacy) && !System.IO.File.Exists(slot0))
                    System.IO.File.Copy(legacy, slot0);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] legacy save migration failed: {e.Message}");
            }
        }

        public void SetStore(ISaveStore newStore)
        {
            store = newStore;
            Data = store != null ? store.Load() : new SaveData();
        }

        // 使用するスロットを切り替える（選択したスロットのデータを読み込む）。PlayerPrefs に記憶。
        public void SetActiveSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            ActiveSlot = slot;
            PlayerPrefs.SetInt(ActiveSlotKey, slot);
            PlayerPrefs.Save();
            store = new LocalJsonSaveStore(Application.persistentDataPath, SlotFileName(slot));
            Data = store.Load();
            if (Data == null) Data = new SaveData();
        }

        // スロットのデータを削除する。アクティブスロットなら空データにリセット（未保存）。
        public void DeleteSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            new LocalJsonSaveStore(Application.persistentDataPath, SlotFileName(slot)).Delete();
            if (slot == ActiveSlot)
                Data = new SaveData();
        }

        // 指定スロットの要約（アクティブを変えずに読み出す）。UI 表示用。
        public SlotInfo GetSlotInfo(int slot)
        {
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            var slotStore = new LocalJsonSaveStore(Application.persistentDataPath, SlotFileName(slot));
            SlotInfo info = new SlotInfo();
            if (!slotStore.Exists())
                return info; // exists=false（空スロット）

            info.exists = true;
            SaveData d = slotStore.Load();
            if (d != null)
            {
                int highest = 0, best = 0;
                for (int i = 0; i < d.chapters.Count; i++)
                {
                    if (d.chapters[i].cleared && d.chapters[i].chapter > highest) highest = d.chapters[i].chapter;
                    if (d.chapters[i].bestScore > best) best = d.chapters[i].bestScore;
                }
                info.highestClearedChapter = highest;
                info.bestScore = best;
                info.bossAllyCount = d.bossAllies != null ? d.bossAllies.Count : 0;
                info.lastSavedUnixSec = d.lastSavedUnixSec;
            }
            return info;
        }

        public void Save()
        {
            if (Data == null) Data = new SaveData();
            Data.lastSavedUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            store?.Save(Data);
        }

        public ChapterRecord GetChapter(int chapter)
        {
            EnsureInitialized();
            for (int i = 0; i < Data.chapters.Count; i++)
            {
                if (Data.chapters[i].chapter == chapter)
                    return Data.chapters[i];
            }
            ChapterRecord rec = new ChapterRecord { chapter = chapter };
            Data.chapters.Add(rec);
            return rec;
        }

        public bool IsChapterUnlocked(int chapter)
        {
            if (chapter <= 1) return true;
            EnsureInitialized();
            for (int i = 0; i < Data.chapters.Count; i++)
            {
                if (Data.chapters[i].chapter == chapter - 1)
                    return Data.chapters[i].cleared;
            }
            return false;
        }

        public bool RecordChapterResult(int chapter, int score, float timeSec, bool cleared)
        {
            EnsureInitialized();
            ChapterRecord rec = GetChapter(chapter);
            bool isNewRecord = false;

            if (cleared)
            {
                rec.cleared = true;
                rec.clearCount++;
            }

            if (score > rec.bestScore)
            {
                rec.bestScore = score;
                isNewRecord = true;
            }
            else if (score == rec.bestScore && score > 0 && cleared)
            {
                if (rec.bestTimeSec <= 0f || (timeSec > 0f && timeSec < rec.bestTimeSec))
                    isNewRecord = true;
            }

            if (cleared && timeSec > 0f && (rec.bestTimeSec <= 0f || timeSec < rec.bestTimeSec))
                rec.bestTimeSec = timeSec;

            Save();
            return isNewRecord;
        }

        public IReadOnlyList<BossAllyRecord> BossAllies
        {
            get
            {
                EnsureInitialized();
                return Data.bossAllies;
            }
        }

        public bool HasBossAlly(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return false;
            EnsureInitialized();
            for (int i = 0; i < Data.bossAllies.Count; i++)
            {
                if (string.Equals(Data.bossAllies[i].unitId, unitId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public void AddBossAlly(string unitId, int starLevel)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            EnsureInitialized();
            int clampedStar = Mathf.Max(1, starLevel);
            for (int i = 0; i < Data.bossAllies.Count; i++)
            {
                if (string.Equals(Data.bossAllies[i].unitId, unitId, StringComparison.OrdinalIgnoreCase))
                {
                    // R1-collection: 再獲得（章再クリア）で育成レベル（recruitCount）を加算。
                    if (Data.bossAllies[i].recruitCount < 1) Data.bossAllies[i].recruitCount = 1;
                    Data.bossAllies[i].recruitCount++;
                    if (clampedStar > Data.bossAllies[i].starLevel)
                        Data.bossAllies[i].starLevel = clampedStar;
                    Save();
                    return;
                }
            }
            Data.bossAllies.Add(new BossAllyRecord
            {
                unitId = unitId,
                starLevel = clampedStar,
                acquiredUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                recruitCount = 1
            });
            Save();
        }

        // === R1-collection: ボス育成（アフィニティ） ===
        // 育成レベル＝獲得累計回数（未所持は0）。
        public int GetBossAffinityLevel(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return 0;
            EnsureInitialized();
            for (int i = 0; i < Data.bossAllies.Count; i++)
            {
                if (string.Equals(Data.bossAllies[i].unitId, unitId, StringComparison.OrdinalIgnoreCase))
                    return Mathf.Max(1, Data.bossAllies[i].recruitCount);
            }
            return 0;
        }

        // 育成によるステータス倍率。Lv1=等倍、以降1レベルごとに +6%（上限 Lv11=+60%）。暫定値・R3-balance。
        public float GetBossAffinityStatMultiplier(string unitId)
        {
            int level = GetBossAffinityLevel(unitId);
            if (level <= 1) return 1f;
            int bonusLevels = Mathf.Min(level - 1, 10);
            return 1f + 0.06f * bonusLevels;
        }

        // === R3-hero-select: スロットごとの主人公（ヒーロー）ID ===
        // 空文字＝未選択（新規スロット or 旧セーブ）。ロビー入場前に選択フローで埋める。
        public string GetHeroUnitId()
        {
            EnsureInitialized();
            return Data != null ? (Data.heroUnitId ?? string.Empty) : string.Empty;
        }

        public void SetHeroUnitId(string id)
        {
            EnsureInitialized();
            if (Data == null) return;
            Data.heroUnitId = id ?? string.Empty;
            Save();
        }

        // === STORY: 真偽フラグの汎用ストア（幕間既読・スキン解放/選択など） ===
        // 存在＝true。大文字小文字は無視。旧セーブは storyFlags が空で全て false 扱い。
        public bool GetStoryFlag(string key)
        {
            EnsureInitialized();
            if (Data == null || Data.storyFlags == null || string.IsNullOrEmpty(key)) return false;
            for (int i = 0; i < Data.storyFlags.Count; i++)
                if (string.Equals(Data.storyFlags[i], key, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public void SetStoryFlag(string key, bool value)
        {
            EnsureInitialized();
            if (Data == null || string.IsNullOrEmpty(key)) return;
            if (Data.storyFlags == null) Data.storyFlags = new System.Collections.Generic.List<string>();
            bool exists = GetStoryFlag(key);
            if (value && !exists) Data.storyFlags.Add(key);
            else if (!value && exists)
                Data.storyFlags.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            else return; // 変化なし
            Save();
        }

        // === R4-collection-hub: ショップ選抜（恒久ON/OFF）。shopDisabledUnitIds に入る＝ショップに出さない。 ===
        // 既定（未登録）＝ON。大文字小文字は無視。旧セーブは集合が空で全ユニットON。
        public bool IsShopUnitEnabled(string unitId)
        {
            EnsureInitialized();
            if (Data == null || Data.shopDisabledUnitIds == null || string.IsNullOrEmpty(unitId)) return true;
            for (int i = 0; i < Data.shopDisabledUnitIds.Count; i++)
                if (string.Equals(Data.shopDisabledUnitIds[i], unitId, StringComparison.OrdinalIgnoreCase))
                    return false;
            return true;
        }

        public void SetShopUnitEnabled(string unitId, bool enabled)
        {
            EnsureInitialized();
            if (Data == null || string.IsNullOrEmpty(unitId)) return;
            if (Data.shopDisabledUnitIds == null) Data.shopDisabledUnitIds = new System.Collections.Generic.List<string>();
            bool disabledNow = !IsShopUnitEnabled(unitId);
            if (enabled && disabledNow)
                Data.shopDisabledUnitIds.RemoveAll(k => string.Equals(k, unitId, StringComparison.OrdinalIgnoreCase));
            else if (!enabled && !disabledNow)
                Data.shopDisabledUnitIds.Add(unitId);
            else return; // 変化なし
            Save();
        }

        // === R3-hero-mastery: 熟練度（チャプタークリアで上昇）＋必殺バリアント解放/装備 ===
        // 必要XPはLvが上がるごとに増える（やり込み型）。最大Lv50を目標に。
        public const int HeroMasteryMaxLevel = 50;      // 熟練度の上限Lv。
        public const int HeroMasteryXpBase = 8;         // Lv1→2 に必要なXP。
        public const int HeroMasteryXpStep = 4;         // Lvが1上がるごとに増える必要XP量。
        public const int HeroMasteryXpPerLevel = HeroMasteryXpBase; // 互換: 旧名（最初のLvUP必要XP）。
        public const float HeroMasteryPerLevel = 0.01f; // 熟練度Lvごとの基礎ステ倍率（+1%/Lv、Lv1=+0% → Lv50で+49%）。
        public const int HeroUltAUnlockLevel = 3;       // 必殺バリアントA 解放Lv。
        public const int HeroUltBUnlockLevel = 6;       // 必殺バリアントB 解放Lv。

        // Lv `level` から `level+1` へ上がるのに必要なXP（base + 増分×経過Lv）。
        public static int HeroMasteryXpForLevelUp(int level)
        {
            if (level < 1) level = 1;
            return HeroMasteryXpBase + (level - 1) * HeroMasteryXpStep;
        }

        // Lv `level` 到達に必要な累計XP（Lv1=0）。
        public static int HeroMasteryCumulativeXp(int level)
        {
            if (level <= 1) return 0;
            if (level > HeroMasteryMaxLevel) level = HeroMasteryMaxLevel;
            int total = 0;
            for (int l = 1; l < level; l++) total += HeroMasteryXpForLevelUp(l);
            return total;
        }

        private HeroMasteryRecord GetHeroMastery(string heroId, bool createIfMissing)
        {
            EnsureInitialized();
            if (Data == null || string.IsNullOrEmpty(heroId)) return null;
            for (int i = 0; i < Data.heroMastery.Count; i++)
                if (string.Equals(Data.heroMastery[i].heroUnitId, heroId, StringComparison.OrdinalIgnoreCase))
                    return Data.heroMastery[i];
            if (!createIfMissing) return null;
            HeroMasteryRecord rec = new HeroMasteryRecord { heroUnitId = heroId };
            Data.heroMastery.Add(rec);
            return rec;
        }

        public int GetHeroMasteryXp(string heroId)
        {
            HeroMasteryRecord rec = GetHeroMastery(heroId, false);
            return rec != null ? Mathf.Max(0, rec.masteryXp) : 0;
        }

        // 熟練度Lv（1スタート、上限 HeroMasteryMaxLevel）。累計XP曲線から逆算。
        public int GetHeroMasteryLevel(string heroId)
        {
            int xp = GetHeroMasteryXp(heroId);
            int lv = 1;
            while (lv < HeroMasteryMaxLevel && xp >= HeroMasteryCumulativeXp(lv + 1)) lv++;
            return lv;
        }

        // 次のLvまでに必要な残りXP表示用。最大Lvなら0。
        public int GetHeroMasteryXpToNext(string heroId)
        {
            int lv = GetHeroMasteryLevel(heroId);
            if (lv >= HeroMasteryMaxLevel) return 0;
            return HeroMasteryCumulativeXp(lv + 1) - GetHeroMasteryXp(heroId);
        }

        // 現Lv帯に入ってからの進捗XP（ゲージ用）。
        public int GetHeroMasteryXpIntoLevel(string heroId)
        {
            int lv = GetHeroMasteryLevel(heroId);
            return Mathf.Max(0, GetHeroMasteryXp(heroId) - HeroMasteryCumulativeXp(lv));
        }

        // 現Lvから次Lvへ上がるのに必要なXP（ゲージの分母）。最大Lvなら0。
        public int GetHeroMasteryXpForCurrentLevel(string heroId)
        {
            int lv = GetHeroMasteryLevel(heroId);
            if (lv >= HeroMasteryMaxLevel) return 0;
            return HeroMasteryXpForLevelUp(lv);
        }

        // 熟練度Lvによる基礎ステ倍率。GameManager の HeroMetaBaseMultiplier に合算する。
        public float GetHeroMasteryStatMultiplier(string heroId)
        {
            return 1f + HeroMasteryPerLevel * (GetHeroMasteryLevel(heroId) - 1);
        }

        // チャプタークリア等で熟練度XPを付与し、上がった新Lvを返す。
        public int AddHeroMasteryXp(string heroId, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(heroId)) return GetHeroMasteryLevel(heroId);
            HeroMasteryRecord rec = GetHeroMastery(heroId, true);
            rec.masteryXp = Mathf.Max(0, rec.masteryXp + amount);
            Save();
            return GetHeroMasteryLevel(heroId);
        }

        // 必殺バリアント（0=基本/1=A/2=B）が熟練度Lvで解放済みか。
        public bool IsHeroUltVariantUnlocked(string heroId, int variant)
        {
            if (variant <= 0) return true;
            int lv = GetHeroMasteryLevel(heroId);
            if (variant == 1) return lv >= HeroUltAUnlockLevel;
            if (variant == 2) return lv >= HeroUltBUnlockLevel;
            return false;
        }

        // 装備中の必殺バリアント（未解放を保存していたら基本へフォールバック）。
        public int GetHeroEquippedUlt(string heroId)
        {
            HeroMasteryRecord rec = GetHeroMastery(heroId, false);
            int v = rec != null ? Mathf.Clamp(rec.equippedUlt, 0, 2) : 0;
            return IsHeroUltVariantUnlocked(heroId, v) ? v : 0;
        }

        // 必殺バリアントを装備（解放済みのみ）。成功で true。
        public bool SetHeroEquippedUlt(string heroId, int variant)
        {
            variant = Mathf.Clamp(variant, 0, 2);
            if (!IsHeroUltVariantUnlocked(heroId, variant)) return false;
            HeroMasteryRecord rec = GetHeroMastery(heroId, true);
            rec.equippedUlt = variant;
            Save();
            return true;
        }
    }
}
