using System;
using System.IO;
using UnityEngine;

namespace AutoChessBossRush.Save
{
    public class LocalJsonSaveStore : ISaveStore
    {
        private const string FileName = "save.json";
        private const string TempSuffix = ".tmp";
        private const string CorruptName = "save.corrupt.json";

        private readonly string filePath;
        private readonly string tempPath;
        private readonly string corruptPath;

        public LocalJsonSaveStore() : this(Application.persistentDataPath)
        {
        }

        public LocalJsonSaveStore(string baseDirectory)
        {
            filePath = Path.Combine(baseDirectory, FileName);
            tempPath = filePath + TempSuffix;
            corruptPath = Path.Combine(baseDirectory, CorruptName);
        }

        public bool Exists() => File.Exists(filePath);

        public SaveData Load()
        {
            if (!File.Exists(filePath))
                return new SaveData();

            try
            {
                string json = File.ReadAllText(filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null)
                    throw new Exception("JsonUtility returned null");
                if (data.chapters == null) data.chapters = new System.Collections.Generic.List<ChapterRecord>();
                if (data.bossAllies == null) data.bossAllies = new System.Collections.Generic.List<BossAllyRecord>();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveStore] Load failed ({e.Message}); quarantining to save.corrupt.json and starting fresh.");
                try
                {
                    if (File.Exists(corruptPath))
                        File.Delete(corruptPath);
                    File.Move(filePath, corruptPath);
                }
                catch (Exception moveError)
                {
                    Debug.LogWarning($"[SaveStore] Failed to quarantine corrupt save: {moveError.Message}");
                }
                return new SaveData();
            }
        }

        public void Save(SaveData data)
        {
            if (data == null) return;
            try
            {
                string json = JsonUtility.ToJson(data, false);
                File.WriteAllText(tempPath, json);
                if (File.Exists(filePath))
                    File.Replace(tempPath, filePath, null);
                else
                    File.Move(tempPath, filePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveStore] Save failed: {e.Message}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }
    }
}
