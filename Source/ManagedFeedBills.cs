using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace Pengin.AnimalManagement
{
    public sealed class FeedProductionPreview
    {
        public Building_WorkTable Bench;
        public RecipeDef Recipe;
        public FeedAnchor Anchor;
        public int Items, Batches;
        public bool DelaySafe;
        public string Blocked="AM_ProductionBlocked";
        public Dictionary<Thing,int> Ingredients=new Dictionary<Thing,int>();
        public bool Ready {get{return Blocked==null;}}
    }
    public static class ManagedFeedBills
    {
        // Fingerprint all public settings plus private storage selection and special filters.
        // Def/Thing references are identities; transient caches/counters never confer ownership.
        static string Value(object value)
        {
            if(value==null)return "null";
            var def=value as Def;if(def!=null)return def.defName;
            var thing=value as Thing;if(thing!=null)return thing.ThingID;
            var slot=value as SlotGroup;if(slot!=null)return slot.parent.ToString();
            return value.ToString();
        }
        // Player-controlled settings only (#21). repeatCount is runtime progress, tracked
        // separately; paused is vanilla's own TargetCount toggle; the list position moves
        // when other bills are added or removed. Pawn restrictions are private fields.
        // v2 is culture-independent (ordinal order, invariant formatting) so a locale change
        // between sessions is not mistaken for a manual edit. Field lists are built once.
        static readonly string[] runtime={"billStack","loadID","nextTickToSearchForIngredients","ingredientFilter","repeatCount","paused"};
        static FieldInfo[] ownedFields,legacyFields,filterFields;
        static FieldInfo[] OwnedFields{get{return ownedFields??(ownedFields=typeof(Bill_Production).GetFields(BindingFlags.Public|BindingFlags.Instance)
            .Where(f=>!runtime.Contains(f.Name)).OrderBy(f=>f.Name,StringComparer.Ordinal).ToArray());}}
        static FieldInfo[] FilterFields{get{return filterFields??(filterFields=typeof(ThingFilter).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .Where(f=>f.Name=="disallowedSpecialFilters"||f.Name=="allowedHitPointsPercents"||f.Name=="allowedQualities").ToArray());}}
        public static string Fingerprint(Bill_Production b)
        {
            var thread=System.Threading.Thread.CurrentThread;var culture=thread.CurrentCulture;
            try
            {
                thread.CurrentCulture=System.Globalization.CultureInfo.InvariantCulture;
                string result=ManagedBillPolicy.Version+string.Join("|",OwnedFields.Select(f=>f.Name+"="+Value(f.GetValue(b))));
                result+="|restriction="+Value(b.PawnRestriction)+","+b.SlavesOnly+","+b.MechsOnly+","+b.NonMechsOnly;
                return result+"|"+Settings(b,StringComparer.Ordinal);
            }
            finally{thread.CurrentCulture=culture;}
        }
        // Owned-settings comparison. A 1.1.0 entry (no version prefix) is compared with the
        // same legacy algorithm after removing its progress and position tokens.
        public static bool MatchesSettings(string saved,Bill_Production b)
        {
            if(string.IsNullOrEmpty(saved))return false;
            if(saved.StartsWith(ManagedBillPolicy.Version,StringComparison.Ordinal))return saved==Fingerprint(b);
            return ManagedBillPolicy.NormalizeLegacy(saved)==ManagedBillPolicy.NormalizeLegacy(LegacyFingerprint(b));
        }
        // Exactly the 1.1.0 algorithm (current culture, default ordering).
        static string LegacyFingerprint(Bill_Production b)
        {
            if(legacyFields==null)legacyFields=typeof(Bill_Production).GetFields(BindingFlags.Public|BindingFlags.Instance)
                .Where(f=>f.Name!="billStack"&&f.Name!="loadID"&&f.Name!="nextTickToSearchForIngredients"&&f.Name!="ingredientFilter").OrderBy(f=>f.Name).ToArray();
            string result=string.Join("|",legacyFields.Select(f=>f.Name+"="+Value(f.GetValue(b))));
            return result+"|"+Settings(b,StringComparer.CurrentCulture)+"|index="+b.billStack.IndexOf(b);
        }
        static string Settings(Bill_Production b,StringComparer order)
        {
            string result=b.RenamableLabel+"|"+b.GetStoreMode().defName+"|"+Value(b.GetSlotGroup())+"|"+Value(b.GetIncludeSlotGroup());
            result+="|"+string.Join(",",b.ingredientFilter.AllowedThingDefs.Select(d=>d.defName).OrderBy(n=>n,order));
            foreach(FieldInfo f in FilterFields)
            {
                var sequence=f.GetValue(b.ingredientFilter) as System.Collections.IEnumerable;
                result+="|"+f.Name+"="+(sequence==null?Value(f.GetValue(b.ingredientFilter)):string.Join(",",sequence.Cast<object>().Select(Value).OrderBy(n=>n,order)));
            }
            return result;
        }
        public static string PermissionKey(Building_WorkTable bench,FeedAnchor anchor)
        {return bench.ThingID+"@"+bench.Position+"/"+anchor.Stack.ThingID+"@"+anchor.Stack.Position;}
        static bool SafeBench(Building_WorkTable bench,Map map)
        {return bench!=null&&bench.Spawned&&bench.Map==map&&bench.Faction==Faction.OfPlayer&&bench.GetType()==typeof(Building_WorkTable)&&!bench.IsForbidden(Faction.OfPlayer)&&bench.CurrentlyUsableForBills();}
        public static FeedProductionPreview Preview(Map map,FeedForecast forecast,FeedAnchor anchor,FeedActionPlan plan,Building_WorkTable bench)
        {
            var p=new FeedProductionPreview{Bench=bench,Anchor=anchor};
            var s=AnimalManagementMod.Settings;var snap=forecast.Snapshot;
            if(!SafeBench(bench,map)||forecast.Failed||snap.Partial||snap.Scenario==null||!plan.Feasible||!plan.Reliable||plan.RequiredItems==0||snap.SettingsSignature!=s.Signature)return p;
            if(!anchor.Stack.Spawned||anchor.Stack.Map!=map||anchor.Stack.IsForbidden(Faction.OfPlayer))return p;
            SlotGroup storage=anchor.Stack.GetSlotGroup();
            if(storage==null||!storage.Settings.AllowedToAccept(anchor.Stack))return p;
            // Only the vanilla kibble recipe is writable. Arbitrary mod recipes stay advice-only.
            var recipe=bench.def.AllRecipes.FirstOrDefault(r=>r.defName=="Make_Kibble"&&r.AvailableNow&&r.AvailableOnNow(bench)&&r.products!=null&&r.products.Count==1&&r.products[0].thingDef==anchor.Stack.def);
            if(recipe==null||recipe.workerClass!=typeof(RecipeWorker)||recipe.ingredients==null||recipe.ingredients.Count==0||recipe.ingredients.Count>8||recipe.fixedIngredientFilter==null||recipe.defaultIngredientFilter==null)return p;
            p.Recipe=recipe;
            if(map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Any(table=>table.BillStack.Bills.Any(b=>b.recipe==recipe))){p.Blocked="AM_DuplicateBill";return p;}
            int yield=recipe.products[0].count;if(yield<=0)return p;
            p.Batches=Math.Min(100,Math.Min(s.MaxItems/yield,(int)Math.Ceiling(plan.RequiredItems*(1+s.SafetyMargin)/yield)));
            if(p.Batches<1)return p;p.Items=p.Batches*yield;
            var worker=map.mapPawns.FreeColonistsSpawned.FirstOrDefault(w=>w.workSettings!=null&&!w.Drafted&&!w.Downed&&!w.InMentalState&&w.Awake()&&recipe.PawnSatisfiesSkillRequirements(w)&&
                w.workSettings.GetPriority(DefDatabase<WorkTypeDef>.GetNamed("Cooking"))>0&&w.CanReach(bench,PathEndMode.InteractionCell,Danger.None));
            if(worker==null)return p;
            // Bounded, no mixed-def arithmetic and no double allocation of shared ingredients.
            int checks=0;var permitted=new HashSet<ThingDef>();
            foreach(var ingredient in recipe.ingredients)
            {
                bool supplied=false;
                foreach(ThingDef def in ingredient.filter.AllowedThingDefs.OrderBy(d=>d.defName))
                {
                    if(++checks>256)return p;
                    if(!recipe.fixedIngredientFilter.Allows(def)||!recipe.defaultIngredientFilter.Allows(def))continue;
                    int need=ingredient.CountRequiredOfFor(def,recipe)*p.Batches;
                    if(need<=0)continue;
                    var choices=new List<KeyValuePair<Thing,int>>();int remaining=need;
                    foreach(var t in map.listerThings.ThingsOfDef(def))
                    {
                        if(++checks>2048)return p;
                        if(!t.Spawned||t.IsForbidden(Faction.OfPlayer)||!ingredient.filter.Allows(t)||!worker.CanReserveAndReach(t,PathEndMode.ClosestTouch,Danger.None))continue;
                        int used;p.Ingredients.TryGetValue(t,out used);int take=Math.Min(remaining,t.stackCount-used);
                        if(take<=0)continue;choices.Add(new KeyValuePair<Thing,int>(t,take));remaining-=take;if(remaining==0)break;
                    }
                    if(remaining>0)continue;
                    foreach(var c in choices){int previous;p.Ingredients.TryGetValue(c.Key,out previous);p.Ingredients[c.Key]=previous+c.Value;}
                    permitted.Add(def);supplied=true;break;
                }
                if(!supplied)return p;
            }
            // New kibble can have different ingredient-derived food restrictions from the
            // existing destination stack. Verify the prospective product before simulation.
            if(p.Ingredients.Keys.Any(t=>t.TryGetComp<CompIngredients>()!=null))return p;
            var product=ThingMaker.MakeThing(anchor.Stack.def);
            var productIngredients=product.TryGetComp<CompIngredients>();
            if(productIngredients!=null)foreach(var def in permitted)productIngredients.RegisterIngredient(def);
            var outputAccess=(bool[])anchor.Candidate.Access.Clone();double outputNutrition=double.PositiveInfinity;
            for(int i=0;i<snap.Consumers.Count;i++)
            {
                if(!outputAccess[i])continue;var consumer=snap.Consumers[i];
                if(!FoodUtility.WillEat(consumer,product,consumer)){outputAccess[i]=false;continue;}
                double value=FoodUtility.NutritionForEater(consumer,product);
                if(double.IsNaN(value)||double.IsInfinity(value)||value<=0)return p;
                outputNutrition=Math.Min(outputNutrition,value);
            }
            if(double.IsInfinity(outputNutrition))return p;
            var producedFeed=new FeedCandidate(outputNutrition,outputAccess,anchor.Candidate.FreshDays);
            var debit=BoundIngredientRemoval(snap,recipe,permitted,p.Batches);
            if(debit==null)return p;
            if(debit.Blocked!=null){p.Blocked=debit.Blocked;return p;}
            // Ingredients are consumed by production: prove net benefit with the SAME allocation.
            var beforeProjection=snap.Scenario.Run(plan.Horizon,0,0);
            var after=snap.Scenario.Convert(plan.Horizon,producedFeed,p.Items,debit);
            if(snap.Scenario.AnimalDeficit(after)>=snap.Scenario.AnimalDeficit(beforeProjection)-1e-6||
                snap.Scenario.HumanDeficit(after)>snap.Scenario.HumanDeficit(beforeProjection)+1e-6)return p;
            var withoutOutput=snap.Scenario.Convert(plan.Horizon,producedFeed,0,debit);
            p.DelaySafe=snap.Scenario.HumanDeficit(withoutOutput)<=snap.Scenario.HumanDeficit(beforeProjection)+1e-6&&
                snap.Scenario.AnimalDeficit(withoutOutput)<=snap.Scenario.AnimalDeficit(beforeProjection)+1e-6;
            p.Blocked=null;return p;
        }
        // Vanilla does not use the previewed stacks. With allowMixingIngredients it gathers, per
        // batch and ingredient, ANY reachable unforbidden stacks of every def the bill allows,
        // ordered by nutrition value then distance, until the base count is met; the last stack
        // overshoots by less than one unit (WorkGiver_DoBill.TryFindBestBillIngredientsInSet_AllowMix,
        // RimWorld 1.7.4985). Bound that removal for every candidate stack in the forecast and
        // let IngredientDebit refuse any choice it cannot pin to one pool. Null = unprovable.
        static MaterialDebit BoundIngredientRemoval(ForecastSnapshot snap,RecipeDef recipe,HashSet<ThingDef> permitted,int batches)
        {
            var getter=recipe.IngredientValueGetter;if(getter==null)return null;
            var demands=new List<IngredientDemand>();var owner=new Dictionary<ThingDef,int>();
            var unit=new Dictionary<ThingDef,double>();var maxUnit=new List<double>();
            for(int i=0;i<recipe.ingredients.Count;i++)
            {
                var ingredient=recipe.ingredients[i];
                // A fixed ingredient ignores the bill's filter; any other uses only allowed defs.
                var defs=ingredient.IsFixedIngredient?new List<ThingDef>{ingredient.FixedIngredient}:permitted.Where(d=>ingredient.filter.Allows(d)).ToList();
                double largest=0;
                foreach(var def in defs)
                {
                    // A def satisfying two ingredients could be drawn by either: not bounded.
                    if(def==null||recipe.ingredients.Count(x=>x.filter.Allows(def))!=1||owner.ContainsKey(def))return null;
                    double value=getter.ValuePerUnitOf(def);
                    if(double.IsNaN(value)||double.IsInfinity(value)||value<=0)return null;
                    owner[def]=i;unit[def]=value;largest=Math.Max(largest,value);
                }
                demands.Add(new IngredientDemand());maxUnit.Add(largest);
            }
            var ratio=new double[demands.Count];
            foreach(var entry in snap.Materials)
            {
                int i;if(!owner.TryGetValue(entry.Key.def,out i))continue;
                if(entry.Value.Count<=0)continue;
                ratio[i]=Math.Max(ratio[i],entry.Value.PerItem/unit[entry.Key.def]);
                demands[i].Sources.Add(new IngredientSource(entry.Value.Pool,entry.Value.Count*entry.Value.PerItem));
            }
            for(int i=0;i<demands.Count;i++)
            {
                double baseCount=recipe.ingredients[i].GetBaseCount();
                if(double.IsNaN(baseCount)||double.IsInfinity(baseCount)||baseCount<0)return null;
                // Stat units removed per batch < base + largest unit; forecast units scale by
                // at most the largest per-stack forecast/stat ratio.
                demands[i].MaxRemoval=batches*(baseCount+maxUnit[i])*ratio[i];
            }
            return snap.Scenario.Debit(demands);
        }
        public static bool Execute(Map map,FeedProductionPreview shown,bool automatic)
        {
            var s=AnimalManagementMod.Settings;var state=map.GetComponent<AnimalActionState>();
            var forecast=map.GetComponent<FeedForecast>();
            // Fresh complete scan at the moment of approval/automation; no stale plan is executed.
            forecast.Recalculate();var snap=forecast.Snapshot;
            if(forecast.Failed||snap.Scenario==null||shown.Anchor==null||shown.Bench==null)return false;
            var anchor=snap.Anchors.FirstOrDefault(a=>a.Stack==shown.Anchor.Stack);if(anchor==null)return false;
            var plan=FeedActionPlanner.Plan(snap.Scenario,anchor.Candidate,s.TargetDays,0,0,100000);
            var next=Preview(map,forecast,anchor,plan,shown.Bench);
            if(!next.Ready||(automatic&&!next.DelaySafe)||next.Recipe!=shown.Recipe||next.Items!=shown.Items||!next.Ingredients.OrderBy(x=>x.Key.ThingID).SequenceEqual(shown.Ingredients.OrderBy(x=>x.Key.ThingID)))return false;
            state.CheckOwnership();int now=Find.TickManager.TicksGame;
            bool allowed=state.Permissions.Contains(PermissionKey(shown.Bench,anchor));
            var mode=automatic?s.ProductionMode:s.ProductionMode==ActionMode.Off?ActionMode.Off:ActionMode.Confirm;
            if(!ManagedBillPolicy.CanCreate(mode,allowed,snap.Scenario.Reliable,false,state.Managed.Count,s.MaxBills,now,state.LastAction,s.Cooldown)||state.Revoked.Contains(shown.Bench.ThingID))return false;
            var bill=new Bill_Production(next.Recipe){repeatMode=BillRepeatModeDefOf.RepeatCount,repeatCount=next.Batches};
            bill.ingredientFilter.SetDisallowAll();foreach(var def in next.Ingredients.Keys.Select(t=>t.def).Distinct())bill.ingredientFilter.SetAllow(def,true);
            bill.SetIncludeGroup(anchor.Stack.GetSlotGroup());
            bill.SetStoreMode(BillStoreModeDefOf.SpecificStockpile,anchor.Stack.GetSlotGroup());
            next.Bench.BillStack.AddBill(bill);
            state.Managed.Add(new ManagedFeedBill{Bench=next.Bench,BillId=bill.GetUniqueLoadID(),Fingerprint=Fingerprint(bill),Expires=now+60000,Automatic=automatic,Permission=PermissionKey(shown.Bench,anchor),Batches=next.Batches,LastSeen=bill.repeatCount});
            state.LastAction=now;state.AddHistory(next.Bench.ThingID+" / "+bill.GetUniqueLoadID()+" / "+next.Items);
            return true;
        }
    }
}
