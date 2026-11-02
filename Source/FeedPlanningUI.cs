using System;
using System.Linq;
using RimWorld;
using Verse;

namespace Pengin.AnimalManagement
{
    sealed class FeedPlanningUI
    {
        FeedActionPlan plan;
        ForecastSnapshot source;
        int selected, days, benchIndex;
        FeedProductionPreview preview;
        public void Draw(Listing_Standard list,FeedForecast forecast)
        {
            var settings=AnimalManagementMod.Settings;
            if(settings.SolutionsMode==ActionMode.Off&&settings.ProductionMode==ActionMode.Off)return;
            list.GapLine();list.Label("AM_Solutions".Translate());
            if(list.ButtonText("AM_TargetDays".Translate(settings.TargetDays)))
            {settings.TargetDays=settings.TargetDays==7?15:settings.TargetDays==15?30:7;plan=null;}
            var snapshot=forecast.Snapshot;
            if(snapshot.Scenario==null||snapshot.Anchors.Count==0)
            {list.Label("AM_NoAnchor".Translate());return;}
            selected=Math.Min(selected,snapshot.Anchors.Count-1);
            var anchor=snapshot.Anchors[selected];
            if(list.ButtonText("AM_FeedChoice".Translate(anchor.Stack.LabelShort)))
            {selected=(selected+1)%snapshot.Anchors.Count;plan=null;anchor=snapshot.Anchors[selected];}
            if(list.ButtonText("AM_CalculatePlan".Translate()))
            {
                int births=settings.PopulationScenario?settings.Births:0,deaths=settings.PopulationScenario?settings.Deaths:0;
                plan=FeedActionPlanner.Plan(snapshot.Scenario,anchor.Candidate,settings.TargetDays,births,deaths,100000);
                source=snapshot;days=settings.TargetDays;preview=null;
            }
            if(plan==null||source!=snapshot||days!=settings.TargetDays)return;
            list.Label("AM_PlanConditions".Translate());
            list.Label("AM_PlanDeficit".Translate(plan.Deficit.ToString("0.00")));
            list.Label(plan.Feasible?"AM_PlanCount".Translate(plan.RequiredItems,anchor.Stack.def.LabelCap,days):"AM_NoSolution".Translate());
            if(!plan.Reliable||forecast.Failed||snapshot.Partial)list.Label("AM_Provisional".Translate());
            if(list.ButtonText("AM_JumpFeed".Translate())&&anchor.Stack.Spawned)
                CameraJumper.TryJump(anchor.Stack.Position,anchor.Stack.Map);
            DrawProduction(list,forecast,anchor,plan);
            DrawCapacity(list,forecast);
        }
        void DrawProduction(Listing_Standard list,FeedForecast forecast,FeedAnchor anchor,FeedActionPlan plan)
        {
            list.Label("AM_ProductionConditions".Translate());
            list.Label("AM_HayConditions".Translate());
            var hay=DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Haygrass");
            if(anchor.Stack.def.defName=="Hay"&&hay!=null&&hay.plant!=null&&hay.plant.harvestYield>0&&plan.Feasible)
                list.Label("AM_HayArea".Translate(Math.Ceiling(plan.RequiredItems/hay.plant.harvestYield),hay.plant.growDays));
            var map=anchor.Stack.Map;if(map==null)return;
            var benches=map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Take(32).ToList();
            if(benches.Count==0)return;benchIndex=Math.Min(benchIndex,benches.Count-1);
            var bench=benches[benchIndex];
            if(list.ButtonText("AM_BenchChoice".Translate(bench.LabelShort)))
            {benchIndex=(benchIndex+1)%benches.Count;preview=null;bench=benches[benchIndex];}
            if(list.ButtonText("AM_PreviewProduction".Translate()))preview=ManagedFeedBills.Preview(map,forecast,anchor,plan,bench);
            if(preview==null||preview.Bench!=bench||preview.Anchor!=anchor)return;
            if(!preview.Ready){list.Label(preview.Blocked.Translate());return;}
            list.Label("AM_ProductionPreview".Translate(preview.Items,preview.Bench.LabelShort));
            foreach(var material in preview.Ingredients)list.Label(material.Key.LabelShort+" × "+material.Value);
            var state=map.GetComponent<AnimalActionState>();string permission=bench.ThingID+"/"+anchor.Stack.ThingID;
            if(list.ButtonText((state.Permissions.Contains(permission)?"AM_RevokeTarget":"AM_AllowTarget").Translate()))
            {
                if(state.Permissions.Contains(permission))state.Permissions.Remove(permission);
                else {state.Permissions.Add(permission);state.Revoked.Remove(bench.ThingID);}
            }
            if(list.ButtonText("AM_CreateProduction".Translate()))
            {
                var shown=preview;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("AM_ProductionConfirm".Translate(shown.Items,shown.Bench.LabelShort),
                    ()=>{if(!ManagedFeedBills.Execute(map,shown,false))Messages.Message("AM_ActionChanged".Translate(),MessageTypeDefOf.RejectInput,false);}));
            }
        }
        static void DrawCapacity(Listing_Standard list,FeedForecast forecast) { }
        public static void Settings(Listing_Standard list,AnimalSettings s)
        {
            if(list.ButtonText("AM_SolutionMode".Translate((("AM_Mode"+s.SolutionsMode).Translate()))))s.SolutionsMode=(ActionMode)(((int)s.SolutionsMode+1)%3);
            if(list.ButtonText("AM_ProductionMode".Translate((("AM_Mode"+s.ProductionMode).Translate()))))s.ProductionMode=(ActionMode)(((int)s.ProductionMode+1)%3);
            list.Label("AM_SafetyMargin".Translate((s.SafetyMargin*100).ToString("0")));s.SafetyMargin=list.Slider(s.SafetyMargin,0,.5f);
            list.Label("AM_MaxItems".Translate(s.MaxItems));s.MaxItems=(int)list.Slider(s.MaxItems,1,10000);
            list.Label("AM_MaxBills".Translate(s.MaxBills));s.MaxBills=(int)list.Slider(s.MaxBills,1,10);
            list.Label("AM_Cooldown".Translate((s.Cooldown/60000f).ToString("0.0")));s.Cooldown=(int)list.Slider(s.Cooldown,10000,180000);
        }
    }
}
