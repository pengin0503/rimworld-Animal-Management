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
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var many=new List<Demand>();for(int i=0;i<100;i++)many.Add(D(1));
        var access=new bool[100];Array.Fill(access,true);
        var food=new List<FeedPool>{F(3000,access)};
        var big=NutritionForecast.Run(many,food,None,30,0,0);timer.Stop();
        Check(big.FirstShortageDay==0,"100 animals x 30 days conserve all stock");
        Console.WriteLine($"PASS {tests} nutrition assertions; 100 animals/30 days model: {timer.Elapsed.TotalMilliseconds:F2} ms (not game scan/TPS)");
    }
}
