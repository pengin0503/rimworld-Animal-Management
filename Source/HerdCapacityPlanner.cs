using System;
using System.Linq;
namespace Pengin.AnimalManagement
{
    public sealed class HerdCapacityPlan
    {
        public int Current,Sustainable,Horizon;
        public bool Feasible,Reliable,AtLimit;
    }
    public static class HerdCapacityPlanner
    {
        public static HerdCapacityPlan Calculate(FeedScenario scenario,bool[] species,int days,int cap,int births,int deaths)
        {
            if(species.Length!=scenario.Count||cap<0||cap>400)throw new ArgumentException("Invalid herd bounds");
            int count=species.Count(x=>x);
            if(count==0)throw new ArgumentException("A species baseline is required");
            for(int i=0;i<species.Length;i++)if(species[i]&&!scenario.IsAnimal(i))throw new ArgumentException("Humans are not herd capacity");
            var result=new HerdCapacityPlan{Current=count,Horizon=days,Reliable=scenario.Reliable};
            // Other consumers may already lack food. A selected species can still
            // have a meaningful capacity provided no other consumer loses nutrition.
            var baseline=scenario.Run(days,births,deaths);
            Func<int,bool> covers=n=>{
                double[] factors=species.Select(x=>x?(double)n/count:1).ToArray();
                var projection=scenario.Run(days,births,deaths,null,0,factors);
                for(int i=0;i<species.Length;i++)
                {
                    if(species[i])
                    {
                        if(projection.Unmet[i]>1e-6)return false;
                    }
                    else if(projection.Unmet[i]>baseline.Unmet[i]+1e-6)return false;
                }
                return true;
            };
            if(!covers(0))return result;
            result.Feasible=true;
            int low=0,high=cap+1;
            while(high-low>1){int mid=low+(high-low)/2;if(covers(mid))low=mid;else high=mid;}
            result.Sustainable=low;result.AtLimit=low==cap;return result;
        }
    }
}
