using System;
using System.Collections.Generic;
using System.Linq;

namespace Pengin.AnimalManagement
{
    public sealed class Demand { public double Daily; public bool Human; }
    // Pasture: today's natural growth, expires daily. Standing: plants already growing in a
    // pen, eaten once and not replenished. Neither is ever available to humans.
    public sealed class FeedPool { public double Nutrition; public bool[] Access; public bool Pasture, Standing; public int LastFreshDay=int.MaxValue; }
    public sealed class Projection
    {
        public int Horizon;
        public int FirstShortageDay;
        public int[] FirstShortageDays;
        public double StockConsumed, HumanStockConsumed, PastureConsumed, StandingConsumed, FinalAnimalCount;
        public double SpoiledNutrition;
        public int CoveredWholeDays;
        public double[] Unmet;
        // Animal-only: a human food deficit is reported separately and must not raise feed warnings.
        public int FirstAnimalShortageDay;
        public Projection Clone(int horizon)
        {
            var p=(Projection)MemberwiseClone();
            p.Horizon=horizon;
            p.FirstShortageDays=(int[])FirstShortageDays.Clone();
            p.Unmet=(double[])Unmet.Clone();
            p.CoveredWholeDays=FirstShortageDay==0?horizon:FirstShortageDay-1;
            return p;
        }
    }

