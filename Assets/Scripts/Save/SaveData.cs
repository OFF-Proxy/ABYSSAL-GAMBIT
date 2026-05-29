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
    }
}
