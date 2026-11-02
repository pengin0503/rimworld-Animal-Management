using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Pengin.AnimalManagement;
using Verse;
class Program
{
    static ManagedFeedBill LoadEntry(string node)
    {
        var entry=new ManagedFeedBill();
        Scribe.EnterNode(node);try{entry.ExposeData();}finally{Scribe.ExitNode();}
        return entry;
    }
    static void Main()
    {
        _=ParseHelper.FromString<string>("fixture");
        // Real default PrefsData for DeepProfiler; no engine or playable-map initialization.
        typeof(Prefs).GetField("data",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new PrefsData());
        string path=Path.Combine(Path.GetTempPath(),"am-"+Guid.NewGuid()+".xml");
        try
        {
            var settings=new AnimalSettings {WarnDays=9,IncludeForbidden=true,PopulationScenario=true,Births=4,Deaths=2,TargetDays=30,ProductionMode=ActionMode.Auto,MaxItems=800};
            Scribe.saver.InitSaving(path,"fixture");settings.ExposeData();Scribe.saver.FinalizeSaving();
            settings=new AnimalSettings();
            Scribe.loader.InitLoading(path);settings.ExposeData();Scribe.loader.FinalizeLoading();
            if(settings.WarnDays!=9||!settings.IncludeForbidden||!settings.PopulationScenario||settings.Births!=4||settings.Deaths!=2||settings.TargetDays!=30||settings.ProductionMode!=ActionMode.Auto||settings.MaxItems!=800)
                throw new Exception("Animal settings Scribe round trip failed");
            Console.WriteLine("PASS real-game Scribe animal settings round trip (not map save/load)");
            // #21: managed bill entry, new fields and a 1.1.0 entry without them.
            var entry=new ManagedFeedBill{BillId="Bill_7",Fingerprint="v2|x",Expires=123,Automatic=true,Permission="p",Batches=3,LastSeen=2,StopState=(int)OwnedBillState.Expired};
            var legacy=new ManagedFeedBill{BillId="Bill_8",Fingerprint="a=1|repeatCount=4|repeatMode=RepeatCount|index=0",Expires=9};
            Scribe.saver.InitSaving(path,"fixture");Scribe_Deep.Look(ref entry,"entry");Scribe_Deep.Look(ref legacy,"legacy");Scribe.saver.FinalizeSaving();
            string xml=File.ReadAllText(path).Replace("<batches>-1</batches>","").Replace("<lastSeenRepeatCount>-1</lastSeenRepeatCount>","");
            File.WriteAllText(path,xml);
            entry=null;legacy=null;
            // Real Scribe LoadingVars and PostLoadInit passes. ScribeExtractor's BackCompatibility
            // type lookup and cross-reference resolution need a running game and are skipped.
            Scribe.loader.InitLoading(path);entry=LoadEntry("entry");legacy=LoadEntry("legacy");
            Scribe.mode=LoadSaveMode.PostLoadInit;entry.ExposeData();legacy.ExposeData();Scribe.loader.ForceStop();
            if(entry.BillId!="Bill_7"||entry.Batches!=3||entry.LastSeen!=2||entry.StopState!=(int)OwnedBillState.Expired||legacy.StopState!=-1||!entry.Automatic||entry.Expires!=123)throw new Exception("managed bill round trip failed");
            if(legacy.Batches!=4||legacy.LastSeen!=4||legacy.Expires!=9)throw new Exception("legacy managed bill progress not recovered");
            Console.WriteLine("PASS real-game Scribe managed bill round trip incl. 1.1.0 entry");
            // The fingerprint excludes exactly the fields vanilla changes during normal work.
            var fields=typeof(RimWorld.Bill_Production).GetFields(BindingFlags.Public|BindingFlags.Instance).Select(f=>f.Name).ToList();
            foreach(string name in new[]{"repeatCount","paused","nextTickToSearchForIngredients","deleted","suspended","repeatMode","ingredientFilter"})
                if(!fields.Contains(name))throw new Exception("Bill_Production field missing: "+name);
            foreach(string name in new[]{"PawnRestriction","SlavesOnly","MechsOnly","NonMechsOnly"})
                if(typeof(RimWorld.Bill).GetProperty(name)==null)throw new Exception("Bill property missing: "+name);
            Console.WriteLine("PASS real-game Bill_Production field set matches the progress-insensitive fingerprint");
            // Real Bill_Production instance (no game: constructor needs UniqueIDsManager).
            var bill=(RimWorld.Bill_Production)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(RimWorld.Bill_Production));
            var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            typeof(RimWorld.Bill_Production).GetField("storeMode",flags).SetValue(bill,new RimWorld.BillStoreModeDef{defName="SpecificStockpile"});
            typeof(RimWorld.Bill_Production).GetField("playerCustomName",flags).SetValue(bill,"fixture");
            bill.repeatMode=new RimWorld.BillRepeatModeDef{defName="RepeatCount"};bill.ingredientFilter=new ThingFilter();bill.repeatCount=3;
            string owned=ManagedFeedBills.Fingerprint(bill);
            bill.repeatCount=2;bill.paused=true;bill.nextTickToSearchForIngredients=999;
            if(ManagedFeedBills.Fingerprint(bill)!=owned||!ManagedFeedBills.MatchesSettings(owned,bill))throw new Exception("vanilla progress changed the ownership fingerprint");
            bill.targetCount=77;
            if(ManagedFeedBills.MatchesSettings(owned,bill))throw new Exception("player setting change not detected");
            bill.targetCount=10;
            owned=ManagedFeedBills.Fingerprint(bill);
            typeof(RimWorld.Bill).GetField("slavesOnly",flags).SetValue(bill,true);
            if(ManagedFeedBills.MatchesSettings(owned,bill))throw new Exception("private pawn restriction change not detected");
            Console.WriteLine("PASS real-game Bill_Production fingerprint ignores progress and detects setting/restriction edits");
        }
        finally {if(File.Exists(path))File.Delete(path);}
    }
}