    // Nutrition-only scenario, not an assertion about the game's actual feeding order.
    // Each edge carries nutrition once. Feeding order is staged and stated in the UI:
    // humans eat food animals cannot use first, then shared food; animals eat today's
    // pasture first (it expires), then standing pen plants, then stored feed.
    public static class NutritionForecast
    {
        const double Epsilon=1e-7;
        static bool Valid(double n) {return !double.IsNaN(n)&&!double.IsInfinity(n)&&n>=0;}
        public static Projection Run(IList<Demand> demands,IList<FeedPool> stocks,
            Func<int,List<FeedPool>> growth,int horizon,int births,int deaths)
        {
            return Run(demands,stocks,growth,new[]{horizon},births,deaths)[0];
        }
        // One simulation serves every requested horizon: the first N days do not depend
        // on how many days follow, so shorter horizons are prefixes of the longest one.
        public static Projection[] Run(IList<Demand> demands,IList<FeedPool> stocks,
            Func<int,List<FeedPool>> growth,int[] horizons,int births,int deaths)
        {
            if(horizons==null||horizons.Length==0||horizons.Any(h=>h<1||h>30)||births<0||deaths<0)throw new ArgumentException("Invalid scenario");
            if(demands.Any(d=>d==null||!Valid(d.Daily)))throw new ArgumentException("Invalid demand");
            var stock=stocks.Select(p=>Copy(p,demands.Count)).ToList();
            var result=new Projection {Unmet=new double[demands.Count],FirstShortageDays=new int[demands.Count]};
            var output=new Projection[horizons.Length];
            int animals=demands.Count(d=>!d.Human);
            if(animals==0&&(births>0||deaths>0))throw new ArgumentException("Population scenario requires a species baseline");
            bool[] animal=demands.Select(d=>!d.Human).ToArray();
            int last=horizons.Max();
            for(int day=1;day<=last;day++)
            {
                foreach(FeedPool pool in stock)
                    if(day>pool.LastFreshDay){result.SpoiledNutrition+=pool.Nutrition;pool.Nutrition=0;}
                double count=Math.Max(0,animals+((double)births-deaths)*day/30.0);
                double scale=animals==0?0:count/animals;
                result.FinalAnimalCount=count;
                var pools=growth(day).Select(p=>Copy(p,demands.Count)).Where(p=>p.Pasture).Concat(stock).ToList();
                double[] need=demands.Select(d=>d.Daily*(d.Human?1:scale)).ToArray();
                Allocate(pools,demands,need,true,result,p=>!AnyAccess(p,animal),p=>true);
                Allocate(pools,demands,need,false,result,p=>p.Pasture,p=>p.Standing,p=>true);
                for(int i=0;i<need.Length;i++)
                {
                    result.Unmet[i]+=need[i];
                    if(need[i]>Epsilon)
                    {
                        if(result.FirstShortageDay==0)result.FirstShortageDay=day;
                        if(!demands[i].Human&&result.FirstAnimalShortageDay==0)result.FirstAnimalShortageDay=day;
                        if(result.FirstShortageDays[i]==0)result.FirstShortageDays[i]=day;
                    }
                }
                for(int h=0;h<horizons.Length;h++)if(horizons[h]==day)output[h]=result.Clone(day);
            }
            return output;
        }
        static bool AnyAccess(FeedPool pool,bool[] who)
        {
            for(int i=0;i<who.Length;i++)if(who[i]&&pool.Access[i])return true;
            return false;
        }
        static FeedPool Copy(FeedPool p,int count)
        {
            if(p==null||!Valid(p.Nutrition)||p.Access==null||p.Access.Length!=count)
                throw new ArgumentException("Invalid feed pool");
            return new FeedPool{Nutrition=p.Nutrition,Access=p.Access,Pasture=p.Pasture,Standing=p.Standing,LastFreshDay=p.LastFreshDay};
        }
        sealed class Edge {public int To,Reverse;public double Capacity;}
        static Edge Add(List<Edge>[] graph,int a,int b,double capacity)
        {
            var edge=new Edge{To=b,Reverse=graph[b].Count,Capacity=capacity};
            graph[a].Add(edge);
            graph[b].Add(new Edge{To=a,Reverse=graph[a].Count-1,Capacity=0});
            return edge;
        }
        // Staged preference inside ONE residual graph: each stage only opens more source
        // capacity, then augments again. Later stages can reroute earlier flow through the
        // reverse edges, so preferring a source never lowers the total that can be fed,
        // and flow already taken from a preferred source is never given back.
        static void Allocate(List<FeedPool> pools,IList<Demand> demands,double[] need,bool humans,Projection result,params Func<FeedPool,bool>[] stages)
        {
            bool anyNeed=false;
            for(int a=0;a<demands.Count;a++)if(demands[a].Human==humans&&need[a]>Epsilon){anyNeed=true;break;}
            if(!anyNeed)return;
            int sink=pools.Count+demands.Count+1;
            var graph=new List<Edge>[sink+1];for(int i=0;i<graph.Length;i++)graph[i]=new List<Edge>();
            var sourceEdges=new Edge[pools.Count];var sinkEdges=new Edge[demands.Count];
            var usable=new bool[pools.Count];var opened=new bool[pools.Count];
            for(int p=0;p<pools.Count;p++)
            {
                FeedPool pool=pools[p];
                usable[p]=pool.Nutrition>Epsilon&&!(humans&&(pool.Pasture||pool.Standing));
                sourceEdges[p]=Add(graph,0,p+1,0);
                if(!usable[p])continue;
                for(int a=0;a<demands.Count;a++)
                    if(demands[a].Human==humans&&pool.Access[a]&&need[a]>Epsilon)
                        Add(graph,p+1,pools.Count+a+1,pool.Nutrition);
            }
            for(int a=0;a<demands.Count;a++)
                if(demands[a].Human==humans)sinkEdges[a]=Add(graph,pools.Count+a+1,sink,need[a]);
            var level=new int[graph.Length];var next=new int[graph.Length];
            foreach(Func<FeedPool,bool> stage in stages)
            {
                for(int p=0;p<pools.Count;p++)
                    if(usable[p]&&!opened[p]&&stage(pools[p])){opened[p]=true;sourceEdges[p].Capacity=pools[p].Nutrition;}
                while(true)
                {
                    for(int i=0;i<level.Length;i++)level[i]=-1;
                    var queue=new Queue<int>();queue.Enqueue(0);level[0]=0;
                    while(queue.Count>0)
                    {
                        int u=queue.Dequeue();foreach(Edge e in graph[u])
                            if(e.Capacity>Epsilon&&level[e.To]<0){level[e.To]=level[u]+1;queue.Enqueue(e.To);}
                    }
                    if(level[sink]<0)break;
                    Array.Clear(next,0,next.Length);
                    while(Send(graph,level,next,0,sink,double.MaxValue)>Epsilon){}
                }
            }
            for(int p=0;p<pools.Count;p++)
            {
                if(!opened[p])continue;
                double consumed=Math.Max(0,pools[p].Nutrition-sourceEdges[p].Capacity);
                pools[p].Nutrition=sourceEdges[p].Capacity;
                if(pools[p].Pasture)result.PastureConsumed+=consumed;
                else if(pools[p].Standing)result.StandingConsumed+=consumed;
                else {result.StockConsumed+=consumed;if(humans)result.HumanStockConsumed+=consumed;}
            }
            for(int a=0;a<need.Length;a++)if(sinkEdges[a]!=null)need[a]=Math.Max(0,sinkEdges[a].Capacity);
        }
        static double Send(List<Edge>[] graph,int[] level,int[] next,int u,int sink,double flow)
        {
            if(u==sink)return flow;
            for(;next[u]<graph[u].Count;next[u]++)
            {
                Edge edge=graph[u][next[u]];
                if(edge.Capacity<=Epsilon||level[edge.To]!=level[u]+1)continue;
                double sent=Send(graph,level,next,edge.To,sink,Math.Min(flow,edge.Capacity));
                if(sent<=Epsilon)continue;
                edge.Capacity-=sent;graph[edge.To][edge.Reverse].Capacity+=sent;return sent;
            }
            return 0;
        }
    }
}
