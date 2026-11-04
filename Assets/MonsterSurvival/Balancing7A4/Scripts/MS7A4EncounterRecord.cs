using System;
using System.Collections.Generic;

// Serializable snapshot only. No SaveData, Currency or Enemy state changes.
[Serializable]
public sealed class MS7A4EncounterRecord
{
    public string bossName;
    public string mapName;
    public string sceneName;
    public string result;
    public string utcDate;
    public int wave;
    public float fightSeconds;
    public float bossInitialHP;
    public float bossDamageObserved;
    public int bossHitEvents;
    public float playerHPAtStart;
    public float playerHPLost;
    public int playerDamageEvents;
    public int goldEarned;
    public int goldNetChange;
    public int expEarned;
    public string note;
}

[Serializable]
public sealed class MS7A4EncounterHistory
{
    public List<MS7A4EncounterRecord> records = new List<MS7A4EncounterRecord>();
}
