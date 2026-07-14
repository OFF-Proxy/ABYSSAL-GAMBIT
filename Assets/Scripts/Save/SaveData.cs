using System.Collections.Generic;

namespace AutoChessBossRush.Save
{
    [System.Serializable]
    public class SaveData
    {
        public int version = 1;
        public List<ChapterRecord> chapters = new List<ChapterRecord>();
        public List<BossAllyRecord> bossAllies = new List<BossAllyRecord>();
        public long lastSavedUnixSec = 0;
        // R3-hero-select: このスロットの主人公（ヒーロー）ID。空＝未選択（旧セーブ含む）。
        // 後方互換: JsonUtility は未知フィールドを既定値("")で埋めるため移行処理は不要。
        public string heroUnitId = "";
        // R3-hero-scale Phase2: 手動育成。heroPoints＝消費通貨（ラン終了で獲得）、heroMastery＝ヒーロー別の手動強化Lv。
        public int heroPoints = 0;
        public List<HeroMasteryRecord> heroMastery = new List<HeroMasteryRecord>();
        // STORY: 真偽フラグの汎用ストア（幕間の既読・スキン解放/選択など）。存在＝true。
        // 例: "int01"/"int08"/"int12"/"int13"（幕間既読）, "skin_kagachi_unlocked", "skin_kagachi_on"。
        public List<string> storyFlags = new List<string>();
        // R4-collection-hub: ショップ選抜。ここに入ったユニットIDは「ショップに出さない」（恒久）。
        // 空＝全部出す（既定）。後方互換: 旧セーブは未知フィールドが空で埋まるため移行不要。
        public List<string> shopDisabledUnitIds = new List<string>();
    }

    [System.Serializable]
    public class HeroMasteryRecord
    {
        public string heroUnitId;
        public int manualLevel = 0; // （旧）手動強化Lv。廃止。後方互換のため残置。
        // R3-hero-mastery: 熟練度。チャプタークリアで masteryXp が増え、Lvが上がると必殺バリアントが解放される。
        public int masteryXp = 0;
        public int equippedUlt = 0; // 装備中の必殺バリアント（0=基本/1=A/2=B）。
    }

    [System.Serializable]
    public class ChapterRecord
    {
        public int chapter;
        public bool cleared;
        public int bestScore;
        public float bestTimeSec;
        public int clearCount;
    }

    [System.Serializable]
    public class BossAllyRecord
    {
        public string unitId;
        public int starLevel = 1;
        public long acquiredUnixSec;
        // R1-collection: このボスを獲得（章クリア）した累計回数。育成（アフィニティ）レベルの基礎。
        public int recruitCount = 1;
    }
}
