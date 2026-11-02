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
            var scaled=demands.Select((d,i)=>new Demand{Human=d.Human,Daily=d.Daily*(factors==null?1:factors[i])}).ToArray();
            return NutritionForecast.Run(scaled,pools,d=>growth[d-1],days,births,deaths);
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
            if(covers(0)){plan.Feasible=true;return plan;}
            if(!covers(cap))return plan;
            int low=0,high=cap;
            while(high-low>1){int mid=low+(high-low)/2;if(covers(mid))high=mid;else low=mid;}
            plan.RequiredItems=high;plan.Feasible=true;return plan;
        }
    }
}
