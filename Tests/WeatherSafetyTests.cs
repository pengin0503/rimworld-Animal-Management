using System;
using System.Collections.Generic;
using Pengin.AnimalManagement;
static class WeatherSafetyTests
{
    static List<FeedPool> None(int day){return new List<FeedPool>();}
    static FeedPool Stock(double nutrition,params bool[] access){return new FeedPool{Nutrition=nutrition,Access=access};}
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var demands=new[]{new Demand{Daily=2},new Demand{Daily=1,Human=true}};
        var pools=new[]{Stock(9,true,true)};
        var dry=new FeedScenario(demands,pools,None,true);
        var growing=new FeedScenario(demands,pools,d=>new List<FeedPool>{new FeedPool{Nutrition=0.5,Pasture=true,Access=new[]{true,false}}},false);
        var feed=new FeedCandidate(1,new[]{true,false},int.MaxValue);
        var debit=IngredientDebit.Build(pools,new[]{new IngredientDemand()});
        Assert(WeatherFeedSafety.CanImprove(growing,dry,feed,4,debit,7),"moderate growth and dry pasture both benefit");
        var abundant=new FeedScenario(demands,pools,d=>new List<FeedPool>{new FeedPool{Nutrition=3,Pasture=true,Access=new[]{true,false}}},false);
        Assert(!WeatherFeedSafety.CanImprove(abundant,dry,feed,4,debit,7),"no production when pasture already meets demand");
        var harmful=IngredientDebit.Build(pools,new[]{new IngredientDemand{MaxRemoval=8,Sources={new IngredientSource(0,8)}}});
        Assert(!WeatherFeedSafety.CanImprove(growing,dry,feed,4,harmful,7),"feed production must not harm shared food safety");
        Assert(!WeatherFeedSafety.CanImprove(growing,dry,feed,4,new MaterialDebit{Blocked="x"},7),"unbounded ingredients never auto");
        Assert(!WeatherFeedSafety.CanImprove(growing,null,feed,4,debit,7),"no conservative scenario is not proof");
        Console.WriteLine("PASS 5 weather-feed safety assertions");
    }
}
