using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7H.3 -- only uses 5A1's existing SaveData transaction and 7H2's confirmed counters.
// Offline UTC is a best-effort guard; a tampered future clock cannot be reliably authenticated.
public enum MS7H3QuestId { DailyVictory = 0, DailyGold = 1, WeeklyVictories = 2, WeeklyBosses = 3 }
public sealed class MS7H3QuestView
{
    public MS7H3QuestId id;
    public int current, target, reward;
    public bool claimed, completed;
    public string title, description;
}
public sealed class MS7H3Snapshot
{
    public int slot, bankGold, revision;
    public string message, dailyUntilUtc, weeklyUntilUtc;
    public bool valid, backupRecovery, clockRollback;
    public MS7H3QuestView[] quests = new MS7H3QuestView[4];
}

public static class MS7H3PeriodicQuestService
{
    private const string StatePrefix = "quest.7h3.v1."; // legacy read-only support
    private const string SafePrefix = "quest.7h3.v2."; // 5A-valid short IDs, no | or Base64
    private static bool transactionBusy;
    private sealed class State
    {
        public string day, week, lastRun = "";
        public int dw, dg, ww, wb, dClaim, wClaim, lastWins, lastBosses, lastGold;
        public long maxUtcTicks;
    }
    public static string Title(MS7H3QuestId id)
    {
        switch (id)
        {
            case MS7H3QuestId.DailyVictory: return "Daily Survivor";
            case MS7H3QuestId.DailyGold: return "Daily Collector";
            case MS7H3QuestId.WeeklyVictories: return "Weekly Veteran";
            default: return "Weekly Boss Hunter";
        }
    }
    public static string Description(MS7H3QuestId id)
    {
        switch (id)
        {
            case MS7H3QuestId.DailyVictory: return "Save 1 Victory today.";
            case MS7H3QuestId.DailyGold: return "Earn 150 Gold from winning runs today.";
            case MS7H3QuestId.WeeklyVictories: return "Save 5 Victories this UTC week.";
            default: return "Defeat 3 real Bosses in winning runs this week.";
        }
    }
    public static int Target(MS7H3QuestId id)
    {
        switch (id)
        {
            case MS7H3QuestId.DailyVictory: return 1;
            case MS7H3QuestId.DailyGold: return 150;
            case MS7H3QuestId.WeeklyVictories: return 5;
            default: return 3;
        }
    }
    public static int Reward(MS7H3QuestId id)
    {
        switch (id)
        {
            case MS7H3QuestId.DailyVictory: return 45;
            case MS7H3QuestId.DailyGold: return 35;
            case MS7H3QuestId.WeeklyVictories: return 180;
            default: return 140;
        }
    }
    private static string DayKey(DateTime utc) { return utc.ToUniversalTime().ToString("yyyyMMdd",CultureInfo.InvariantCulture); }
    private static DateTime WeekStart(DateTime utc)
    {
        utc = utc.ToUniversalTime().Date;
        int days = ((int)utc.DayOfWeek + 6) % 7;
        return utc.AddDays(-days);
    }
    private static string WeekKey(DateTime utc) { return DayKey(WeekStart(utc)); }
    private static string Encode(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? "")); }
    private static string Decode(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
    private static bool IsKnownId(MS7H3QuestId id) { return (int)id >= 0 && (int)id < 4; }
    private static int PosInt(string s)
    {
        int v;
        return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out v) ? v : -1;
    }
    // The 5A Save validator rejects the legacy 7H3 single ID with pipe ('|')
    // delimiters / Base64. Use FOUR short, alphanumeric-and-dot-only identifiers.
    // These are persisted together in one 5A TrySave; a partial set fails closed.
    private static string RunFingerprint(string runId)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(runId ?? ""));
            var sb = new StringBuilder(32);
            for (int i=0; i<16; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
    private static string RunToken(string runId)
    {
        return !string.IsNullOrEmpty(runId) && runId.StartsWith("hash:", StringComparison.Ordinal)
            ? runId.Substring(5) : RunFingerprint(runId);
    }
    private static bool SameRun(State state, string runId)
    {
        if (state == null || string.IsNullOrEmpty(runId)) return false;
        return state.lastRun == runId || state.lastRun == "hash:" + RunFingerprint(runId);
    }
    private static bool ValidDay(string value)
    {
        DateTime d;
        return value != null && value.Length == 8 &&
            DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    }
    private static bool ValidState(State s)
    {
        return s!=null && ValidDay(s.day) && ValidDay(s.week) &&
            s.dw>=0 && s.dg>=0 && s.ww>=0 && s.wb>=0 &&
            s.dClaim>=0 && s.dClaim<=3 && s.wClaim>=0 && s.wClaim<=3 &&
            s.lastWins>=0 && s.lastBosses>=0 && s.lastGold>=0 &&
            s.maxUtcTicks>=0 && s.lastRun!=null && s.lastRun.Length<=100;
    }
    private static bool IsHex32(string value)
    {
        if (value==null || value.Length!=32) return false;
        foreach(char c in value)
            if (!((c>='0' && c<='9') || (c>='a' && c<='f'))) return false;
        return true;
    }
    private static State ReadSafeState(MS5A1SaveData data)
    {
        string day=null, week=null, total=null, clock=null;
        int count=0;
        foreach (string id in data.unlockedContentIds)
        {
            if (id==null || !id.StartsWith(SafePrefix,StringComparison.Ordinal)) continue;
            count++;
            string tail=id.Substring(SafePrefix.Length);
            if (tail.StartsWith("d.",StringComparison.Ordinal)) { if(day!=null)return null;day=tail.Substring(2); }
            else if (tail.StartsWith("w.",StringComparison.Ordinal)) { if(week!=null)return null;week=tail.Substring(2); }
            else if (tail.StartsWith("t.",StringComparison.Ordinal)) { if(total!=null)return null;total=tail.Substring(2); }
            else if (tail.StartsWith("c.",StringComparison.Ordinal)) { if(clock!=null)return null;clock=tail.Substring(2); }
            else return null;
        }
        if (count!=4 || day==null || week==null || total==null || clock==null) return null;
        string[] d=day.Split('.'),w=week.Split('.'),t=total.Split('.');
        if(d.Length!=4 || w.Length!=4 || t.Length!=4) return null;
        State s=new State();
        s.day=d[0]; s.dw=PosInt(d[1]); s.dg=PosInt(d[2]); s.dClaim=PosInt(d[3]);
        s.week=w[0]; s.ww=PosInt(w[1]); s.wb=PosInt(w[2]); s.wClaim=PosInt(w[3]);
        s.lastWins=PosInt(t[0]); s.lastBosses=PosInt(t[1]); s.lastGold=PosInt(t[2]);
        if(!IsHex32(t[3])) return null;
        s.lastRun="hash:"+t[3];
        if(!long.TryParse(clock,NumberStyles.None,CultureInfo.InvariantCulture,out s.maxUtcTicks))return null;
        return ValidState(s)?s:null;
    }
    // Allow reading any old valid 7H3 state, but always WRITING the new safe format.
    private static State ReadLegacyState(MS5A1SaveData data)
    {
        string record = null;
        foreach (string id in data.unlockedContentIds)
        {
            if (id != null && id.StartsWith(StatePrefix, StringComparison.Ordinal))
            {
                if (record != null) return null; // ambiguous legacy data
                record = id.Substring(StatePrefix.Length);
            }
        }
        if (record == null) return null;
        try
        {
            string[] p = record.Split('|');
            if (p.Length != 14 || p[13] != "END") return null;
            State s = new State();
            s.day=p[0]; s.week=p[1];
            s.dw=PosInt(p[2]);s.dg=PosInt(p[3]);s.ww=PosInt(p[4]);s.wb=PosInt(p[5]);
            s.dClaim=PosInt(p[6]);s.wClaim=PosInt(p[7]);
            s.lastWins=PosInt(p[8]);s.lastBosses=PosInt(p[9]);s.lastGold=PosInt(p[10]);
            s.lastRun=Decode(p[11]);
            if(!long.TryParse(p[12],NumberStyles.None,CultureInfo.InvariantCulture,out s.maxUtcTicks))return null;
            return ValidState(s)?s:null;
        }
        catch { return null; }
    }
    private static State ReadState(MS5A1SaveData data)
    {
        if (data==null || data.unlockedContentIds==null) return null;
        foreach (string id in data.unlockedContentIds)
            if (id!=null && id.StartsWith(SafePrefix,StringComparison.Ordinal)) return ReadSafeState(data);
        return ReadLegacyState(data);
    }
    private static bool HasStateMarker(MS5A1SaveData data)
    {
        if (data==null || data.unlockedContentIds==null) return false;
        foreach(string id in data.unlockedContentIds)
            if(id!=null && (id.StartsWith(StatePrefix,StringComparison.Ordinal) ||
                              id.StartsWith(SafePrefix,StringComparison.Ordinal))) return true;
        return false;
    }
    private static void WriteState(MS5A1SaveData data, State s)
    {
        if (data.unlockedContentIds==null) data.unlockedContentIds=new List<string>();
        List<string> ids=data.unlockedContentIds;
        // Remove 7H3 markers only. All 7H1/7H2, achievements, gear and map IDs stay untouched.
        for(int i=ids.Count-1;i>=0;i--)
        {
            string id=ids[i];
            if(id!=null && (id.StartsWith(StatePrefix,StringComparison.Ordinal) ||
                            id.StartsWith(SafePrefix,StringComparison.Ordinal))) ids.RemoveAt(i);
        }
        string run=RunToken(s.lastRun);
        ids.Add(SafePrefix+"d."+s.day+"."+s.dw+"."+s.dg+"."+s.dClaim);
        ids.Add(SafePrefix+"w."+s.week+"."+s.ww+"."+s.wb+"."+s.wClaim);
        ids.Add(SafePrefix+"t."+s.lastWins+"."+s.lastBosses+"."+s.lastGold+"."+run);
        ids.Add(SafePrefix+"c."+s.maxUtcTicks.ToString(CultureInfo.InvariantCulture));
    }
    // Returns false when time is earlier than the last saved UTC instant by > 5 minutes.
    // No writes are performed by the read-only preview.
    private static bool Roll(State s, DateTime nowUtc, out string error)
    {
        error = "";
        nowUtc = nowUtc.ToUniversalTime();
        if (s.maxUtcTicks > 0 && nowUtc.Ticks < s.maxUtcTicks - TimeSpan.FromMinutes(5).Ticks)
        { error = "UTC clock moved backwards; claims and progress blocked"; return false; }
        string d = DayKey(nowUtc), w = WeekKey(nowUtc);
        if (string.CompareOrdinal(d,s.day) < 0 || string.CompareOrdinal(w,s.week) < 0)
        { error = "UTC period moved backwards; claims and progress blocked"; return false; }
        if (d != s.day) { s.day=d; s.dw=0; s.dg=0; s.dClaim=0; }
        if (w != s.week) { s.week=w; s.ww=0; s.wb=0; s.wClaim=0; }
        if (nowUtc.Ticks > s.maxUtcTicks) s.maxUtcTicks=nowUtc.Ticks;
        return true;
    }
    private static State Copy(State s)
    {
        return new State { day=s.day,week=s.week,lastRun=s.lastRun,dw=s.dw,dg=s.dg,ww=s.ww,wb=s.wb,
            dClaim=s.dClaim,wClaim=s.wClaim,lastWins=s.lastWins,lastBosses=s.lastBosses,lastGold=s.lastGold,
            maxUtcTicks=s.maxUtcTicks };
    }
    private static State Fresh(DateTime now, int baselineWins, int baselineBosses, int baselineGold)
    {
        return new State { day=DayKey(now),week=WeekKey(now),lastWins=baselineWins,
            lastBosses=baselineBosses,lastGold=baselineGold,maxUtcTicks=now.ToUniversalTime().Ticks };
    }
    private static int Current(State s, MS7H3QuestId id)
    {
        switch(id) { case MS7H3QuestId.DailyVictory: return s.dw;
            case MS7H3QuestId.DailyGold: return s.dg;
            case MS7H3QuestId.WeeklyVictories: return s.ww;
            default: return s.wb; }
    }
    private static bool Claimed(State s, MS7H3QuestId id)
    { return (((int)id<2 ? s.dClaim : s.wClaim) >> ((int)id%2) & 1) == 1; }
    // Pure in-memory. 7H2 must already have synchronized THIS same run.
    // Observer supplies counters captured BEFORE the run to prevent legacy backfill.
    public static bool TryApplyRunToData(MS5A1SaveData data, string runId,
        int beforeWins, int beforeBosses, int beforeGold, DateTime nowUtc, out string message)
    {
        message = "BLOCKED";
        if (data == null || string.IsNullOrEmpty(runId) || runId.Length>100 || data.lastCommittedRunId != runId ||
            beforeWins < 0 || beforeBosses<0 || beforeGold<0 || data.unlockedContentIds==null) return false;
        string latest7H2 = "quest.7h2.progress.last." + runId;
        if (!data.unlockedContentIds.Contains(latest7H2))
        { message = "7H2 has not saved this Victory"; return false; }
        int wins=MS7H2ProgressService.Wins(data), bosses=MS7H2ProgressService.Bosses(data), gold=MS7H2ProgressService.EarnedGold(data);
        if (wins-beforeWins != 1 || bosses-beforeBosses < 0 || bosses-beforeBosses > 1 || gold-beforeGold < 0)
        { message = "7H2 totals changed unexpectedly; no progress written"; return false; }
        State previous = ReadState(data);
        if (previous==null && HasStateMarker(data)) { message="Malformed 7H3 Save marker; inspect backup"; return false; }
        State s=previous==null ? Fresh(nowUtc,beforeWins,beforeBosses,beforeGold) : Copy(previous);
        string err;
        if (!Roll(s,nowUtc,out err)) { message=err; return false; }
        if (SameRun(s, runId)) { message="Run already recorded in 7H3"; return false; }
        if (previous!=null && (s.lastWins != beforeWins || s.lastBosses!=beforeBosses || s.lastGold!=beforeGold))
        {
            // An older committed run may have been missed during a scene transition.
            // Rebase only forwards; never grant any earlier wins, boss kills or Gold.
            if (s.lastWins > beforeWins || s.lastBosses > beforeBosses || s.lastGold > beforeGold)
            { message="7H3 counters ahead of baseline; recovery blocked"; return false; }
            s.lastWins=beforeWins; s.lastBosses=beforeBosses; s.lastGold=beforeGold;
        }
        int g=gold-beforeGold;
        if (s.dw==int.MaxValue || s.ww==int.MaxValue || s.dg > int.MaxValue-g ||
            (bosses>beforeBosses && s.wb==int.MaxValue)) { message="Progress overflow"; return false; }
        s.dw++; s.ww++; s.dg += g; s.wb += bosses-beforeBosses;
        s.lastWins=wins; s.lastBosses=bosses; s.lastGold=gold; s.lastRun=runId;
        WriteState(data,s); message="7H3 progress applied for committed run"; return true;
    }
    // Pure in-memory and atomic with 5A1 SaveData.bankGold.
    public static bool TryClaimToData(MS5A1SaveData data, MS7H3QuestId id, DateTime utc, out string message)
    {
        message="BLOCKED";
        if (data == null || !IsKnownId(id)) return false;
        State current = ReadState(data);
        if (current == null) { message="No valid 7H3 state/progress"; return false; }
        State s=Copy(current);
        if (!Roll(s,utc,out message)) return false;
        if (Current(s,id) < Target(id)) { message="Quest not completed"; return false; }
        if (Claimed(s,id)) { message="Already claimed in this period"; return false; }
        int reward=Reward(id);
        if (data.bankGold<0 || (long)data.bankGold+reward>int.MaxValue) { message="Gold overflow"; return false; }
        if ((int)id < 2) s.dClaim |= (1 << (int)id);
        else s.wClaim |= (1 << ((int)id-2));
        data.bankGold += reward; WriteState(data,s);
        message="Reward granted in SaveData; transaction still needs 5A1 TrySave";
        return true;
    }
    public static MS7H3Snapshot ReadSnapshot(int slot) { return ReadSnapshotAt(slot,DateTime.UtcNow); }
    private static MS7H3Snapshot ReadSnapshotAt(int slot, DateTime utc)
    {
        var snap=new MS7H3Snapshot { slot=slot, message="NO SAVE" };
        MS5A1SaveData data=null; string load=""; bool recovered=false;
        if (slot>=1 && slot<=3)
        {
            try { snap.valid=MS5A1SaveService.TryLoad(slot,out data,out load,out recovered) && data!=null;
                snap.backupRecovery=recovered; snap.message=snap.valid?(recovered?"BACKUP: claims blocked":"VALID"):"INVALID: "+load;
                if (snap.valid) { snap.bankGold=data.bankGold;snap.revision=data.revision; }
            } catch(Exception ex) { snap.message="READ ERROR: "+ex.Message; snap.valid=false; }
        }
        State s= snap.valid ? ReadState(data) : null;
        if (snap.valid && HasStateMarker(data) && s == null) { snap.message="INVALID 7H3 MARKER";snap.valid=false; }
        if (s != null)
        {
            s=Copy(s); string error;
            if (!Roll(s,utc,out error)) { snap.clockRollback=true;snap.message=error; }
        }
        if (s==null) s=Fresh(utc,0,0,0);
        DateTime day=utc.ToUniversalTime().Date;
        snap.dailyUntilUtc=day.AddDays(1).ToString("yyyy-MM-dd HH:mm 'UTC'");
        snap.weeklyUntilUtc=WeekStart(utc).AddDays(7).ToString("yyyy-MM-dd HH:mm 'UTC'");
        for(int i=0;i<4;i++)
        {
            var id=(MS7H3QuestId)i;
            snap.quests[i]=new MS7H3QuestView { id=id,title=Title(id),description=Description(id),
                current=Current(s,id),target=Target(id),reward=Reward(id),
                claimed=Claimed(s,id), completed=Current(s,id)>=Target(id) };
        }
        return snap;
    }
    public static bool TrySyncVictory(int slot, string runId, int oldWins, int oldBosses, int oldGold, out string message)
    {
        message="BLOCKED";
        if (transactionBusy) { message="7H3 transaction busy"; return false; }
        if (!Application.isPlaying || SceneManager.GetActiveScene().name!="SampleScene" ||
            MS5A2GoldBank.SelectedSlot!=slot || slot<1 || slot>3) return false;
        transactionBusy=true;
        try
        {
            MS5A1SaveData data;string info;bool recovered;
            if (!MS5A1SaveService.TryLoad(slot,out data,out info,out recovered) || data==null || recovered)
            { message="Invalid/backup-only Save: "+info; return false; }
            if (!TryApplyRunToData(data,runId,oldWins,oldBosses,oldGold,DateTime.UtcNow,out message)) return false;
            MS5A1SaveData saved;string result;
            if (!MS5A1SaveService.TrySave(slot,data,out saved,out result) || saved==null)
            { message="Progress Save FAILED: "+result; return false; }
            State s=ReadState(saved);
            if (s==null || !SameRun(s,runId)) { message="Save verification failed";return false; }
            message="7H3 SAVED Profile "+slot+" run="+runId+" dailyWins="+s.dw+" weeklyWins="+s.ww+" Rev="+saved.revision;
            return true;
        }
        catch(Exception ex) {message="SYNC EXCEPTION: "+ex.Message;Debug.LogException(ex);return false;}
        finally {transactionBusy=false;}
    }

// 7H.3.3: USER-INITIATED recovery of the single LATEST 5A/7H2 committed Victory.
// ONLY in MainMenu Play Mode; caller must show a confirmation and back up SaveData.
// When a reliable previous 7H3 baseline is unavailable, credit exactly one win,
// but never guess the boss/gold amount of historical runs.
public static bool TryRecoverLatestCommittedVictory(int slot, out string message)
{
    message="BLOCKED: not started";
    if (transactionBusy) { message="Another 7H3 transaction active";return false; }
    if (!Application.isPlaying || SceneManager.GetActiveScene().name!="MainMenu" ||
        slot<1 || slot>3 || MS5A2GoldBank.SelectedSlot!=slot)
    { message="Recovery allowed only in active MainMenu and selected Profile";return false; }
    transactionBusy=true;
    try
    {
        MS5A1SaveData data=null; string info; bool recovered;
        if (!MS5A1SaveService.TryLoad(slot,out data,out info,out recovered) || data==null || recovered)
        { message="Missing or backup-only Save: "+info;return false; }
        string runId=data.lastCommittedRunId;
        if (string.IsNullOrEmpty(runId) || runId.Length>100 || data.unlockedContentIds==null ||
            !data.unlockedContentIds.Contains("quest.7h2.progress.last."+runId))
        { message="Latest 5A Run ID was not verified by 7H2; no recovery";return false; }
        State s=ReadState(data);
        if (s==null && HasStateMarker(data))
        { message="Invalid 7H3 marker; manual recovery blocked";return false; }
        if (SameRun(s,runId))
        { message="Run already recorded in 7H3";return false; }
        int wins=MS7H2ProgressService.Wins(data);
        int bosses=MS7H2ProgressService.Bosses(data);
        int gold=MS7H2ProgressService.EarnedGold(data);
        if (wins<=0) { message="No verified wins in 7H2";return false; }
        int beforeWins=wins-1;
        int beforeBosses=bosses;  // conservative default: no invented Boss kill
        int beforeGold=gold;      // conservative default: no invented earned Gold
        if (s!=null)
        {
            if (s.lastWins>=wins || s.lastBosses>bosses || s.lastGold>gold)
            { message="7H3/7H2 totals inconsistent; no recovery";return false; }
            if (s.lastWins==beforeWins)
            {
                // The last saved 7H3 checkpoint is exact: real deltas are provable.
                beforeBosses=s.lastBosses;
                beforeGold=s.lastGold;
            }
        }
        if (!TryApplyRunToData(data,runId,beforeWins,beforeBosses,beforeGold,DateTime.UtcNow,out message))
            return false;
        MS5A1SaveData saved;string saveResult;
        if (!MS5A1SaveService.TrySave(slot,data,out saved,out saveResult) || saved==null)
        { message="7H3 recovery save failed: "+saveResult;return false; }
        State verify=ReadState(saved);
        if (verify==null || !SameRun(verify,runId))
        { message="Recovery saved, but verification failed; inspect backup";return false; }
        message="RECOVERED latest verified Victory (1 win only unless an exact prior checkpoint existed), Profile="+
            slot+" DailyWins="+verify.dw+" WeeklyWins="+verify.ww+" Rev="+saved.revision;
        Debug.Log("[7H3.3] "+message);
        return true;
    }
    catch (Exception ex) { message="RECOVERY EXCEPTION: "+ex.Message;Debug.LogException(ex);return false; }
    finally { transactionBusy=false; }
}

    public static bool TryClaim(int slot, MS7H3QuestId id, out string message)
    {
        message="BLOCKED";
        if (transactionBusy) { message="7H3 transaction busy";return false; }
        if (!Application.isPlaying || SceneManager.GetActiveScene().name!="MainMenu" ||
            slot<1 || slot>3 || MS5A2GoldBank.SelectedSlot!=slot || !IsKnownId(id)) return false;
        transactionBusy=true;
        try
        {
            MS5A1SaveData data;string info;bool recovered;
            if (!MS5A1SaveService.TryLoad(slot,out data,out info,out recovered) || data==null || recovered)
            { message="Invalid/backup-only Save: "+info;return false; }
            if (!TryClaimToData(data,id,DateTime.UtcNow,out message)) return false;
            MS5A1SaveData saved;string result;
            if (!MS5A1SaveService.TrySave(slot,data,out saved,out result) || saved==null)
            { message="Claim Save FAILED: "+result;return false; }
            State s=ReadState(saved);
            if(s==null || !Claimed(s,id)) { message="Claim Save verification failed";return false; }
            message="CLAIMED +"+Reward(id)+" Gold / Profile "+slot+" / Rev "+saved.revision;
            Debug.Log("[7H3] "+message);return true;
        }
        catch(Exception ex) {message="CLAIM EXCEPTION: "+ex.Message;Debug.LogException(ex);return false;}
        finally {transactionBusy=false;}
    }
    // Pure tests exercise no disk I/O; must not be presented as runtime validation.
    public static string PureRuleTests()
    {
        int pass=0, fail=0;var sb=new StringBuilder("7H3 PURE RULE TESTS - IN MEMORY ONLY\n");
        Action<bool,string> check=(ok,description)=>{if(ok)pass++;else fail++;sb.AppendLine((ok?"[PASS] ":"[FAIL] ")+description);};
        DateTime monday=new DateTime(2026,11,2,12,0,0,DateTimeKind.Utc);
        var d=new MS5A1SaveData();d.bankGold=100; d.lastCommittedRunId="A";
        d.unlockedContentIds=new List<string>{"quest.7h2.progress.last.A"};
        string message;
        // Intentionally start from 7H2 baseline 7/5/1000 to ensure no legacy backfill.
        check(TryApplyRunToData(d,"A",7,5,1000,monday,out message)==false,"Reject run if 7H2 counters not yet incremented");
        d.unlockedContentIds.Add("quest.7h2.progress.wins.8");
        d.unlockedContentIds.Add("quest.7h2.progress.bosses.6");
        d.unlockedContentIds.Add("quest.7h2.progress.gold.1180");
        check(TryApplyRunToData(d,"A",7,5,1000,monday,out message),"Record first verified run");
        var s=ReadState(d);check(s!=null && s.dw==1 && s.ww==1 && s.dg==180 && s.wb==1,"Daily/weekly counters reflect ONLY delta");
        int safeMarkers=0; bool safeIds=true;
        foreach(string id in d.unlockedContentIds)
        {
            if(id==null || !id.StartsWith(SafePrefix,StringComparison.Ordinal)) continue;
            safeMarkers++;
            if(id.Length>80) safeIds=false;
            foreach(char c in id)
                if (!((c>='a'&&c<='z')||(c>='0'&&c<='9')||c=='.'))safeIds=false;
        }
        check(safeMarkers==4 && safeIds,"7H3 writes four 5A-safe short ID markers (no pipe or Base64)");
        check(SameRun(s,"A") && !SameRun(s,"WRONG"),"Run fingerprint survives read/write without duplicate reward");
        check(!TryApplyRunToData(d,"A",7,5,1000,monday,out message),"Prevent repeated same-run sync");
        check(TryClaimToData(d,MS7H3QuestId.DailyVictory,monday,out message) && d.bankGold==145,"Daily Victory atomic Claim");
        check(!TryClaimToData(d,MS7H3QuestId.DailyVictory,monday,out message),"Double Claim blocked");
        check(TryClaimToData(d,MS7H3QuestId.DailyGold,monday,out message) && d.bankGold==180,"Daily Gold Claim");
        check(!TryClaimToData(d,MS7H3QuestId.WeeklyBosses,monday,out message),"Weekly 3-boss target not met");
        var other=new MS5A1SaveData();other.bankGold=10;
        check(!TryClaimToData(other,MS7H3QuestId.DailyVictory,monday,out message),"Empty Profile has no claimable quest");
        check(!TryClaimToData(d,MS7H3QuestId.DailyVictory,monday.AddHours(-2),out message),"UTC clock rollback blocks Claim");
        s=ReadState(d);check(s!=null && s.dClaim==3 && s.wClaim==0,"Claim bitmask persisted");
        // A day later, old daily claim must reset; weekly counters remain.
        d.lastCommittedRunId="B";
        for(int i=d.unlockedContentIds.Count-1;i>=0;i--)
            if(d.unlockedContentIds[i].StartsWith("quest.7h2.progress.")) d.unlockedContentIds.RemoveAt(i);
        d.unlockedContentIds.Add("quest.7h2.progress.last.B");
        d.unlockedContentIds.Add("quest.7h2.progress.wins.9");
        d.unlockedContentIds.Add("quest.7h2.progress.bosses.6");
        d.unlockedContentIds.Add("quest.7h2.progress.gold.1220");
        check(TryApplyRunToData(d,"B",8,6,1180,monday.AddDays(1),out message),"Next UTC day recorded");
        s=ReadState(d);check(s.dw==1 && s.dg==40 && s.dClaim==0 && s.ww==2,"Daily reset retains weekly totals");
        check(!TryClaimToData(d,MS7H3QuestId.DailyGold,monday.AddDays(1),out message),"Old day reward cannot be reclaimed");
        check(!TryApplyRunToData(d,"invalid",8,6,1180,monday,out message),"Wrong committed run blocked");
        // Following Monday: weekly counters/claims reset, only new committed Victory counts.
        d.lastCommittedRunId="C";
        for(int i=d.unlockedContentIds.Count-1;i>=0;i--)
            if(d.unlockedContentIds[i].StartsWith("quest.7h2.progress.")) d.unlockedContentIds.RemoveAt(i);
        d.unlockedContentIds.Add("quest.7h2.progress.last.C");
        d.unlockedContentIds.Add("quest.7h2.progress.wins.10");
        d.unlockedContentIds.Add("quest.7h2.progress.bosses.7");
        d.unlockedContentIds.Add("quest.7h2.progress.gold.1420");
        check(TryApplyRunToData(d,"C",9,6,1220,monday.AddDays(7),out message),"Next UTC week recorded");
        s=ReadState(d);
        check(s.ww==1 && s.wb==1 && s.dw==1 && s.dg==200 && s.dClaim==0 && s.wClaim==0,
            "Weekly reset retains only new period progress");
        check(TryClaimToData(d,MS7H3QuestId.DailyGold,monday.AddDays(7),out message) && d.bankGold==215,
            "New day/week can claim newly earned daily reward");
        sb.AppendLine("PURE PASS="+pass+" FAIL="+fail+" (no real Save modified)");
        return sb.ToString();
    }
}
