using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Pengin.AnimalManagement
{
    public sealed class AnimalSettings : ModSettings
    {
        public int WarnDays=3, Births, Deaths;
        public ActionMode SolutionsMode=ActionMode.Confirm, ProductionMode=ActionMode.Confirm;
        public int TargetDays=15, MaxBills=2, MaxItems=1000, Cooldown=60000;
        public float SafetyMargin=.1f;
        public bool AllowSlaughter;
        public List<string> AllowedFoods=new List<string>(), AllowedBenches=new List<string>();
        public bool IncludeForbidden, PopulationScenario, ShowAlert=true;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref WarnDays,"warnDays",3);
            Scribe_Values.Look(ref IncludeForbidden,"includeForbidden",false);
            Scribe_Values.Look(ref PopulationScenario,"populationScenario",false);
            Scribe_Values.Look(ref Births,"births",0);
            Scribe_Values.Look(ref Deaths,"deaths",0);
            Scribe_Values.Look(ref ShowAlert,"showAlert",true);
            Scribe_Values.Look(ref SolutionsMode,"solutionsMode",ActionMode.Confirm);
            Scribe_Values.Look(ref ProductionMode,"productionMode",ActionMode.Confirm);
            Scribe_Values.Look(ref TargetDays,"targetDays",15);
            Scribe_Values.Look(ref SafetyMargin,"safetyMargin",.1f);
            Scribe_Values.Look(ref MaxBills,"maxBills",2);
            Scribe_Values.Look(ref MaxItems,"maxItems",1000);
            Scribe_Values.Look(ref Cooldown,"actionCooldown",60000);
            Scribe_Values.Look(ref AllowSlaughter,"allowSlaughter",false);
            Scribe_Collections.Look(ref AllowedFoods,"allowedFoods",LookMode.Value);
            Scribe_Collections.Look(ref AllowedBenches,"allowedBenches",LookMode.Value);
            if(AllowedFoods==null)AllowedFoods=new List<string>();
            if(AllowedBenches==null)AllowedBenches=new List<string>();
            TargetDays=TargetDays==7||TargetDays==30?TargetDays:15;
            MaxBills=Math.Max(1,Math.Min(10,MaxBills));MaxItems=Math.Max(1,Math.Min(10000,MaxItems));
            Cooldown=Math.Max(10000,Cooldown);SafetyMargin=Math.Max(0,Math.Min(.5f,SafetyMargin));
            base.ExposeData();
        }
        // Only settings that change the calculation; the warning threshold is applied on display.
        public string Signature {get{return IncludeForbidden+":"+PopulationScenario+":"+Births+":"+Deaths;}}
    }
    public sealed class FeedMaterial
    {
        public int Pool;
        public double PerItem;
    }
    public sealed class FeedAnchor
    {
        public Thing Stack;
        public FeedCandidate Candidate;
    }
    public sealed class ForecastSnapshot
    {
        public int AnimalCount,FeedStacks,Tick,AccessChecks,RoamingGrazers,BreastfedBabies;
        public double DailyDemand,HumanDemand,StockNutrition,StandingNutrition,RoamingDemand;
        public bool Partial;
        public string SettingsSignature;
        public FeedScenario Scenario;
        public Dictionary<Thing,FeedMaterial> Materials=new Dictionary<Thing,FeedMaterial>();
        public List<Pawn> Consumers;
        public List<FeedAnchor> Anchors=new List<FeedAnchor>();
        public bool WeatherDependent;
        public List<string> Lines=new List<string>();
        public Projection StockOnly, Full;
    }
    public sealed class FeedForecast : MapComponent
    {
        const int MaxObjects=12000, MaxAccessChecks=2000, MaxConsumers=400, MaxPenCells=30000;
        float[] futureTemperatures=new float[30];
        public ForecastSnapshot Snapshot {get;private set;} = new ForecastSnapshot();
        public double ScanMilliseconds {get;private set;}
        public bool Failed {get;private set;}
        public FeedForecast(Map map):base(map){}
        public override void MapComponentTick()
        {
            if(Find.TickManager.TicksGame%10000==map.uniqueID%10000)Recalculate();
        }
        public void Recalculate()
        {
            if(AnimalManagementMod.Settings==null)return;
            var timer=Stopwatch.StartNew();
            try{Snapshot=Calculate();Failed=false;}
            catch(Exception e){if(!Failed)Log.Warning("Animal Management forecast incomplete: "+e.GetType().Name);Failed=true;}
            finally{ScanMilliseconds=timer.Elapsed.TotalMilliseconds;}
        }
        int LastFreshDay(Thing item)
        {
            CompRottable rot=item.TryGetComp<CompRottable>();
            if(rot==null)return int.MaxValue;
            Room room=item.GetRoom();bool outdoor=room==null||room.UsesOutdoorTemperature;
            double remainingFraction=1;
            for(int day=1;day<=30;day++)
            {
                float temp=outdoor?(day==1?Math.Max(item.AmbientTemperature,futureTemperatures[0]):futureTemperatures[day-1]):item.AmbientTemperature;
                int ticks=rot.TicksUntilRotAtTemp(temp);
                if(ticks<=0)return day-1;
                if(ticks==int.MaxValue)continue;
                remainingFraction-=60000.0/ticks;
                if(remainingFraction<=0)return day-1; // Do not bank food that spoils during that day.
            }
            return int.MaxValue;
        }
        // Steady-state consumption: the food meter falls slower while hungry (0.5x), urgently
        // hungry (0.25x) and starving (0x), so the current rate would hide demand exactly
        // when feed runs short. Use the fed rate the pawn returns to once it eats.
        static double DailyNeed(Pawn p){return Math.Max(0,p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed)*60000.0);}
        static bool Grazes(Pawn p){return p.RaceProps.CanEverEat(ThingDefOf.Plant_Grass)&&FoodUtility.WillEat(p,ThingDefOf.Plant_Grass,p);}

        sealed class AccessCache
        {
            // Reachability within one district is shared by every stack in it, and food
            // suitability is shared by every stack of a plain def: each is evaluated once.
            readonly Dictionary<long,bool> reach=new Dictionary<long,bool>();
            readonly Dictionary<long,bool> willEat=new Dictionary<long,bool>();
            public int Misses;
            public bool OverBudget(){return Misses>=MaxAccessChecks;}
            public bool? Reach(int index,Pawn pawn,Thing item)
            {
                District district=item.GetDistrict();
                long key=district==null?-1:((long)index<<32)|(uint)district.ID;
                bool value;
                if(key>=0&&reach.TryGetValue(key,out value))return value;
                if(OverBudget())return null;
                Misses++;
                value=pawn.CanReach(item,PathEndMode.Touch,Danger.None);
                if(key>=0)reach[key]=value;
                return value;
            }
            public bool? WillEat(int index,Pawn pawn,Thing item)
            {
                // Meals carry ingredients (cannibalism, insect meat, ...): evaluate per stack.
                bool plain=item.TryGetComp<CompIngredients>()==null;
                long key=((long)index<<32)|(uint)item.def.shortHash;
                bool value;
                if(plain&&willEat.TryGetValue(key,out value))return value;
                if(OverBudget())return null;
                Misses++;
                value=FoodUtility.WillEat(pawn,item,pawn);
                if(plain)willEat[key]=value;
                return value;
            }
        }

        ForecastSnapshot Calculate()
        {
            var settings=AnimalManagementMod.Settings;
            int climateStart=Find.TickManager.TicksAbs;
            for(int d=0;d<30;d++)futureTemperatures[d]=GenTemperature.GetTemperatureFromSeasonAtTile(
                (int)Math.Min(int.MaxValue,(long)climateStart+d*60000L),map.Tile);
            var result=new ForecastSnapshot {Tick=Find.TickManager.TicksGame,SettingsSignature=settings.Signature};
            var all=map.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead&&p.needs!=null&&p.needs.food!=null&&
                (p.IsColonyAnimal || (p.RaceProps.Humanlike&&(p.Faction==Faction.OfPlayer||p.IsPrisonerOfColony)))).ToList();
            // Omitted consumers cannot be ignored as if they consumed nothing.
            if(all.Count>MaxConsumers)throw new InvalidOperationException("Consumer budget exceeded");
            var demands=all.Select(p=>new Demand {Daily=DailyNeed(p),Human=!p.IsColonyAnimal}).ToList();
            // Babies are breastfed while a lactating colonist is present; then they do not
            // draw on stored food. Without one, baby food from stock is assumed.
            bool lactating=all.Any(p=>p.RaceProps.Humanlike&&p.health!=null&&p.health.hediffSet.HasHediff(HediffDefOf.Lactating));
            for(int a=0;a<all.Count;a++)
                if(demands[a].Human&&lactating&&all[a].DevelopmentalStage.Baby()){demands[a].Daily=0;result.BreastfedBabies++;}
            result.AnimalCount=all.Count(p=>p.IsColonyAnimal);
            var penOf=new CompAnimalPenMarker[all.Count];
            var pens=new List<CompAnimalPenMarker>();
            var penRegions=new Dictionary<Region,CompAnimalPenMarker>();
            for(int a=0;a<all.Count;a++)
            {
                if(demands[a].Human)continue;
                CompAnimalPenMarker pen=AnimalPenUtility.GetCurrentPenOf(all[a],false);
                if(pen==null||!pen.PenState.Enclosed)continue;
                CompAnimalPenMarker canonical=null;
                foreach(Region region in pen.PenState.ConnectedRegions)
                    if(penRegions.TryGetValue(region,out canonical))break;
                if(canonical==null)
                {
                    canonical=pen;pens.Add(pen);
                    foreach(Region region in pen.PenState.ConnectedRegions)penRegions[region]=pen;
                }
                penOf[a]=canonical;
            }
            // Grazers outside pens feed on wild plants, which are not modeled: they are listed
            // and excluded instead of being charged entirely to stored feed.
            for(int a=0;a<all.Count;a++)
                if(!demands[a].Human&&penOf[a]==null&&!all[a].Downed&&Grazes(all[a]))
                {result.RoamingGrazers++;result.RoamingDemand+=demands[a].Daily;demands[a].Daily=0;}
            result.DailyDemand=demands.Where(d=>!d.Human).Sum(d=>d.Daily);
            result.HumanDemand=demands.Where(d=>d.Human).Sum(d=>d.Daily);
            // Prisoners and downed colonists are fed by wardens/carers: their access follows
            // whatever a capable free colonist can reach.
            var deliverers=Enumerable.Range(0,all.Count).Where(a=>demands[a].Human&&all[a].IsFreeColonist&&!all[a].Downed).ToList();
            var cache=new AccessCache();
            var stock=new List<FeedPool>();
            // Combine feed pools only when every consumer has the same verified access.
            var byAccess=new Dictionary<string,FeedPool>();
            int scanned=0;
            // FoodSource lists ingestible things only (not rocks, chunks, filth or buildings).
            // Standing plants are skipped before counting, so trees and grass cannot use up
            // the item budget before any stored food is reached.
            foreach(Thing item in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource))
            {
                if(item is Pawn||item is Plant||item.def==null||!item.def.IsNutritionGivingIngestible||!item.IngestibleNow)continue;
                if(++scanned>MaxObjects){result.Partial=true;break;}
                if(!settings.IncludeForbidden&&item.IsForbidden(Faction.OfPlayer))continue;
                double perItem=item.GetStatValue(StatDefOf.Nutrition);
                double conservativeNutrition=double.PositiveInfinity;
                if(double.IsNaN(perItem)||double.IsInfinity(perItem)||perItem<=0||item.stackCount<=0)continue;
                var access=new bool[all.Count];
                bool? delivered=null;
                for(int a=0;a<all.Count;a++)
                {
                    Pawn pawn=all[a];
                    if(!pawn.RaceProps.CanEverEat(item.def))continue;
                    bool needsDelivery=demands[a].Human&&(pawn.IsPrisonerOfColony||pawn.Downed);
                    if(!needsDelivery&&!item.Position.InAllowedArea(pawn))continue;
                    if(penOf[a]!=null&&!penOf[a].PenState.ContainsConnectedRegion(item.GetRegion()))continue;
                    // Policy/food suitability includes DLC-specific dietary restrictions.
                    bool? eats=cache.WillEat(a,pawn,item);
                    if(eats==null){result.Partial=true;continue;}
                    if(!eats.Value)continue;
                    bool? reach;
                    if(needsDelivery)
                    {
                        if(delivered==null)
                        {
                            delivered=false;
                            foreach(int c in deliverers)
                            {
                                bool? r=cache.Reach(c,all[c],item);
                                if(r==null){result.Partial=true;break;}
                                if(r.Value){delivered=true;break;}
                            }
                        }
                        reach=delivered;
                    }
                    // Downed animals cannot independently reach feed; delivery is not assumed.
                    else reach=pawn.Downed?false:cache.Reach(a,pawn,item);
                    if(reach==null){result.Partial=true;continue;}
                    if(!reach.Value)continue;
                    double nutrition=FoodUtility.NutritionForEater(pawn,item);
                    if(double.IsNaN(nutrition)||double.IsInfinity(nutrition)||nutrition<=0){result.Partial=true;continue;}
                    conservativeNutrition=Math.Min(conservativeNutrition,nutrition);
                    access[a]=true;
                }
                if(!access.Any(x=>x))continue;
                perItem=conservativeNutrition;
                int expiry=LastFreshDay(item);
                // Existing stacks provide verified diet/access and a conservative expiry.
                // Do not infer accessibility for a food which has never reached the pen.
                if(result.Anchors.Count<32&&(item.def.defName=="Hay"||item.def.defName=="Kibble")&&
                    !item.IsForbidden(Faction.OfPlayer)&&!result.Anchors.Any(a=>a.Stack.def==item.def&&a.Candidate.Access.SequenceEqual(access)))
                    result.Anchors.Add(new FeedAnchor{Stack=item,Candidate=new FeedCandidate(perItem,access,expiry)});
                string key=new string(access.Select(x=>x?'1':'0').ToArray())+":"+expiry;
                FeedPool pool;
                if(!byAccess.TryGetValue(key,out pool))
                {pool=new FeedPool{Access=access,LastFreshDay=expiry};byAccess[key]=pool;stock.Add(pool);}
                pool.Nutrition+=perItem*item.stackCount;
                result.Materials[item]=new FeedMaterial{Pool=stock.IndexOf(pool),PerItem=perItem};
                result.FeedStacks++;result.StockNutrition+=perItem*item.stackCount;
            }
            result.AccessChecks=cache.Misses;
            // Plants already standing in each pen: eaten once, not replenished.
            var standing=new List<FeedPool>();
            int penCells=0;
            foreach(CompAnimalPenMarker pen in pens)
            {
                var byDef=new Dictionary<ThingDef,double>();
                foreach(Region region in pen.PenState.ConnectedRegions)
                {
                    foreach(IntVec3 cell in region.Cells)
                    {
                        if(++penCells>MaxPenCells){result.Partial=true;break;}
                        Plant plant=cell.GetPlant(map);
                        if(plant==null||!plant.IngestibleNow||!MapPlantGrowthRateCalculator.IsEdibleByPastureAnimals(plant.def))continue;
                        double value=plant.GetStatValue(StatDefOf.Nutrition);
                        if(double.IsNaN(value)||double.IsInfinity(value)||value<=0)continue;
                        double sum;byDef.TryGetValue(plant.def,out sum);byDef[plant.def]=sum+value;
                    }
                    if(penCells>MaxPenCells)break;
                }
                foreach(var entry in byDef)
                {
                    var access=new bool[all.Count];
                    for(int a=0;a<all.Count;a++)access[a]=penOf[a]==pen&&!all[a].Downed&&
                        all[a].RaceProps.CanEverEat(entry.Key)&&FoodUtility.WillEat(all[a],entry.Key,all[a]);
                    if(!access.Any(x=>x))continue;
                    standing.Add(new FeedPool{Nutrition=entry.Value,Standing=true,Access=access});
                    result.StandingNutrition+=entry.Value;
                }
            }
            foreach(var group in all.Where(p=>p.IsColonyAnimal).GroupBy(p=>p.def))
                result.Lines.Add("AM_SpeciesLine".Translate(group.Key.LabelCap,group.Count(),group.Sum(p=>DailyNeed(p)).ToString("0.00")));
            if(result.RoamingGrazers>0)result.Lines.Add("AM_RoamingGrazers".Translate(result.RoamingGrazers,result.RoamingDemand.ToString("0.00")));
            if(result.BreastfedBabies>0)result.Lines.Add("AM_BreastfedBabies".Translate(result.BreastfedBabies));
            var growthByDay=new Dictionary<int,List<FeedPool>>();
            float longitude=Find.WorldGrid.LongLatOf(map.Tile).x;
            int abs=Find.TickManager.TicksAbs;
            var grassAccess=new bool[all.Count];
            for(int a=0;a<all.Count;a++)grassAccess[a]=penOf[a]!=null&&!all[a].Downed&&Grazes(all[a]);
            for(int day=1;day<=30;day++)
            {
                var growth=new List<FeedPool>();
                long future=(long)abs+(day-1)*60000L;
                Quadrum quadrum=GenDate.Quadrum(future,longitude);
                bool frozen=futureTemperatures[day-1]<=0;
                foreach(CompAnimalPenMarker pen in pens)
                {
                    PenFoodCalculator calc=pen.PenFoodCalculator;
                    double amount=frozen?0:calc.nutritionPerDayPerQuadrum.ForQuadrum(quadrum);
                    if(double.IsNaN(amount)||double.IsInfinity(amount)||amount<0){result.Partial=true;continue;}
                    var access=new bool[all.Count];
                    for(int a=0;a<all.Count;a++)access[a]=penOf[a]==pen&&grassAccess[a];
                    growth.Add(new FeedPool{Nutrition=amount,Pasture=true,Access=access});
                }
                growthByDay[day]=growth;
            }
            result.Consumers=all;
            result.WeatherDependent=result.RoamingGrazers>0||growthByDay.Values.Any(g=>g.Any(p=>p.Nutrition>0))||stock.Any(p=>p.LastFreshDay<30);
            result.Scenario=new FeedScenario(demands,stock.Concat(standing).ToList(),day=>growthByDay[day],
                !result.Partial&&!result.WeatherDependent&&!settings.IncludeForbidden&&!settings.PopulationScenario);
            // A pen whose animals share stored feed with other consumers has an
            // allocation-dependent shortage date: the shared feed could go either way.
            var sharedPens=new HashSet<CompAnimalPenMarker>();
            foreach(CompAnimalPenMarker pen in pens)
                if(stock.Any(pool=>Enumerable.Range(0,all.Count).Any(a=>pool.Access[a]&&penOf[a]==pen&&demands[a].Daily>0)&&
                    Enumerable.Range(0,all.Count).Any(a=>pool.Access[a]&&penOf[a]!=pen&&demands[a].Daily>0)))sharedPens.Add(pen);
            foreach(CompAnimalPenMarker pen in pens)
            {
                int count=Enumerable.Range(0,all.Count).Count(a=>penOf[a]==pen);
                double demand=Enumerable.Range(0,all.Count).Where(a=>penOf[a]==pen).Sum(a=>demands[a].Daily);
                double produced=pen.PenFoodCalculator.NutritionPerDayToday;
                result.Lines.Add("AM_PenPopulation".Translate(pen.parent.LabelShort,count,demand.ToString("0.00")));
                result.Lines.Add("AM_PenProduction".Translate(produced.ToString("0.00"),(produced-demand).ToString("+0.00;-0.00;0.00")));
                foreach(var species in Enumerable.Range(0,all.Count).Where(a=>penOf[a]==pen).GroupBy(a=>all[a].def))
                    result.Lines.Add("AM_PenSpecies".Translate(pen.parent.LabelShort,species.Key.LabelCap,
                        species.Count(),species.Sum(a=>demands[a].Daily).ToString("0.00")));
            }
            if(settings.PopulationScenario&&result.AnimalCount==0)result.Lines.Add("AM_NoPopulationBaseline".Translate());
            int births=settings.PopulationScenario&&result.AnimalCount>0?Math.Min(100,Math.Max(0,settings.Births)):0;
            int deaths=settings.PopulationScenario&&result.AnimalCount>0?Math.Min(100,Math.Max(0,settings.Deaths)):0;
            result.StockOnly=NutritionForecast.Run(demands,stock,day=>new List<FeedPool>(),30,births,deaths);
            Projection[] projections=NutritionForecast.Run(demands,stock.Concat(standing).ToList(),day=>growthByDay[day],new[]{7,15,30},births,deaths);
            result.Full=projections[2];
            foreach(Projection p in projections)
            {
                string shortage=p.FirstShortageDay==0?"AM_NoShortage".Translate().ToString():"AM_ShortageDay".Translate(p.FirstShortageDay).ToString();
                result.Lines.Add("AM_Projection".Translate(p.Horizon,shortage,p.StockConsumed.ToString("0.0"),
                    (p.PastureConsumed+p.StandingConsumed).ToString("0.0"),p.Unmet.Sum().ToString("0.0")));
                result.Lines.Add("AM_Spoilage".Translate(p.SpoiledNutrition.ToString("0.0")));
                foreach(CompAnimalPenMarker pen in pens)
                {
                    double deficit=Enumerable.Range(0,all.Count).Where(a=>penOf[a]==pen).Sum(a=>p.Unmet[a]);
                    int first=Enumerable.Range(0,all.Count).Where(a=>penOf[a]==pen&&p.FirstShortageDays[a]>0)
                        .Select(a=>p.FirstShortageDays[a]).DefaultIfEmpty(0).Min();
                    string date=first==0?"AM_NoShortage".Translate().ToString():"AM_ShortageDay".Translate(first).ToString();
                    string line="AM_PenProjection".Translate(pen.parent.LabelShort,p.Horizon,deficit.ToString("0.0"),date);
                    if(sharedPens.Contains(pen)&&p.FirstAnimalShortageDay>0)line+=" "+"AM_PenShared".Translate().ToString();
                    result.Lines.Add(line);
                }
                if(settings.PopulationScenario)result.Lines.Add("AM_PopulationAt".Translate(p.Horizon,p.FinalAnimalCount.ToString("0.0")));
            }
            return result;
        }
    }
}
