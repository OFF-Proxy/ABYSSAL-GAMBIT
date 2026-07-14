using System;
using System.IO;
using UnityEngine;

namespace AutoChessBossRush.Save
{
    public class LocalJsonSaveStore : ISaveStore
    {
        private const string DefaultFileName = "save.json";
        private const string TempSuffix = ".tmp";

        private readonly string filePath;
        private readonly string tempPath;
        private readonly string corruptPath;

        public LocalJsonSaveStore() : this(Application.persistentDataPath, DefaultFileName)
        {
        }

        public LocalJsonSaveStore(string baseDirectory) : this(baseDirectory, DefaultFileName)
        {
        }

        // セーブスロット対応：ファイル名を指定して別ファイルに保存できる（例: save_0.json）。
        public LocalJsonSaveStore(string baseDirectory, string fileName)
        {
            filePath = Path.Combine(baseDirectory, fileName);
            tempPath = filePath + TempSuffix;
            corruptPath = filePath + ".corrupt";
        }

        public bool Exists() => File.Exists(filePath);

        // セーブスロット削除：本体・一時・破損退避ファイルを消す。
        public void Delete()
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch (Exception e) { Debug.LogWarning($"[SaveStore] Delete failed: {e.Message}"); }
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            try { if (File.Exists(corruptPath)) File.Delete(corruptPath); } catch { }
        }

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
