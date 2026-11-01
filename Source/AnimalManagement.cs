using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Pengin.AnimalManagement
{
    public sealed class AnimalSettings : ModSettings
    {
        public int WarnDays = 3;
        public bool IncludeForbidden;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref WarnDays, "warnDays", 3);
            Scribe_Values.Look(ref IncludeForbidden, "includeForbidden", false);
            base.ExposeData();
        }
    }

    // Stock-only estimates: they are NOT guaranteed nutrition available to the animals.
    public sealed class FeedForecast : MapComponent
    {
        private const int MaxObjects = 12000;
        public int AnimalCount { get; private set; }
        public int FeedStacks { get; private set; }
        public float DailyDemand { get; private set; }
        public float StockNutrition { get; private set; }
        public bool PartialScan { get; private set; }
        public string LimitingSpecies { get; private set; }
        public float LimitingSpeciesDays { get; private set; }
        public float DaysLeft { get { return DailyDemand > 0.0001f ? StockNutrition / DailyDemand : 0f; } }
        public FeedForecast(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 10000 == map.uniqueID % 10000) Recalculate();
        }

        public void Recalculate()
        {
            AnimalCount = FeedStacks = 0;
            DailyDemand = StockNutrition = LimitingSpeciesDays = 0f;
            LimitingSpecies = null;
            PartialScan = false;
            if (!map.IsPlayerHome || AnimalManagementMod.Settings == null) return;

            var animals = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.IsColonyAnimal || pawn.Dead || pawn.needs == null || pawn.needs.food == null) continue;
                animals.Add(pawn);
                AnimalCount++;
                DailyDemand += Math.Max(0f, pawn.needs.food.FoodFallPerTick * 60000f);
            }
            if (animals.Count == 0) return;

            var groups = animals.GroupBy(p => p.def).ToList();
            var compatibleNutrition = groups.ToDictionary(g => g.Key, g => 0f);
            int scanned = 0;
            foreach (Thing item in map.listerThings.AllThings)
            {
                if (++scanned > MaxObjects) { PartialScan = true; break; }
                if (item is Pawn || item.def == null || !item.def.IsNutritionGivingIngestible) continue;
                if (!AnimalManagementMod.Settings.IncludeForbidden && item.IsForbidden(Faction.OfPlayer)) continue;
                float perItem = item.GetStatValue(StatDefOf.Nutrition);
                if (!(perItem > 0f) || float.IsInfinity(perItem) || float.IsNaN(perItem)) continue;

                float total = perItem * item.stackCount;
                bool edible = false;
                foreach (var group in groups)
                {
                    if (!group.First().RaceProps.CanEverEat(item.def)) continue;
                    edible = true;
                    compatibleNutrition[group.Key] += total;
                }
                if (!edible) continue;
                FeedStacks++;
                StockNutrition += total;
            }

            float minimum = float.MaxValue;
            foreach (var group in groups)
            {
                float demand = group.Sum(p => Math.Max(0f, p.needs.food.FoodFallPerTick * 60000f));
                if (demand <= 0.0001f) continue;
                float days = compatibleNutrition[group.Key] / demand;
                if (days < minimum)
                {
                    minimum = days;
                    LimitingSpecies = group.Key.label;
                    LimitingSpeciesDays = days;
                }
            }
        }
    }

    public sealed class AnimalManagementMod : Mod
    {
        public static AnimalSettings Settings;
        public AnimalManagementMod(ModContentPack pack) : base(pack)
        {
            Settings = GetSettings<AnimalSettings>();
        }
        public override string SettingsCategory() { return "Animal Management"; }
        public override void DoSettingsWindowContents(Rect rect)
        {
            var list = new Listing_Standard();
            list.Begin(rect);
            list.Label("AM_Description".Translate());
            list.Label("AM_WarnDays".Translate(Settings.WarnDays));
            Settings.WarnDays = (int)list.Slider(Settings.WarnDays, 1, 15);
            list.CheckboxLabeled("AM_Forbidden".Translate(), ref Settings.IncludeForbidden);
            Map current = Find.CurrentMap;
            if (current == null) { list.Label("AM_NoMap".Translate()); list.End(); return; }
            FeedForecast f = current.GetComponent<FeedForecast>();
            if (list.ButtonText("AM_Refresh".Translate())) f.Recalculate();
            list.Gap();
            list.Label("AM_Animals".Translate(f.AnimalCount));
            list.Label("AM_Demand".Translate(f.DailyDemand.ToString("0.00")));
            list.Label("AM_Stock".Translate(f.StockNutrition.ToString("0.0"), f.FeedStacks));
            list.Label("AM_Coverage".Translate(f.AnimalCount == 0 ? "N/A" : f.DaysLeft.ToString("0.0")));
            if (f.LimitingSpecies != null)
                list.Label("AM_Species".Translate(f.LimitingSpecies, f.LimitingSpeciesDays.ToString("0.0")));
            if (f.PartialScan) list.Label("AM_Partial".Translate());
            if (f.AnimalCount > 0 && f.DaysLeft < Settings.WarnDays) list.Label("AM_Warning".Translate());
            list.End();
        }
    }
}
