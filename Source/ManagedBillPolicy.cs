using System.Text.RegularExpressions;
namespace Pengin.AnimalManagement
{
    // Lifecycle of a mod-created feed bill (#21). Vanilla only changes repeatCount through
    // Bill_Production.Notify_IterationCompleted (one decrement per finished batch) and leaves
    // a finished RepeatCount bill in the stack at 0x; it never suspends or deletes it.
    public enum OwnedBillState
    {
        Owned,
        Completed,   // every batch observed finishing: released, bill removed, no revocation
        Expired,     // assistance window over: released, own unchanged bill removed, no revocation
        Withdrawn,   // mode/permission/forecast no longer authorize it: removed, no revocation
        BenchLost,   // bench gone or moved off the map: released untouched, no revocation
        ManualEdit,  // settings or repeat count changed by someone else: released untouched, revoked
        Unverified,  // count changed without an observed batch finishing: treated as manual
        Suspended,   // suspended by someone else: released untouched, revoked
        Missing      // bill deleted by someone else: released, revoked
    }
    public static class ManagedBillPolicy
    {
        public static bool CanCreate(ActionMode mode,bool permitted,bool reliable,bool duplicate,int active,int cap,int now,int last,int cooldown)
        {return mode!=ActionMode.Off&&(mode!=ActionMode.Auto||permitted)&&reliable&&!duplicate&&active<cap&&now>=last&&(long)now-last>=cooldown;}
        public static bool Owns(string expected,string current,bool suspended){return !suspended&&expected==current;}
        // Exactly one batch finished since the last observation, at a moment when a pawn was
        // working this bill at the bench. Anything else is not vanilla progress.
        public static bool IsProgress(int lastSeen,int current,bool workerObserved)
        {return workerObserved&&lastSeen>0&&current==lastSeen-1;}
        public static OwnedBillState Classify(bool benchPresent,bool billPresent,bool settingsMatch,bool suspended,int lastSeen,int current,bool expired,bool authorized)
        {
            if(!benchPresent)return OwnedBillState.BenchLost;
            if(!billPresent)return OwnedBillState.Missing;
            if(suspended)return OwnedBillState.Suspended;
            if(!settingsMatch)return OwnedBillState.ManualEdit;
            if(lastSeen<0)return OwnedBillState.Unverified;
            if(current>lastSeen)return OwnedBillState.ManualEdit;
            if(current!=lastSeen)return OwnedBillState.Unverified;
            if(current==0)return OwnedBillState.Completed;
            if(expired)return OwnedBillState.Expired;
            return authorized?OwnedBillState.Owned:OwnedBillState.Withdrawn;
        }
        public static bool Revokes(OwnedBillState s)
        {return s==OwnedBillState.ManualEdit||s==OwnedBillState.Unverified||s==OwnedBillState.Suspended||s==OwnedBillState.Missing;}
        // Only a bill whose settings still match ours and that nobody suspended is removed.
        public static bool RemovesOwnBill(OwnedBillState s)
        {return s==OwnedBillState.Completed||s==OwnedBillState.Expired||s==OwnedBillState.Withdrawn;}
        public static string HistoryKey(OwnedBillState s){return "AM_Bill"+s;}

        // Fingerprints from 1.1.0 saves included runtime progress (repeatCount, paused) and
        // the bill's list position. Strip exactly those tokens before comparing.
        public const string Version="v2|";
        static readonly Regex progress=new Regex(@"(^|\|)(paused|repeatCount)=[^|]*",RegexOptions.CultureInvariant);
        static readonly Regex position=new Regex(@"\|index=-?\d+$",RegexOptions.CultureInvariant);
        static readonly Regex count=new Regex(@"(?:^|\|)repeatCount=(\d+)(?:\||$)",RegexOptions.CultureInvariant);
        public static string NormalizeLegacy(string fingerprint)
        {
            if(fingerprint==null)return null;
            return progress.Replace(position.Replace(fingerprint,""),"$1");
        }
        public static int LegacyRepeatCount(string fingerprint)
        {
            if(fingerprint==null)return -1;
            var m=count.Match(fingerprint);int value;
            return m.Success&&int.TryParse(m.Groups[1].Value,out value)?value:-1;
        }
    }
}
