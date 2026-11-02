using System;
using System.Collections.Generic;
using Pengin.AnimalManagement;

static class Program
{
    static int tests;
    static void Check(bool v,string name) {if(!v)throw new Exception(name);tests++;}
    static Demand D(double amount,bool human=false) {return new Demand {Daily=amount,Human=human};}
    static FeedPool F(double amount,params bool[] access) {return new FeedPool {Nutrition=amount,Access=access};}
    static List<FeedPool> None(int day) {return new List<FeedPool>();}
    static void Main()
    {
        OwnershipTests.Run();HerdTests.Run();
        var d=new List<Demand>{D(1),D(1)};
        var pools=new List<FeedPool>{F(10,true,true)};
        var r=NutritionForecast.Run(d,pools,None,7,0,0);
        Check(r.FirstShortageDay==6,"shared stack is not duplicated across species");
        Check(Math.Abs(r.StockConsumed-10)<1e-7,"stock conservation");
        Check(pools[0].Nutrition==10,"snapshot input is immutable");
        r=NutritionForecast.Run(d,new List<FeedPool>{F(100,true,false)},None,7,0,0);
        Check(r.FirstShortageDay==1 && r.Unmet[1]>=7,"incompatible diet cannot borrow surplus");
        Check(r.FirstShortageDays[0]==0 && r.FirstShortageDays[1]==1,"per-consumer shortage dates are independent");
        r=NutritionForecast.Run(new List<Demand>{D(1,true),D(1)},new List<FeedPool>{F(2,true,true)},None,2,0,0);
        Check(r.FirstShortageDay==2 && r.HumanStockConsumed==1,"human consumption competes once");
        Check(r.FirstAnimalShortageDay==2,"animal shortage caused by human competition is reported");
        r=NutritionForecast.Run(new List<Demand>{D(1,true),D(1)},new List<FeedPool>{F(10,false,true)},None,7,0,0);
        Check(r.FirstShortageDay==1 && r.FirstAnimalShortageDay==0,"human-only deficit does not raise an animal feed shortage");
        r=NutritionForecast.Run(d,new List<FeedPool>{F(100,false,false)},None,1,0,0);
        Check(r.FirstShortageDay==1 && r.StockConsumed==0,"inaccessible feed remains unavailable");
        r=NutritionForecast.Run(new List<Demand>{D(1)},new List<FeedPool>(), day => new List<FeedPool>{new FeedPool{Nutrition=day<3?1:0,Pasture=true,Access=new[]{true}}},7,0,0);
        Check(r.FirstShortageDay==3 && r.PastureConsumed==2 && r.StockConsumed==0,"winter stops growth and pasture is separate");
        r=NutritionForecast.Run(new List<Demand>{D(1)},new List<FeedPool>(), day => new List<FeedPool>{new FeedPool{Nutrition=day==1?10:0,Pasture=true,Access=new[]{true}}},2,0,0);
        Check(r.FirstShortageDay==2,"unused daily pasture does not become stock");
        r=NutritionForecast.Run(new List<Demand>{D(1)},new List<FeedPool>{F(30,true)},None,30,1,0);
        Check(r.FirstShortageDay<30 && r.FinalAnimalCount==2,"birth scenario changes headcount and demand");
        r=NutritionForecast.Run(new List<Demand>{D(1)},new List<FeedPool>{F(30,true)},None,30,0,1);
        Check(r.FirstShortageDay==0 && r.FinalAnimalCount==0,"death scenario is bounded at zero");
        r=NutritionForecast.Run(new List<Demand>(),new List<FeedPool>(),None,30,0,0);
        Check(r.FirstShortageDay==0 && r.FinalAnimalCount==0,"empty map has no shortage");
        bool noBaseline=false;try{NutritionForecast.Run(new List<Demand>(),new List<FeedPool>(),None,30,1,0);}catch(ArgumentException){noBaseline=true;}
        Check(noBaseline,"births without a species baseline cannot imply free nutrition");
        bool rejected=false;try{NutritionForecast.Run(new List<Demand>{D(double.NaN)},new List<FeedPool>(),None,7,0,0);}catch(ArgumentException){rejected=true;}
        Check(rejected,"invalid modded numeric data is rejected");
        r=NutritionForecast.Run(new List<Demand>{D(1),D(1)},new List<FeedPool>{F(1,true,true),F(1,true,false)},None,1,0,0);
        Check(r.FirstShortageDay==0,"flow reroutes generalist demand to preserve specialist food");
        r=NutritionForecast.Run(new List<Demand>{D(1)},new List<FeedPool>{new FeedPool{Nutrition=20,Access=new[]{true},LastFreshDay=1}},None,7,0,0);
        Check(r.FirstShortageDay==2&&r.CoveredWholeDays==1&&r.SpoiledNutrition==19,"expired stocks cannot sustain future days");
        // Humans eat food animals cannot use before shared feed.
        r=NutritionForecast.Run(new List<Demand>{D(1,true),D(1)},new List<FeedPool>{F(1,true,false),F(1,true,true)},None,1,0,0);
        Check(r.FirstShortageDay==0,"humans prefer meals over shared animal feed");
        // Animals eat expiring pasture, then standing plants, before stored feed.
        var stockPool=F(5,true);
        r=NutritionForecast.Run(new List<Demand>{D(2)},new List<FeedPool>{stockPool,new FeedPool{Nutrition=1,Standing=true,Access=new[]{true}}},
            day=>new List<FeedPool>{new FeedPool{Nutrition=1,Pasture=true,Access=new[]{true}}},1,0,0);
        Check(r.StockConsumed==0&&r.PastureConsumed==1&&r.StandingConsumed==1,"pasture then standing before stock");
        bool humanStanding=NutritionForecast.Run(new List<Demand>{D(1,true)},new List<FeedPool>{new FeedPool{Nutrition=9,Standing=true,Access=new[]{true}}},None,1,0,0).FirstShortageDay==1;
        Check(humanStanding,"humans never graze standing pen plants");
        // A preferred source must not strand a consumer that only the preferred source can feed.
        r=NutritionForecast.Run(new List<Demand>{D(1),D(1)},new List<FeedPool>{F(1,true,false)},
            day=>new List<FeedPool>{new FeedPool{Nutrition=1,Pasture=true,Access=new[]{true,true}}},1,0,0);
        Check(r.FirstShortageDay==0&&r.PastureConsumed==1&&r.StockConsumed==1,"later stages reroute earlier pasture flow");
        r=NutritionForecast.Run(new List<Demand>{D(1,true),D(1,true)},new List<FeedPool>{F(1,true,true),F(1,false,true)},None,1,0,0);
        Check(r.FirstShortageDay==0,"human stages reroute as well");
        // An excluded (zero-demand) animal does not make a pool "animal feed" for the human stage.
        r=NutritionForecast.Run(new List<Demand>{D(1,true),D(1),D(0)},new List<FeedPool>{F(1,true,true,false),F(1,true,false,true)},None,1,0,0);
        Check(r.FirstShortageDay==0,"zero-demand animals do not block the human-first stage");
        var multi=NutritionForecast.Run(d,new List<FeedPool>{F(20,true,true)},None,new[]{7,15,30},0,0);
        var single=NutritionForecast.Run(d,new List<FeedPool>{F(20,true,true)},None,15,0,0);
        Check(multi[1].FirstShortageDay==single.FirstShortageDay&&Math.Abs(multi[1].StockConsumed-single.StockConsumed)<1e-9&&multi[0].Horizon==7&&multi[2].CoveredWholeDays==single.CoveredWholeDays,"one simulation serves every horizon");
        var scenario=new FeedScenario(d,new List<FeedPool>{F(10,true,true)},day=>None(day),true);
        var plan=FeedActionPlanner.Plan(scenario,new FeedCandidate(1,new[]{true,true},int.MaxValue),7,0,0,100);
        Check(plan.Feasible&&plan.RequiredItems==4,"what-if shares one additional stock across two species");
        Check(scenario.Run(7,0,0).StockConsumed==10,"planner preserves snapshot");
        Check(!FeedActionPlanner.Plan(scenario,new FeedCandidate(1,new[]{true,false},99),7,0,0,100).Feasible,"cannot cure incompatible diet");
        Check(!FeedActionPlanner.Plan(new FeedScenario(d,pools,None,false),new FeedCandidate(1,new[]{true,true},99),7,0,0,100).Reliable,"partial snapshots cannot authorize changes");
        Check(!FeedActionPlanner.Plan(scenario,new FeedCandidate(1,new[]{true,true},1),7,0,0,100).Feasible,"spoiling replacement cannot cover winter");
        foreach(int horizon in new[]{7,15,30})
        {
            var winter=new FeedScenario(new[]{D(1)},new List<FeedPool>(),None,true);
            Check(FeedActionPlanner.Plan(winter,new FeedCandidate(.5,new[]{true},99),horizon,0,0,100).RequiredItems==horizon*2,"winter zero-growth target "+horizon);
        }
        var conversion=new FeedScenario(new[]{D(1,true),D(1)},new[]{F(14,true,true)},None,true);
        var converted=conversion.Convert(7,new FeedCandidate(1,new[]{true,true},99),7,new Dictionary<int,double>{{0,7}});
        Check(conversion.HumanDeficit(converted)==0&&conversion.AnimalDeficit(converted)==0,"conversion debits ingredients before adding output");
        Check(conversion.HumanDeficit(conversion.Convert(7,new FeedCandidate(1,new[]{false,true},99),7,new Dictionary<int,double>{{0,7}}))>0,"human diet change can make a conversion unsafe");
        Check(conversion.Run(7,0,0).StockConsumed==14,"material debits preserve snapshot");
        var delayed=new FeedScenario(new[]{D(1,true),D(1)},new[]{F(30,true,true)},None,true);
        var beforeOutput=delayed.Convert(7,new FeedCandidate(1,new[]{false,true},99),0,new Dictionary<int,double>{{0,7}});
        Check(delayed.HumanDeficit(beforeOutput)==0&&delayed.AnimalDeficit(beforeOutput)==0,"surplus ingredients can be consumed safely before output exists");
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var many=new List<Demand>();for(int i=0;i<100;i++)many.Add(D(1));
        var access=new bool[100];Array.Fill(access,true);
        var food=new List<FeedPool>{F(3000,access)};
        var big=NutritionForecast.Run(many,food,None,30,0,0);timer.Stop();
        Check(big.FirstShortageDay==0,"100 animals x 30 days conserve all stock");
        Console.WriteLine($"PASS {tests} nutrition assertions; 100 animals/30 days model: {timer.Elapsed.TotalMilliseconds:F2} ms (not game scan/TPS)");
    }
}
