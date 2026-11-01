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
        public bool IncludeForbidden, PopulationScenario;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref WarnDays,"warnDays",3);
            Scribe_Values.Look(ref IncludeForbidden,"includeForbidden",false);
            Scribe_Values.Look(ref PopulationScenario,"populationScenario",false);
            Scribe_Values.Look(ref Births,"births",0);
            Scribe_Values.Look(ref Deaths,"deaths",0);
            base.ExposeData();
        }
        public string Signature {get{return WarnDays+":"+IncludeForbidden+":"+PopulationScenario+":"+Births+":"+Deaths;}}
    }
    public sealed class ForecastSnapshot
    {
        public int AnimalCount,FeedStacks,Tick,AccessChecks;
        public double DailyDemand,HumanDemand,StockNutrition;
        public bool Partial;
        public string SettingsSignature;
        public List<string> Lines=new List<string>();
        public Projection StockOnly;
    }
    public sealed class FeedForecast : MapComponent
    {
        const int MaxObjects=12000, MaxAccessChecks=2000, MaxConsumers=400;
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
        ForecastSnapshot Calculate()
        {
            var settings=AnimalManagementMod.Settings;
            var result=new ForecastSnapshot {Tick=Find.TickManager.TicksGame,SettingsSignature=settings.Signature};
            var all=map.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead&&p.needs!=null&&p.needs.food!=null&&
                (p.IsColonyAnimal || (p.RaceProps.Humanlike&&(p.Faction==Faction.OfPlayer||p.IsPrisonerOfColony)))).ToList();
            // Omitted consumers cannot be ignored as if they consumed nothing.
            if(all.Count>MaxConsumers)throw new InvalidOperationException("Consumer budget exceeded");
            var demands=all.Select(p=>new Demand {Daily=Math.Max(0,p.needs.food.FoodFallPerTick*60000.0),Human=!p.IsColonyAnimal}).ToList();
            result.AnimalCount=all.Count(p=>p.IsColonyAnimal);
            result.DailyDemand=demands.Where(d=>!d.Human).Sum(d=>d.Daily);
            result.HumanDemand=demands.Where(d=>d.Human).Sum(d=>d.Daily);
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
            var stock=new List<FeedPool>();
            // Combine feed pools only when every consumer has the same verified access.
            var byAccess=new Dictionary<string,FeedPool>();
            int scanned=0;
            foreach(Thing item in map.listerThings.AllThings)
            {
                if(++scanned>MaxObjects){result.Partial=true;break;}
                if(item is Pawn||item is Plant||item.def==null||!item.def.IsNutritionGivingIngestible||!item.IngestibleNow)continue;
                if(!settings.IncludeForbidden&&item.IsForbidden(Faction.OfPlayer))continue;
                double perItem=item.GetStatValue(StatDefOf.Nutrition);
                if(double.IsNaN(perItem)||double.IsInfinity(perItem)||perItem<=0||item.stackCount<=0)continue;
                var access=new bool[all.Count];
                for(int a=0;a<all.Count;a++)
                {
                    Pawn pawn=all[a];
                    if(!pawn.RaceProps.CanEverEat(item.def)||!item.Position.InAllowedArea(pawn))continue;
                    if(penOf[a]!=null&&!penOf[a].PenState.ContainsConnectedRegion(item.GetRegion()))continue;
                    if(result.AccessChecks>=MaxAccessChecks){result.Partial=true;continue;}
                    result.AccessChecks++;
                    // Policy/food suitability includes DLC-specific dietary restrictions.
                    if(!FoodUtility.WillEat(pawn,item,pawn))continue;
                    // Downed animals cannot independently reach feed; delivery is not assumed.
                    if(pawn.Downed||!pawn.CanReach(item,PathEndMode.Touch,Danger.None))continue;
                    access[a]=true;
                }
                if(!access.Any(x=>x))continue;
                string key=new string(access.Select(x=>x?'1':'0').ToArray());
                FeedPool pool;
                if(!byAccess.TryGetValue(key,out pool))
                {pool=new FeedPool{Access=access};byAccess[key]=pool;stock.Add(pool);}
                pool.Nutrition+=perItem*item.stackCount;
                result.FeedStacks++;result.StockNutrition+=perItem*item.stackCount;
            }
            // In a partial scan, omitted stores may be eaten by humans. It is therefore
            // not a complete competition forecast, even though omitted feed is not credited.
            foreach(var group in all.Where(p=>p.IsColonyAnimal).GroupBy(p=>p.def))
                result.Lines.Add("AM_SpeciesLine".Translate(group.Key.LabelCap,group.Count(),
                    group.Sum(p=>p.needs.food.FoodFallPerTick*60000.0).ToString("0.00")));
            var growthByDay=new Dictionary<int,List<FeedPool>>();
            float longitude=Find.WorldGrid.LongLatOf(map.Tile).x;
            int abs=Find.TickManager.TicksAbs;
            for(int day=1;day<=30;day++)
            {
                var growth=new List<FeedPool>();
                long future=(long)abs+(day-1)*60000L;
                Quadrum quadrum=GenDate.Quadrum(future,longitude);
                bool frozen=GenTemperature.GetTemperatureFromSeasonAtTile((int)Math.Min(int.MaxValue,future),map.Tile)<=0;
                foreach(CompAnimalPenMarker pen in pens)
                {
                    PenFoodCalculator calc=pen.PenFoodCalculator;
                    double amount=frozen?0:calc.nutritionPerDayPerQuadrum.ForQuadrum(quadrum);
                    if(double.IsNaN(amount)||double.IsInfinity(amount)||amount<0){result.Partial=true;continue;}
                    var access=new bool[all.Count];
                    for(int a=0;a<all.Count;a++)access[a]=penOf[a]==pen&&
                        all[a].RaceProps.CanEverEat(ThingDefOf.Plant_Grass)&&!all[a].Downed;
                    growth.Add(new FeedPool{Nutrition=amount,Pasture=true,Access=access});
                }
                growthByDay[day]=growth;
            }
            foreach(CompAnimalPenMarker pen in pens)
            {
                int count=Enumerable.Range(0,all.Count).Count(a=>penOf[a]==pen);
                double demand=Enumerable.Range(0,all.Count).Where(a=>penOf[a]==pen).Sum(a=>demands[a].Daily);
                result.Lines.Add("AM_PenPopulation".Translate(pen.parent.LabelShort,count,demand.ToString("0.00")));
                foreach(var species in Enumerable.Range(0,all.Count).Where(a=>penOf[a]==pen).GroupBy(a=>all[a].def))
                    result.Lines.Add("AM_PenSpecies".Translate(pen.parent.LabelShort,species.Key.LabelCap,
                        species.Count(),species.Sum(a=>demands[a].Daily).ToString("0.00")));
            }
            if(settings.PopulationScenario&&result.AnimalCount==0)result.Lines.Add("AM_NoPopulationBaseline".Translate());
            int births=settings.PopulationScenario&&result.AnimalCount>0?Math.Min(100,Math.Max(0,settings.Births)):0;
            int deaths=settings.PopulationScenario&&result.AnimalCount>0?Math.Min(100,Math.Max(0,settings.Deaths)):0;
            result.StockOnly=NutritionForecast.Run(demands,stock,day=>new List<FeedPool>(),30,births,deaths);
            foreach(int horizon in new[]{7,15,30})
            {
                Projection p=NutritionForecast.Run(demands,stock,day=>growthByDay[day],horizon,births,deaths);
                string shortage=p.FirstShortageDay==0?"AM_NoShortage".Translate().ToString():"AM_ShortageDay".Translate(p.FirstShortageDay).ToString();
                result.Lines.Add("AM_Projection".Translate(horizon,shortage,p.StockConsumed.ToString("0.0"),
                    p.PastureConsumed.ToString("0.0"),p.Unmet.Sum().ToString("0.0")));
                foreach(CompAnimalPenMarker pen in pens)
                {
                    double deficit=Enumerable.Range(0,all.Count).Where(a=>penOf[a]==pen).Sum(a=>p.Unmet[a]);
                    int first=Enumerable.Range(0,all.Count).Where(a=>penOf[a]==pen&&p.FirstShortageDays[a]>0)
                        .Select(a=>p.FirstShortageDays[a]).DefaultIfEmpty(0).Min();
                    string date=first==0?"AM_NoShortage".Translate().ToString():"AM_ShortageDay".Translate(first).ToString();
                    result.Lines.Add("AM_PenProjection".Translate(pen.parent.LabelShort,horizon,deficit.ToString("0.0"),date));
                }
                if(settings.PopulationScenario)result.Lines.Add("AM_PopulationAt".Translate(horizon,p.FinalAnimalCount.ToString("0.0")));
            }
            return result;
        }
    }
    public sealed class AnimalManagementMod : Mod
    {
        public static AnimalSettings Settings;
        Vector2 scroll;float contentHeight=1600;
        public AnimalManagementMod(ModContentPack pack):base(pack){Settings=GetSettings<AnimalSettings>();}
        public override string SettingsCategory(){return "Animal Management";}
        public override void DoSettingsWindowContents(Rect rect)
        {
            Rect content=new Rect(0,0,Math.Max(100,rect.width-20),Math.Max(rect.height,contentHeight));
            Widgets.BeginScrollView(rect,ref scroll,content);
            var list=new Listing_Standard();list.Begin(content);
            list.Label("AM_Description".Translate());
            list.Label("AM_WarnDays".Translate(Settings.WarnDays));Settings.WarnDays=(int)list.Slider(Settings.WarnDays,1,15);
            list.CheckboxLabeled("AM_Forbidden".Translate(),ref Settings.IncludeForbidden);
            list.CheckboxLabeled("AM_PopulationScenario".Translate(),ref Settings.PopulationScenario);
            if(Settings.PopulationScenario)
            {
                list.Label("AM_Births".Translate(Settings.Births));Settings.Births=(int)list.Slider(Settings.Births,0,100);
                list.Label("AM_Deaths".Translate(Settings.Deaths));Settings.Deaths=(int)list.Slider(Settings.Deaths,0,100);
            }
            Map current=Find.CurrentMap;
            if(current!=null)
            {
                FeedForecast f=current.GetComponent<FeedForecast>();
                if(list.ButtonText("AM_Refresh".Translate()))f.Recalculate();
                ForecastSnapshot s=f.Snapshot;
                if(f.Failed)list.Label("AM_Failed".Translate());
                if(s.SettingsSignature!=Settings.Signature)list.Label("AM_Stale".Translate());
                list.Label("AM_Animals".Translate(s.AnimalCount));
                list.Label("AM_Demand".Translate(s.DailyDemand.ToString("0.00")));
                list.Label("AM_HumanDemand".Translate(s.HumanDemand.ToString("0.00")));
                list.Label("AM_Stock".Translate(s.StockNutrition.ToString("0.0"),s.FeedStacks));
                if(s.StockOnly!=null)
                {
                    list.Label("AM_StockShortage".Translate(s.StockOnly.FirstShortageDay==0?"AM_NoShortage".Translate().ToString():"AM_ShortageDay".Translate(s.StockOnly.FirstShortageDay).ToString()));
                    if(s.StockOnly.FirstShortageDay>0&&s.StockOnly.FirstShortageDay<=Settings.WarnDays)list.Label("AM_Warning".Translate());
                }
                if(s.Partial)list.Label("AM_Partial".Translate());
                list.Label("AM_Assumptions".Translate());
                foreach(string line in s.Lines)list.Label(line);
                list.Label("AM_Invalidation".Translate());
                list.Label("AM_Scan".Translate(s.Tick,f.ScanMilliseconds.ToString("0.00"),s.AccessChecks));
            }
            else list.Label("AM_NoMap".Translate());
            contentHeight=list.CurHeight+20;list.End();Widgets.EndScrollView();
        }
    }
}
