using System;
using System.Collections.Generic;
using System.Linq;

namespace Pengin.AnimalManagement
{
    public sealed class Demand { public double Daily; public bool Human; }
    public sealed class FeedPool { public double Nutrition; public bool[] Access; public bool Pasture; public int LastFreshDay=int.MaxValue; }
    public sealed class Projection
    {
        public int FirstShortageDay;
        public int[] FirstShortageDays;
        public double StockConsumed, HumanStockConsumed, PastureConsumed, FinalAnimalCount;
        public double SpoiledNutrition;
        public int CoveredWholeDays;
        public double[] Unmet;
    }

    // Nutrition-only scenario, not an assertion about the game's actual feeding order.
    // Each edge carries nutrition once. Human demand is allocated first; unused pasture
    // expires daily so today's modeled regrowth cannot inflate future stored feed.
    public static class NutritionForecast
    {
        const double Epsilon=1e-7;
        static bool Valid(double n) {return !double.IsNaN(n)&&!double.IsInfinity(n)&&n>=0;}
        public static Projection Run(IList<Demand> demands,IList<FeedPool> stocks,
            Func<int,List<FeedPool>> growth,int horizon,int births,int deaths)
        {
            if(horizon<1||horizon>30||births<0||deaths<0)throw new ArgumentException("Invalid scenario");
            if(demands.Any(d=>d==null||!Valid(d.Daily)))throw new ArgumentException("Invalid demand");
            var stock=stocks.Select(p=>Copy(p,demands.Count)).ToList();
            var result=new Projection {Unmet=new double[demands.Count],FirstShortageDays=new int[demands.Count]};
            int animals=demands.Count(d=>!d.Human);
            if(animals==0&&(births>0||deaths>0))throw new ArgumentException("Population scenario requires a species baseline");
            for(int day=1;day<=horizon;day++)
            {
                foreach(FeedPool pool in stock)
                    if(day>pool.LastFreshDay){result.SpoiledNutrition+=pool.Nutrition;pool.Nutrition=0;}
                double count=Math.Max(0,animals+((double)births-deaths)*day/30.0);
                double scale=animals==0?0:count/animals;
                result.FinalAnimalCount=count;
                var pools=growth(day).Select(p=>Copy(p,demands.Count)).Where(p=>p.Pasture).Concat(stock).ToList();
                double[] need=demands.Select(d=>d.Daily*(d.Human?1:scale)).ToArray();
                // An explicit conservative feeding-order assumption, visible in the UI.
                Allocate(pools,demands,need,true,result);
                Allocate(pools,demands,need,false,result);
                for(int i=0;i<need.Length;i++)
                {
                    result.Unmet[i]+=need[i];
                    if(need[i]>Epsilon)
                    {
                        if(result.FirstShortageDay==0)result.FirstShortageDay=day;
                        if(result.FirstShortageDays[i]==0)result.FirstShortageDays[i]=day;
                    }
                }
            }
            result.CoveredWholeDays=result.FirstShortageDay==0?horizon:result.FirstShortageDay-1;
            return result;
        }
        static FeedPool Copy(FeedPool p,int count)
        {
            if(p==null||!Valid(p.Nutrition)||p.Access==null||p.Access.Length!=count)
                throw new ArgumentException("Invalid feed pool");
            return new FeedPool{Nutrition=p.Nutrition,Access=p.Access,Pasture=p.Pasture,LastFreshDay=p.LastFreshDay};
        }
        sealed class Edge {public int To,Reverse;public double Capacity;}
        static Edge Add(List<Edge>[] graph,int a,int b,double capacity)
        {
            var edge=new Edge{To=b,Reverse=graph[b].Count,Capacity=capacity};
            graph[a].Add(edge);
            graph[b].Add(new Edge{To=a,Reverse=graph[a].Count-1,Capacity=0});
            return edge;
        }
        static void Allocate(List<FeedPool> pools,IList<Demand> demands,double[] need,bool humans,Projection result)
        {
            int sink=pools.Count+demands.Count+1;
            var graph=new List<Edge>[sink+1];for(int i=0;i<graph.Length;i++)graph[i]=new List<Edge>();
            var sourceEdges=new Edge[pools.Count];var sinkEdges=new Edge[demands.Count];
            for(int p=0;p<pools.Count;p++)
            {
                FeedPool pool=pools[p];
                sourceEdges[p]=Add(graph,0,p+1,pool.Nutrition);
                if(humans&&pool.Pasture)continue;
                for(int a=0;a<demands.Count;a++)
                    if(demands[a].Human==humans&&pool.Access[a]&&need[a]>Epsilon)
                        Add(graph,p+1,pools.Count+a+1,pool.Nutrition);
            }
            for(int a=0;a<demands.Count;a++)
                if(demands[a].Human==humans)sinkEdges[a]=Add(graph,pools.Count+a+1,sink,need[a]);
            var level=new int[graph.Length];var next=new int[graph.Length];
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
            for(int p=0;p<pools.Count;p++)
            {
                double consumed=Math.Max(0,pools[p].Nutrition-sourceEdges[p].Capacity);
                pools[p].Nutrition=sourceEdges[p].Capacity;
                if(pools[p].Pasture)result.PastureConsumed+=consumed;
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
