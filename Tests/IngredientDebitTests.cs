using System;
using System.Collections.Generic;
using System.Linq;
using Pengin.AnimalManagement;

// Issue #25: ingredient debits for kibble conversion.
static class IngredientDebitTests
{
    static int tests;
    static void Check(bool v,string name){if(!v)throw new Exception(name);tests++;}
    static Demand D(double amount,bool human=false){return new Demand{Daily=amount,Human=human};}
    static FeedPool F(double amount,int fresh,params bool[] access){return new FeedPool{Nutrition=amount,Access=access,LastFreshDay=fresh};}
    static List<FeedPool> None(int day){return new List<FeedPool>();}
    static IngredientDemand I(double max,params (int pool,double n)[] sources)
    {var d=new IngredientDemand{MaxRemoval=max};foreach(var s in sources)d.Sources.Add(new IngredientSource(s.pool,s.n));return d;}
    static double Get(MaterialDebit d,int pool){double v;return d.Debits.TryGetValue(pool,out v)?v:0;}
    public static void Run()
    {
        // Two ingredient defs (meat + vegetable) in ONE pool: both removals are debited.
        var shared=new List<FeedPool>{F(2,int.MaxValue,true,true)};
        var debit=IngredientDebit.Build(shared,new[]{I(1,(0,1)),I(1,(0,1))});
        Check(debit.Blocked==null&&Math.Abs(Get(debit,0)-2)<1e-9,"two defs in one pool are summed, not overwritten");
        // Reproduction of the reported unsafe approval: human + animal share 2 nutrition,
        // kibble is animal-only. The pre-fix per-def assignment debited only 1.
        var scenario=new FeedScenario(new[]{D(1,true),D(1)},shared,None,true);
        var kibble=new FeedCandidate(1,new[]{false,true},int.MaxValue);
        var before=scenario.Run(1,0,0);
        var overwritten=new Dictionary<int,double>();foreach(var amount in new[]{1.0,1.0})overwritten[0]=amount;
        var legacy=scenario.Convert(1,kibble,1,overwritten);
        Check(scenario.HumanDeficit(legacy)<=scenario.HumanDeficit(before)+1e-6,"pre-fix overwrite hid the human food loss (reproduction)");
        var fixedAfter=scenario.Convert(1,kibble,1,debit);
        Check(scenario.HumanDeficit(fixedAfter)>scenario.HumanDeficit(before)+1e-6,"summed debit exposes the human food loss");
        // A pool only supplies what its candidate stacks hold.
        debit=IngredientDebit.Build(new List<FeedPool>{F(10,int.MaxValue,true)},new[]{I(5,(0,3))});
        Check(Math.Abs(Get(debit,0)-3)<1e-9,"debit capped by candidate stacks");
        // One def in several pools: vanilla's split is not fixed. Neither the pre-fix
        // "full bound from every pool" (double debit) nor any guessed order is accepted.
        var two=new List<FeedPool>{F(5,int.MaxValue,true,true),F(5,10,true,false)};
        debit=IngredientDebit.Build(two,new[]{I(3,(0,5),(1,5))});
        Check(debit.Blocked=="AM_IngredientPools"&&debit.Debits.Count==0,"multi-pool ingredient is refused, never double debited");
        // Reproduction of why a superset-access "least valuable pool" order is unsafe here:
        // removing human-only food can be worse for humans than removing shared food.
        // Pools: 0 shared human+animal, 1 human-only, 2 animal-only.
        var noOutput=new FeedCandidate(1,new[]{false,true},int.MaxValue);
        var humansHurt=new FeedScenario(new[]{D(1,true),D(1)},new List<FeedPool>{F(1,int.MaxValue,true,true),F(2,int.MaxValue,true,false),F(10,int.MaxValue,false,true)},None,true);
        Check(humansHurt.HumanDeficit(humansHurt.Convert(2,noOutput,0,new Dictionary<int,double>{{1,1}}))>
            humansHurt.HumanDeficit(humansHurt.Convert(2,noOutput,0,new Dictionary<int,double>{{0,1}}))+1e-6,"removing human-only food can hurt humans more");
        var animalsHurt=new FeedScenario(new[]{D(.5,true),D(1)},new List<FeedPool>{F(1,int.MaxValue,true,true),F(1,int.MaxValue,true,false)},None,true);
        Check(animalsHurt.AnimalDeficit(animalsHurt.Convert(1,noOutput,0,new Dictionary<int,double>{{0,.5}}))>
            animalsHurt.AnimalDeficit(animalsHurt.Convert(1,noOutput,0,new Dictionary<int,double>{{1,.5}}))+1e-6,"removing shared food can hurt animals more");
        // Distinct ingredients in distinct single pools are independent.
        debit=IngredientDebit.Build(two,new[]{I(2,(0,4)),I(1,(1,2))});
        Check(debit.Blocked==null&&Math.Abs(Get(debit,0)-2)<1e-9&&Math.Abs(Get(debit,1)-1)<1e-9,"each ingredient in its own pool");
        // Invalid or overlapping inputs are unprovable.
        Check(IngredientDebit.Build(shared,new[]{I(1,(1,1))}).Blocked!=null,"unknown pool rejected");
        Check(IngredientDebit.Build(shared,new[]{I(double.NaN,(0,1))}).Blocked!=null,"NaN removal rejected");
        Check(IngredientDebit.Build(shared,new[]{I(1,(0,2)),I(1,(0,1))}).Blocked!=null,"claims beyond pool nutrition rejected");
        Check(IngredientDebit.Build(new List<FeedPool>{new FeedPool{Nutrition=3,Pasture=true,Access=new[]{true}}},new[]{I(1,(0,1))}).Blocked!=null,"pasture is never an ingredient");
        Check(IngredientDebit.Build(shared,new[]{I(0,(0,1))}).Debits.Count==0,"zero removal debits nothing");
        Check(IngredientDebit.Build(shared,new[]{I(1)}).Debits.Count==0,"no modeled candidate stack debits nothing");
        bool rejected=false;try{scenario.Convert(1,kibble,1,IngredientDebit.Build(two,new[]{I(3,(0,5),(1,5))}));}catch(ArgumentException){rejected=true;}
        Check(rejected,"a refused debit cannot be simulated as safe");
        Check(scenario.Run(1,0,0).StockConsumed==before.StockConsumed&&shared[0].Nutrition==2,"debits preserve snapshot");
        Soundness();
        Console.WriteLine($"PASS {tests} ingredient debit assertions (#25)");
    }
    // Any removal vanilla can make within the bound (single pool per ingredient, from
    // nothing up to the bound) must leave the forecast no worse than the approved debit.
    static void Soundness()
    {
        var random=new Random(25);
        int checkedCases=0;
        for(int round=0;round<500;round++)
        {
            int consumers=random.Next(2,5);
            var demands=Enumerable.Range(0,consumers).Select(i=>D(random.Next(1,4)*.5,i==0||random.NextDouble()<.2)).ToArray();
            int poolCount=random.Next(1,5);
            var pools=new List<FeedPool>();
            for(int p=0;p<poolCount;p++)
            {
                var access=Enumerable.Range(0,consumers).Select(i=>random.NextDouble()<.65).ToArray();
                if(!access.Any())access[random.Next(consumers)]=true;
                pools.Add(F(random.Next(2,12),random.NextDouble()<.5?int.MaxValue:random.Next(2,9),access));
            }
            var ingredients=new List<IngredientDemand>();var home=new List<int>();var avail=new List<double>();
            for(int k=0;k<2;k++)
            {
                int p=random.Next(poolCount);double amount=Math.Min(pools[p].Nutrition/2,random.Next(1,6));
                ingredients.Add(I(random.Next(1,8),(p,amount)));home.Add(p);avail.Add(amount);
            }
            var scenario=new FeedScenario(demands,pools,None,true);
            var debit=scenario.Debit(ingredients);
            Check(debit.Blocked==null,"valid random debit");
            var output=new FeedCandidate(1,Enumerable.Range(0,consumers).Select(i=>!demands[i].Human).ToArray(),int.MaxValue);
            int items=random.Next(0,6),days=random.Next(1,10);
            var worst=scenario.Convert(days,output,items,debit);
            for(int trial=0;trial<12;trial++)
            {
                var actual=new Dictionary<int,double>();
                for(int k=0;k<ingredients.Count;k++)
                {
                    double take=Math.Min(avail[k],ingredients[k].MaxRemoval)*(trial==0?1:random.NextDouble());
                    double sum;actual.TryGetValue(home[k],out sum);actual[home[k]]=sum+take;
                }
                var real=scenario.Convert(days,output,items,actual);
                Check(scenario.HumanDeficit(real)<=scenario.HumanDeficit(worst)+1e-6&&scenario.AnimalDeficit(real)<=scenario.AnimalDeficit(worst)+1e-6,
                    "actual removal no worse than approved bound (round "+round+")");
                checkedCases++;
            }
        }
        Console.WriteLine($"PASS {checkedCases} randomized vanilla-choice removals bounded by the approved debit (model only)");
    }
}
