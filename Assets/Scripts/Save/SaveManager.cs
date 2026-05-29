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
            if (store == null) store = new LocalJsonSaveStore();
            Data = store.Load();
            if (Data == null) Data = new SaveData();
        }

        public void SetStore(ISaveStore newStore)
        {
            store = newStore;
            Data = store != null ? store.Load() : new SaveData();
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
                    if (clampedStar > Data.bossAllies[i].starLevel)
                    {
                        Data.bossAllies[i].starLevel = clampedStar;
                        Save();
                    }
                    return;
                }
            }
            Data.bossAllies.Add(new BossAllyRecord
            {
                unitId = unitId,
                starLevel = clampedStar,
                acquiredUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            Save();
        }
    }
}
