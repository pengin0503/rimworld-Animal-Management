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
        public void ExposeData(){Scribe_References.Look(ref Bench,"bench");Scribe_Values.Look(ref BillId,"billId","");Scribe_Values.Look(ref Fingerprint,"fingerprint","");Scribe_Values.Look(ref Expires,"expires",0);Scribe_Values.Look(ref Automatic,"automatic",false);Scribe_Values.Look(ref Permission,"permission","");}
    }
    public sealed class AnimalActionState : MapComponent
    {
        public List<ManagedFeedBill> Managed=new List<ManagedFeedBill>();
        public List<string> Permissions=new List<string>();
        public List<string> Revoked=new List<string>(),History=new List<string>();
        public int LastAction=-180000;
        int nextAuto;
        bool faulted;
        public AnimalActionState(Map map):base(map){}
        public override void ExposeData()
        {
            base.ExposeData();Scribe_Collections.Look(ref Permissions,"feedPermissions",LookMode.Value);if(Permissions==null)Permissions=new List<string>();Scribe_Collections.Look(ref Managed,"managedFeedBills",LookMode.Deep);
            Scribe_Collections.Look(ref Revoked,"revokedFeedBenches",LookMode.Value);Scribe_Collections.Look(ref History,"feedActionHistory",LookMode.Value);
            Scribe_Values.Look(ref LastAction,"lastFeedAction",-180000);
            if(Managed==null)Managed=new List<ManagedFeedBill>();if(Revoked==null)Revoked=new List<string>();if(History==null)History=new List<string>();
        }
        public void AddHistory(string entry){History.Add(Find.TickManager.TicksGame+": "+entry);while(History.Count>20)History.RemoveAt(0);}
        public void CheckOwnership()
        {
            foreach(var entry in Managed.ToList())
            {
                var b=entry.Bench==null?null:entry.Bench.BillStack.Bills.OfType<Bill_Production>().FirstOrDefault(x=>x.GetUniqueLoadID()==entry.BillId);
                if(b!=null&&entry.Bench.Map==map&&ManagedBillPolicy.Owns(entry.Fingerprint,ManagedFeedBills.Fingerprint(b),b.suspended))
                {
                    var settings=AnimalManagementMod.Settings;int now=Find.TickManager.TicksGame;
                    var forecast=map.GetComponent<FeedForecast>();
                    if(settings!=null&&settings.ProductionMode!=ActionMode.Off&&(!entry.Automatic||(settings.ProductionMode==ActionMode.Auto&&Permissions.Contains(entry.Permission)))&&
                        !forecast.Failed&&!forecast.Snapshot.Partial&&forecast.Snapshot.Scenario!=null&&forecast.Snapshot.Scenario.Reliable&&now<entry.Expires&&b.ShouldDoNow())continue;
                    // A bounded assistance window; never reactivate a satisfied/expired instruction.
                    b.suspended=true;
                }
                if(entry.Bench!=null&&!Revoked.Contains(entry.Bench.ThingID))Revoked.Add(entry.Bench.ThingID);
                Managed.Remove(entry);AddHistory("ownership released: "+entry.BillId);
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
            if(now%250==map.uniqueID%250)CheckOwnership();
            if(s.ProductionMode!=ActionMode.Auto||now<nextAuto||(long)now-LastAction<s.Cooldown)return;
            nextAuto=now+60000;
            var f=map.GetComponent<FeedForecast>();f.Recalculate();var snap=f.Snapshot;
            if(f.Failed||snap.Partial||snap.Scenario==null||!snap.Scenario.Reliable)return;
            foreach(var anchor in snap.Anchors.Take(4))
            {
                var plan=FeedActionPlanner.Plan(snap.Scenario,anchor.Candidate,s.TargetDays,0,0,100000);
                if(!plan.Feasible||plan.RequiredItems==0)continue;
                foreach(var bench in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b=>Permissions.Contains(b.ThingID+"/"+anchor.Stack.ThingID)&&!Revoked.Contains(b.ThingID)).Take(4))
                {
                    var preview=ManagedFeedBills.Preview(map,f,anchor,plan,bench);
                    if(preview.Ready&&ManagedFeedBills.Execute(map,preview,true))return;
                }
            }
        }
    }
}
