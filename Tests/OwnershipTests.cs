using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Pengin.AnimalManagement;
static class OwnershipTests
{
    static int tests;
    static void Check(bool v,string name){if(!v)throw new Exception(name);tests++;}
    static OwnedBillState C(int lastSeen,int current,bool match=true,bool suspended=false,bool expired=false,bool authorized=true,bool bench=true,bool bill=true)
    {return ManagedBillPolicy.Classify(bench,bill,match,suspended,lastSeen,current,expired,authorized);}
    public static void Run()
    {
        Check(ManagedBillPolicy.CanCreate(ActionMode.Confirm,true,true,false,0,2,60000,0,60000),"confirmed safe creation");
        Check(!ManagedBillPolicy.CanCreate(ActionMode.Auto,false,true,false,0,2,60000,0,60000),"target permission required");
        Check(!ManagedBillPolicy.CanCreate(ActionMode.Auto,true,false,false,0,2,60000,0,60000),"partial forecast blocked");
        Check(!ManagedBillPolicy.CanCreate(ActionMode.Auto,true,true,true,0,2,60000,0,60000),"manual/duplicate bill protected");
        Check(!ManagedBillPolicy.CanCreate(ActionMode.Auto,true,true,false,2,2,60000,0,60000),"map operation cap");
        Check(!ManagedBillPolicy.CanCreate(ActionMode.Auto,true,true,false,0,2,59999,0,60000),"cooldown");
        Check(!ManagedBillPolicy.Owns("before","edited",false),"manual edits revoke ownership");
        Check(!ManagedBillPolicy.Owns("same","same",true),"suspended bill revokes ownership");

        // #21: 3 batches -> vanilla finishes them one by one -> normal end -> next day.
        int lastSeen=3;
        Check(C(lastSeen,3)==OwnedBillState.Owned,"new bill owned");
        foreach(int next in new[]{2,1,0})
        {
            Check(ManagedBillPolicy.IsProgress(lastSeen,next,true),"observed batch "+next+" is progress");
            lastSeen=next;
            if(next>0)Check(C(lastSeen,next)==OwnedBillState.Owned,"ownership continues at "+next);
        }
        var done=C(lastSeen,0);
        Check(done==OwnedBillState.Completed&&!ManagedBillPolicy.Revokes(done)&&ManagedBillPolicy.RemovesOwnBill(done),"natural completion releases without revocation and clears the 0x bill");
        Check(ManagedBillPolicy.CanCreate(ActionMode.Auto,true,true,false,0,2,120000,60000,60000),"next day's replenishment is allowed again");
        // Pre-fix behaviour reproduced: the progress-sensitive fingerprint changed on the first
        // batch, which counted as a manual edit and revoked the bench permanently.
        Check(!ManagedBillPolicy.Owns("repeatCount=3","repeatCount=2",false),"pre-fix: progress looked like an edit (reproduction)");
        // Natural expiry.
        var expired=C(2,2,expired:true);
        Check(expired==OwnedBillState.Expired&&!ManagedBillPolicy.Revokes(expired)&&ManagedBillPolicy.RemovesOwnBill(expired),"expiry releases without revocation");
        // Withdrawn authority (mode Off, permission removed, forecast unreliable).
        var withdrawn=C(2,2,authorized:false);
        Check(withdrawn==OwnedBillState.Withdrawn&&!ManagedBillPolicy.Revokes(withdrawn)&&ManagedBillPolicy.RemovesOwnBill(withdrawn),"withdrawn authority stops our bill only");
        // Manual interventions keep the safe side: never touched, bench revoked.
        foreach(var manual in new[]{C(3,3,match:false),C(3,5),C(3,1),C(3,3,suspended:true),C(3,-1,bill:false)})
            Check(ManagedBillPolicy.Revokes(manual)&&!ManagedBillPolicy.RemovesOwnBill(manual),"manual "+manual+" revokes and is left untouched");
        Check(C(3,3,match:false)==OwnedBillState.ManualEdit&&C(3,5)==OwnedBillState.ManualEdit,"edit or raised count is manual");
        Check(!ManagedBillPolicy.IsProgress(3,1,true)&&C(3,1)==OwnedBillState.Unverified,"skipping batches is not progress");
        Check(!ManagedBillPolicy.IsProgress(3,2,false)&&C(3,2)==OwnedBillState.Unverified,"unobserved decrement is not progress");
        Check(!ManagedBillPolicy.IsProgress(0,-1,true),"no progress below zero");
        Check(C(3,3,suspended:true)==OwnedBillState.Suspended&&C(3,-1,bill:false)==OwnedBillState.Missing,"suspend and delete distinguished");
        Check(C(-1,3)==OwnedBillState.Unverified,"unknown legacy progress is not trusted");
        // Manual intervention outranks expiry or completion.
        Check(C(0,0,suspended:true,expired:true)==OwnedBillState.Suspended&&C(1,1,match:false,expired:true)==OwnedBillState.ManualEdit,"manual state wins over natural end");
        var lost=C(3,3,bench:false);
        Check(lost==OwnedBillState.BenchLost&&!ManagedBillPolicy.Revokes(lost)&&!ManagedBillPolicy.RemovesOwnBill(lost),"lost bench is released untouched");

        // Save compatibility: 1.1.0 fingerprints embed progress and list position.
        string legacy="allowedSkillRange=0~20|deleted=False|paused=False|pauseWhenSatisfied=False|repeatCount=3|repeatMode=RepeatCount|suspended=False|Kibble|SpecificStockpile|index=0";
        string progressed=legacy.Replace("repeatCount=3","repeatCount=1").Replace("index=0","index=2");
        Check(ManagedBillPolicy.NormalizeLegacy(legacy)==ManagedBillPolicy.NormalizeLegacy(progressed),"legacy progress and position ignored");
        Check(ManagedBillPolicy.NormalizeLegacy(legacy)!=ManagedBillPolicy.NormalizeLegacy(legacy.Replace("repeatMode=RepeatCount","repeatMode=Forever")),"legacy settings still compared");
        Check(ManagedBillPolicy.NormalizeLegacy(legacy)!=ManagedBillPolicy.NormalizeLegacy(legacy.Replace("pauseWhenSatisfied=False","pauseWhenSatisfied=True")),"similar field names not stripped");
        Check(ManagedBillPolicy.LegacyRepeatCount(legacy)==3&&ManagedBillPolicy.LegacyRepeatCount("repeatMode=X")==-1&&ManagedBillPolicy.LegacyRepeatCount(null)==-1,"legacy count parsed");

        // Every reported release reason has English and Japanese text.
        string root=AppContext.BaseDirectory;
        while(root!=null&&!Directory.Exists(Path.Combine(root,"Languages")))root=Path.GetDirectoryName(root);
        if(root==null)root=Directory.GetCurrentDirectory();
        foreach(string language in new[]{"English","Japanese"})
        {
            var keys=XDocument.Load(Path.Combine(root,"Languages",language,"Keyed","Strings.xml")).Root.Elements().Select(e=>e.Name.LocalName).ToHashSet();
            foreach(OwnedBillState state in Enum.GetValues(typeof(OwnedBillState)))
                if(state!=OwnedBillState.Owned)Check(keys.Contains(ManagedBillPolicy.HistoryKey(state)),language+" "+state);
        }
        Console.WriteLine($"PASS {tests} production ownership lifecycle assertions (#21)");
    }
}
