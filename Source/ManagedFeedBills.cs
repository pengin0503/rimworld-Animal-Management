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
        public static string Fingerprint(Bill_Production b)
        {
            var fields=typeof(Bill_Production).GetFields(BindingFlags.Public|BindingFlags.Instance)
                .Where(f=>f.Name!="billStack"&&f.Name!="loadID"&&f.Name!="nextTickToSearchForIngredients"&&f.Name!="ingredientFilter").OrderBy(f=>f.Name);
            string result=string.Join("|",fields.Select(f=>f.Name+"="+Value(f.GetValue(b))));
            result+="|"+b.RenamableLabel+"|"+b.GetStoreMode().defName+"|"+Value(b.GetSlotGroup())+"|"+Value(b.GetIncludeSlotGroup());
            result+="|"+string.Join(",",b.ingredientFilter.AllowedThingDefs.Select(d=>d.defName).OrderBy(n=>n));
            foreach(FieldInfo f in typeof(ThingFilter).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(f=>f.Name=="disallowedSpecialFilters"||f.Name=="allowedHitPointsPercents"||f.Name=="allowedQualities"))
            {
                var sequence=f.GetValue(b.ingredientFilter) as System.Collections.IEnumerable;
                result+="|"+f.Name+"="+(sequence==null?Value(f.GetValue(b.ingredientFilter)):string.Join(",",sequence.Cast<object>().Select(Value).OrderBy(n=>n)));
            }
            return result+"|index="+b.billStack.IndexOf(b);
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
            var debits=new Dictionary<int,double>();
            // Vanilla may select another stack of the permitted def. Debit the worst-case
            // amount from EVERY reachable pool, not merely the displayed ingredient stacks.
            foreach(var def in permitted)
            {
                if(recipe.ingredients.Count(i=>i.filter.Allows(def))!=1)return p;
                int count=p.Ingredients.Where(c=>c.Key.def==def).Sum(c=>c.Value);
                foreach(var group in snap.Materials.Where(c=>c.Key.def==def).GroupBy(c=>c.Value.Pool))
                {
                    double total=group.Sum(c=>c.Key.stackCount*c.Value.PerItem);
                    debits[group.Key]=Math.Min(total,count*group.Max(c=>c.Value.PerItem));
                }
            }
            // Ingredients are consumed by production: prove net benefit with the SAME allocation.
            var beforeProjection=snap.Scenario.Run(plan.Horizon,0,0);
            var after=snap.Scenario.Convert(plan.Horizon,producedFeed,p.Items,debits);
            if(snap.Scenario.AnimalDeficit(after)>=snap.Scenario.AnimalDeficit(beforeProjection)-1e-6||
                snap.Scenario.HumanDeficit(after)>snap.Scenario.HumanDeficit(beforeProjection)+1e-6)return p;
            var withoutOutput=snap.Scenario.Convert(plan.Horizon,producedFeed,0,debits);
            p.DelaySafe=snap.Scenario.HumanDeficit(withoutOutput)<=snap.Scenario.HumanDeficit(beforeProjection)+1e-6&&
                snap.Scenario.AnimalDeficit(withoutOutput)<=snap.Scenario.AnimalDeficit(beforeProjection)+1e-6;
            p.Blocked=null;return p;
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
            state.Managed.Add(new ManagedFeedBill{Bench=next.Bench,BillId=bill.GetUniqueLoadID(),Fingerprint=Fingerprint(bill),Expires=now+60000,Automatic=automatic,Permission=PermissionKey(shown.Bench,anchor)});
            state.LastAction=now;state.AddHistory(next.Bench.ThingID+" / "+bill.GetUniqueLoadID()+" / "+next.Items);
            return true;
        }
    }
}
