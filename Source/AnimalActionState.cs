using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace Pengin.AnimalManagement
{
    public sealed class ManagedFeedBill : IExposable
    {
        public Building_WorkTable Bench;
        public string BillId,Fingerprint;
        public int Expires;
        public bool Automatic;
        public string Permission;
        // Batches requested and the repeat count last confirmed as ours (-1: unknown).
        public int Batches=-1,LastSeen=-1;
        // Runtime only: the resolved bill and the pawn last seen working it at the bench.
        internal Bill_Production Bill;
        internal Pawn Worker;
        internal int WorkerTick=-1;
        public void ExposeData()
        {
            Scribe_References.Look(ref Bench,"bench");Scribe_Values.Look(ref BillId,"billId","");Scribe_Values.Look(ref Fingerprint,"fingerprint","");
            Scribe_Values.Look(ref Expires,"expires",0);Scribe_Values.Look(ref Automatic,"automatic",false);Scribe_Values.Look(ref Permission,"permission","");
            Scribe_Values.Look(ref Batches,"batches",-1);Scribe_Values.Look(ref LastSeen,"lastSeenRepeatCount",-1);
            // 1.1.0 entries stored the creation count only inside the fingerprint.
            if(Scribe.mode==LoadSaveMode.PostLoadInit&&LastSeen<0)
            {
                int legacy=ManagedBillPolicy.LegacyRepeatCount(Fingerprint);
                if(Batches<0)Batches=legacy;LastSeen=legacy;
            }
        }
    }
    public sealed class AnimalActionState : MapComponent
    {
        public List<ManagedFeedBill> Managed=new List<ManagedFeedBill>();
        public List<string> Permissions=new List<string>();
        public List<string> Revoked=new List<string>(),History=new List<string>();
        public int LastAction=-180000;
        public string LastAutoReason="AM_AutoNotEvaluated";
        void SetAutoReason(string reason)
        {
            if(LastAutoReason==reason)return;
            LastAutoReason=reason;AddHistory("auto status: "+reason);
        }
        int nextAuto;
        bool faulted;
        public AnimalActionState(Map map):base(map){}
        public override void ExposeData()
        {
            base.ExposeData();Scribe_Collections.Look(ref Permissions,"feedPermissions",LookMode.Value);if(Permissions==null)Permissions=new List<string>();Scribe_Collections.Look(ref Managed,"managedFeedBills",LookMode.Deep);
            Scribe_Collections.Look(ref Revoked,"revokedFeedBenches",LookMode.Value);Scribe_Collections.Look(ref History,"feedActionHistory",LookMode.Value);
            Scribe_Values.Look(ref LastAction,"lastFeedAction",-180000);
            Scribe_Values.Look(ref LastAutoReason,"lastAutoReason","AM_AutoNotEvaluated");
            if(Managed==null)Managed=new List<ManagedFeedBill>();if(Revoked==null)Revoked=new List<string>();if(History==null)History=new List<string>();
            Managed.RemoveAll(m=>m==null);
        }
        public void AddHistory(string entry){History.Add(Find.TickManager.TicksGame+": "+entry);while(History.Count>20)History.RemoveAt(0);}
        bool BenchPresent(ManagedFeedBill entry)
        {return entry.Bench!=null&&!entry.Bench.Destroyed&&entry.Bench.Spawned&&entry.Bench.Map==map;}
        Bill_Production Resolve(ManagedFeedBill entry)
        {
            if(entry.Bill!=null&&!entry.Bill.deleted&&entry.Bill.billStack==entry.Bench.BillStack)return entry.Bill;
            entry.Bill=entry.Bench.BillStack.Bills.OfType<Bill_Production>().FirstOrDefault(x=>x.GetUniqueLoadID()==entry.BillId);
            return entry.Bill;
        }
        // A batch is done standing on the bench's interaction cell; the worker is still there
        // in the tick its iteration completes, even if its job ended and another started.
        Pawn WorkingPawn(ManagedFeedBill entry,Bill_Production bill)
        {
            if(!entry.Bench.def.hasInteractionCell)return null;
            var things=entry.Bench.InteractionCell.GetThingList(map);
            for(int i=0;i<things.Count;i++)
            {
                var pawn=things[i] as Pawn;
                if(pawn!=null&&pawn.CurJob!=null&&pawn.CurJob.bill==bill)return pawn;
            }
            return null;
        }
        // Per tick, managed bills only (at most MaxBills): one field read and one cell lookup.
        // Records each vanilla batch completion as it happens so a later count change that
        // was not observed can be told apart from progress.
        void ObserveProgress(int now)
        {
            bool unexplained=false;
            for(int i=0;i<Managed.Count;i++)
            {
                var entry=Managed[i];
                if(!BenchPresent(entry))continue;
                var bill=Resolve(entry);if(bill==null)continue;
                var worker=WorkingPawn(entry,bill);
                if(bill.repeatCount!=entry.LastSeen)
                {
                    bool observed=worker!=null||(entry.Worker!=null&&now-entry.WorkerTick<=1&&entry.Worker.Spawned&&entry.Worker.Map==map&&entry.Worker.Position==entry.Bench.InteractionCell);
                    if(ManagedBillPolicy.IsProgress(entry.LastSeen,bill.repeatCount,observed))entry.LastSeen=bill.repeatCount;
                    else unexplained=true;
                }
                if(worker!=null){entry.Worker=worker;entry.WorkerTick=now;}
            }
            if(unexplained)CheckOwnership();
        }
        public void CheckOwnership()
        {
            var settings=AnimalManagementMod.Settings;int now=Find.TickManager.TicksGame;
            var forecast=map.GetComponent<FeedForecast>();
            bool reliable=forecast!=null&&!forecast.Failed&&!forecast.Snapshot.Partial&&forecast.Snapshot.Scenario!=null&&forecast.Snapshot.Scenario.Reliable;
            for(int i=Managed.Count-1;i>=0;i--)
            {
                var entry=Managed[i];
                bool bench=BenchPresent(entry);
                var bill=bench?Resolve(entry):null;
                bool authorized=settings!=null&&settings.ProductionMode!=ActionMode.Off&&
                    (!entry.Automatic||(settings.ProductionMode==ActionMode.Auto&&Permissions.Contains(entry.Permission)))&&reliable;
                var state=ManagedBillPolicy.Classify(bench,bill!=null,bill!=null&&ManagedFeedBills.MatchesSettings(entry.Fingerprint,bill),
                    bill!=null&&bill.suspended,entry.LastSeen,bill==null?-1:bill.repeatCount,now>=entry.Expires,authorized);
                if(state==OwnedBillState.Owned)
                {
                    // Upgrade a 1.1.0 fingerprint once it has been verified against the bill.
                    if(!entry.Fingerprint.StartsWith(ManagedBillPolicy.Version,StringComparison.Ordinal))entry.Fingerprint=ManagedFeedBills.Fingerprint(bill);
                    continue;
                }
                // Only our own, unchanged bill is removed; an edited, suspended or foreign
                // state is left exactly as the player or other mod set it.
                if(ManagedBillPolicy.RemovesOwnBill(state)&&bill!=null)entry.Bench.BillStack.Delete(bill);
                if(ManagedBillPolicy.Revokes(state)&&entry.Bench!=null&&!Revoked.Contains(entry.Bench.ThingID))Revoked.Add(entry.Bench.ThingID);
                Managed.RemoveAt(i);AddHistory(entry.BillId+" "+ManagedBillPolicy.HistoryKey(state));
            }
        }
        public override void MapComponentTick()
        {
            if(faulted)return;
            try{TickActions();}
            catch(Exception e){faulted=true;Log.Warning("Animal Management actions stopped: "+e.GetType().Name);}
        }
        void TickActions()
        {
            var s=AnimalManagementMod.Settings;if(s==null)return;int now=Find.TickManager.TicksGame;
            if(Managed.Count>0)ObserveProgress(now);
            if(now%250==map.uniqueID%250)CheckOwnership();
            if(s.ProductionMode!=ActionMode.Auto||now<nextAuto||(long)now-LastAction<s.Cooldown)return;
            nextAuto=now+60000;
            var f=map.GetComponent<FeedForecast>();f.Recalculate();var snap=f.Snapshot;
            if(f.Failed||snap.Partial||snap.Scenario==null)
            {SetAutoReason("AM_AutoIncomplete");return;}
            if(!snap.Scenario.Reliable)
            {SetAutoReason(snap.WeatherDependent?"AM_AutoWeather":"AM_AutoUncertain");return;}
            foreach(var anchor in snap.Anchors.Take(4))
            {
                var plan=FeedActionPlanner.Plan(snap.Scenario,anchor.Candidate,s.TargetDays,0,0,100000);
                if(!plan.Feasible||plan.RequiredItems==0)continue;
                foreach(var bench in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b=>Permissions.Contains(ManagedFeedBills.PermissionKey(b,anchor))&&!Revoked.Contains(b.ThingID)).Take(4))
                {
                    var preview=ManagedFeedBills.Preview(map,f,anchor,plan,bench);
                    if(preview.Ready&&ManagedFeedBills.Execute(map,preview,true))
                    {SetAutoReason("AM_AutoCreated");return;}
                }
            }
            SetAutoReason(snap.Anchors.Count==0?"AM_AutoNoAnchor":"AM_AutoNoSafeAction");
        }
    }
}
