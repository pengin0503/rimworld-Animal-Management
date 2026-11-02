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
        int selected, days;
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
                source=snapshot;days=settings.TargetDays;
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
        static void DrawProduction(Listing_Standard list,FeedForecast forecast,FeedAnchor anchor,FeedActionPlan plan)
        {
            list.Label("AM_ProductionConditions".Translate());
            list.Label("AM_HayConditions".Translate());
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
