using System;
using System.Collections.Generic;
using System.Linq;

namespace Pengin.AnimalManagement
{
    public enum ActionMode { Off, Confirm, Auto }
    // Deep copy at acquisition: a what-if never mutates the published forecast.
    public sealed class FeedScenario
    {
        readonly Demand[] demands;
        readonly FeedPool[] stock;
        readonly List<FeedPool>[] growth;
        public readonly bool Reliable;
        public int Count { get { return demands.Length; } }
        public bool IsAnimal(int i) { return !demands[i].Human; }
        public double Daily(int i) { return demands[i].Daily; }
        static FeedPool Copy(FeedPool p) {return new FeedPool{Nutrition=p.Nutrition,Access=(bool[])p.Access.Clone(),Pasture=p.Pasture,Standing=p.Standing,LastFreshDay=p.LastFreshDay};}
        public FeedScenario(IList<Demand> consumers,IList<FeedPool> pools,Func<int,List<FeedPool>> dailyGrowth,bool reliable)
        {
            demands=consumers.Select(d=>new Demand{Daily=d.Daily,Human=d.Human}).ToArray();
            stock=pools.Select(Copy).ToArray();growth=new List<FeedPool>[30];
            for(int d=1;d<=30;d++)growth[d-1]=dailyGrowth(d).Select(Copy).ToList();
            Reliable=reliable;
        }
        public Projection Run(int days,int births,int deaths,FeedCandidate candidate=null,int items=0,double[] factors=null)
        {
            var pools=stock.ToList();
            if(candidate!=null&&items>0)pools.Add(new FeedPool{Nutrition=candidate.Nutrition*items,Access=candidate.Access,LastFreshDay=candidate.FreshDays});
            // Demands are deep-copied on construction; NutritionForecast only reads them.
            // Avoid one allocation per what-if when all demand factors are unchanged.
            var scaled=factors==null?demands:demands.Select((d,i)=>new Demand{Human=d.Human,Daily=d.Daily*factors[i]}).ToArray();
            return NutritionForecast.Run(scaled,pools,d=>growth[d-1],days,births,deaths);
        }
        // Bound every ingredient removal vanilla may choose against this snapshot's stock.
        public MaterialDebit Debit(IList<IngredientDemand> ingredients){return IngredientDebit.Build(stock,ingredients);}
        public Projection Convert(int days,FeedCandidate output,int items,MaterialDebit debit)
        {
            if(debit==null||debit.Blocked!=null)throw new ArgumentException("Unbounded material debit");
            return Convert(days,output,items,debit.Debits);
        }
        public Projection Convert(int days,FeedCandidate output,int items,IDictionary<int,double> debits)
        {
            var pools=stock.Select(Copy).ToList();
            foreach(var debit in debits)
            {
                if(debit.Key<0||debit.Key>=pools.Count||debit.Value<0||debit.Value>pools[debit.Key].Nutrition+1e-6)throw new ArgumentException("Invalid material debit");
                pools[debit.Key].Nutrition=Math.Max(0,pools[debit.Key].Nutrition-debit.Value);
            }
            pools.Add(new FeedPool{Nutrition=output.Nutrition*items,Access=output.Access,LastFreshDay=output.FreshDays});
            return NutritionForecast.Run(demands,pools,d=>growth[d-1],days,0,0);
        }
        public double AnimalDeficit(Projection p) {return p.Unmet.Where((n,i)=>!demands[i].Human).Sum();}
        public double HumanDeficit(Projection p) {return p.Unmet.Where((n,i)=>demands[i].Human).Sum();}
    }
    public sealed class FeedCandidate
    {
        public readonly double Nutrition;
        public readonly bool[] Access;
        public readonly int FreshDays;
        public FeedCandidate(double nutrition,bool[] access,int freshDays)
        {
            if(double.IsNaN(nutrition)||double.IsInfinity(nutrition)||nutrition<=0||access==null)throw new ArgumentException("Invalid candidate");
            Nutrition=nutrition;Access=(bool[])access.Clone();FreshDays=freshDays;
        }
    }
    public sealed class FeedActionPlan
    {
        public bool Feasible, Reliable;
        public int RequiredItems, Horizon;
        public double Deficit;
    }
    public static class FeedActionPlanner
    {
        // At most 19 simulations for the supported item cap; never run per tick/frame.
        public static FeedActionPlan Plan(FeedScenario scenario,FeedCandidate candidate,int days,int births,int deaths,int cap)
        {
            if(candidate.Access.Length!=scenario.Count||cap<0||cap>100000)throw new ArgumentException("Invalid planning bounds");
            var baseline=scenario.Run(days,births,deaths);
            var plan=new FeedActionPlan{Horizon=days,Reliable=scenario.Reliable,Deficit=scenario.AnimalDeficit(baseline)};
            Func<int,bool> covers=n=>{
                var p=scenario.Run(days,births,deaths,candidate,n);
                return scenario.AnimalDeficit(p)<1e-6&&scenario.HumanDeficit(p)<=scenario.HumanDeficit(baseline)+1e-6;
            };
            // The baseline is precisely the zero-addition simulation; reuse it.
            if(scenario.AnimalDeficit(baseline)<1e-6){plan.Feasible=true;return plan;}
            if(!covers(cap))return plan;
            int low=0,high=cap;
            while(high-low>1){int mid=low+(high-low)/2;if(covers(mid))high=mid;else low=mid;}
            plan.RequiredItems=high;plan.Feasible=true;return plan;
        }
    }
}
