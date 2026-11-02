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
        HerdCapacityPlan capacity;
        int speciesIndex;
        ThingDef capacitySpecies;
        ForecastSnapshot capacitySource;
        FeedActionPlan hypotheticalPlan;
        ThingDef hypotheticalDef;
        ForecastSnapshot hypotheticalSource;
        int hypotheticalDays;
        public void Draw(Listing_Standard list,FeedForecast forecast)
        {
            var settings=AnimalManagementMod.Settings;
            if(settings.SolutionsMode==ActionMode.Off&&settings.ProductionMode==ActionMode.Off&&settings.CapacityMode==ActionMode.Off)return;
            list.GapLine();list.Label("AM_Solutions".Translate());
            if(list.ButtonText("AM_TargetDays".Translate(settings.TargetDays)))
            {settings.TargetDays=settings.TargetDays==7?15:settings.TargetDays==15?30:7;plan=null;capacity=null;}
            var snapshot=forecast.Snapshot;
            if(snapshot.Scenario==null)
            {
                list.Label("AM_NoAnchor".Translate());
            }
            else if(snapshot.Anchors.Count==0)
            {
                // A missing anchor is not grounds for hiding the independent herd plan.
                // A diet-only scenario is advisory: location and accessibility are unknown.
                list.Label("AM_NoAnchor".Translate());
                if(settings.SolutionsMode!=ActionMode.Off&&list.ButtonText("AM_CalculatePlan".Translate()))
                {
                    var def=DefDatabase<ThingDef>.GetNamedSilentFail("Hay")??DefDatabase<ThingDef>.GetNamedSilentFail("Kibble");
                    if(def!=null&&def.statBases!=null)
                    {
                        var nutrition=def.statBases.FirstOrDefault(s=>s.stat==StatDefOf.Nutrition);
                        if(nutrition!=null&&nutrition.value>0&&snapshot.Consumers!=null)
                        {
                            var access=snapshot.Consumers.Select(p=>p.RaceProps.CanEverEat(def)&&FoodUtility.WillEat(p,def,p)).ToArray();
                            var candidate=new FeedCandidate(nutrition.value,access,int.MaxValue);
                            hypotheticalPlan=FeedActionPlanner.Plan(snapshot.Scenario,candidate,settings.TargetDays,
                                settings.PopulationScenario?settings.Births:0,settings.PopulationScenario?settings.Deaths:0,100000);
                            hypotheticalDef=def;hypotheticalSource=snapshot;hypotheticalDays=settings.TargetDays;
                        }
                    }
                }
                if(hypotheticalPlan!=null&&hypotheticalSource==snapshot&&hypotheticalDays==settings.TargetDays)
                    list.Label("AM_HypotheticalPlan".Translate(hypotheticalDef.LabelCap,
                        hypotheticalPlan.Feasible?hypotheticalPlan.RequiredItems.ToString():"?",hypotheticalDays));
                list.Label("AM_Provisional".Translate());
            }
            else
            {
                selected=Math.Min(selected,snapshot.Anchors.Count-1);
                var anchor=snapshot.Anchors[selected];
                if(list.ButtonText("AM_FeedChoice".Translate(anchor.Stack.LabelShort)))
                {selected=(selected+1)%snapshot.Anchors.Count;plan=null;anchor=snapshot.Anchors[selected];}
                if((settings.SolutionsMode==ActionMode.Auto&&(source!=snapshot||days!=settings.TargetDays))||list.ButtonText("AM_CalculatePlan".Translate()))
                {
                    int births=settings.PopulationScenario?settings.Births:0,deaths=settings.PopulationScenario?settings.Deaths:0;
                    plan=FeedActionPlanner.Plan(snapshot.Scenario,anchor.Candidate,settings.TargetDays,births,deaths,100000);
                    source=snapshot;days=settings.TargetDays;preview=null;
                }
                if(plan!=null&&source==snapshot&&days==settings.TargetDays)
                {
                    list.Label("AM_PlanConditions".Translate());
                    list.Label("AM_PlanDeficit".Translate(plan.Deficit.ToString("0.00")));
                    list.Label(plan.Feasible?"AM_PlanCount".Translate(plan.RequiredItems,anchor.Stack.def.LabelCap,days):"AM_NoSolution".Translate());
                    if(!plan.Reliable||forecast.Failed||snapshot.Partial)list.Label("AM_Provisional".Translate());
                    if(list.ButtonText("AM_JumpFeed".Translate())&&anchor.Stack.Spawned)
                        CameraJumper.TryJump(anchor.Stack.Position,anchor.Stack.Map);
                    if(settings.ProductionMode!=ActionMode.Off)DrawProduction(list,forecast,anchor,plan);
                }
            }
            DrawCapacity(list,forecast);
            var state=forecast.map.GetComponent<AnimalActionState>();
            if(state!=null&&state.History.Count>0)
            {
                list.GapLine();list.Label("AM_ActionHistory".Translate());
                foreach(string entry in state.History)list.Label(entry);
                if(settings.ProductionMode==ActionMode.Auto)list.Label("AM_LastAutoStatus".Translate(state.LastAutoReason.Translate()));
            }
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

            var state=map.GetComponent<AnimalActionState>();string permission=ManagedFeedBills.PermissionKey(bench,anchor);
            if(list.ButtonText((state.Permissions.Contains(permission)?"AM_RevokeTarget":"AM_AllowTarget").Translate()))
            {
                if(state.Permissions.Contains(permission))state.Permissions.Remove(permission);
                else {state.Permissions.Add(permission);state.Revoked.Remove(bench.ThingID);}
            }
            if(!preview.Ready){list.Label(preview.Blocked.Translate());return;}
            list.Label("AM_ProductionPreview".Translate(preview.Items,preview.Bench.LabelShort));
            foreach(var material in preview.Ingredients)list.Label(material.Key.LabelShort+" × "+material.Value);
            if(list.ButtonText("AM_CreateProduction".Translate()))
            {
                var shown=preview;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("AM_ProductionConfirm".Translate(shown.Items,shown.Bench.LabelShort),
                    ()=>{if(!ManagedFeedBills.Execute(map,shown,false))Messages.Message("AM_ActionChanged".Translate(),MessageTypeDefOf.RejectInput,false);}));
            }
        }
        void DrawCapacity(Listing_Standard list,FeedForecast forecast)
        {
            var s=AnimalManagementMod.Settings;if(s.CapacityMode==ActionMode.Off)return;
            var snap=forecast.Snapshot;if(snap.Scenario==null||snap.Consumers==null)return;
            var species=snap.Consumers.Where(p=>p.IsColonyAnimal).Select(p=>p.def).Distinct().ToList();
            if(species.Count==0)return;speciesIndex=Math.Min(speciesIndex,species.Count-1);var def=species[speciesIndex];
            list.GapLine();list.Label("AM_CapacityConditions".Translate());
            if(list.ButtonText("AM_CapacitySpecies".Translate(def.LabelCap)))
            {speciesIndex=(speciesIndex+1)%species.Count;def=species[speciesIndex];capacity=null;}
            if((s.CapacityMode==ActionMode.Auto&&(capacitySource!=snap||capacitySpecies!=def||capacity==null||capacity.Horizon!=s.TargetDays))||list.ButtonText("AM_CalculateCapacity".Translate()))
            {
                capacity=HerdCapacityPlanner.Calculate(snap.Scenario,snap.Consumers.Select(p=>p.IsColonyAnimal&&p.def==def).ToArray(),s.TargetDays,400,
                    s.PopulationScenario?s.Births:0,s.PopulationScenario?s.Deaths:0);
                capacitySource=snap;capacitySpecies=def;
            }
            if(capacity==null||capacitySource!=snap||capacitySpecies!=def||capacity.Horizon!=s.TargetDays)return;
            if(!capacity.Feasible){list.Label("AM_CapacityUnsolved".Translate());return;}
            list.Label("AM_CapacityResult".Translate(capacity.Current,capacity.Sustainable,capacity.Horizon,Math.Max(0,capacity.Current-capacity.Sustainable)));
            if(capacity.AtLimit)list.Label("AM_CapacityBound".Translate());
            if(!capacity.Reliable)list.Label("AM_Provisional".Translate());
            if(!s.AllowSlaughter||!capacity.Reliable||forecast.Failed||snap.Partial)return;
            var map=forecast.map;var config=map.autoSlaughterManager.configs.FirstOrDefault(c=>c.animal==def);
            if(config==null)return;
            if(list.ButtonText("AM_SlaughterPreview".Translate(config.maxTotal,capacity.Sustainable)))
            {
                string before=SlaughterSignature(config);int target=capacity.Sustainable;int tick=snap.Tick;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("AM_SlaughterConfirm".Translate(def.LabelCap,config.maxTotal,target),()=>
                {
                    var state=map.GetComponent<AnimalActionState>();int now=Find.TickManager.TicksGame;
                    if(!s.AllowSlaughter||s.CapacityMode==ActionMode.Off||!map.autoSlaughterManager.configs.Contains(config)||
                        SlaughterSignature(config)!=before||forecast.Failed||forecast.Snapshot!=snap||now-tick>250||now-state.LastAction<s.Cooldown)
                    {Messages.Message("AM_ActionChanged".Translate(),MessageTypeDefOf.RejectInput,false);return;}
                    config.maxTotal=target;config.allowSlaughterPregnant=false;config.allowSlaughterBonded=false;
                    map.autoSlaughterManager.Notify_ConfigChanged();state.LastAction=now;state.AddHistory("slaughter config "+def.defName+": "+before+" -> "+SlaughterSignature(config));
                }));
            }
        }
        static string SlaughterSignature(AutoSlaughterConfig c)
        {return c.maxTotal+":"+c.maxMales+":"+c.maxMalesYoung+":"+c.maxFemales+":"+c.maxFemalesYoung+":"+c.allowSlaughterPregnant+":"+c.allowSlaughterBonded;}
        public static void Settings(Listing_Standard list,AnimalSettings s)
        {
            if(list.ButtonText("AM_SolutionMode".Translate((("AM_Mode"+s.SolutionsMode).Translate()))))s.SolutionsMode=(ActionMode)(((int)s.SolutionsMode+1)%3);
            if(list.ButtonText("AM_ProductionMode".Translate((("AM_Mode"+s.ProductionMode).Translate()))))s.ProductionMode=(ActionMode)(((int)s.ProductionMode+1)%3);
            if(list.ButtonText("AM_CapacityMode".Translate(("AM_Mode"+s.CapacityMode).Translate())))s.CapacityMode=(ActionMode)(((int)s.CapacityMode+1)%3);
            list.CheckboxLabeled("AM_AllowSlaughter".Translate(),ref s.AllowSlaughter);
            list.Label("AM_SafetyMargin".Translate((s.SafetyMargin*100).ToString("0")));s.SafetyMargin=list.Slider(s.SafetyMargin,0,.5f);
            list.Label("AM_MaxItems".Translate(s.MaxItems));s.MaxItems=(int)list.Slider(s.MaxItems,1,10000);
            list.Label("AM_MaxBills".Translate(s.MaxBills));s.MaxBills=(int)list.Slider(s.MaxBills,1,10);
            list.Label("AM_Cooldown".Translate((s.Cooldown/60000f).ToString("0.0")));s.Cooldown=(int)list.Slider(s.Cooldown,10000,180000);
        }
    }
}
