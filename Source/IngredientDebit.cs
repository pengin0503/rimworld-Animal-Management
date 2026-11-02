using System;
using System.Collections.Generic;
using System.Linq;

namespace Pengin.AnimalManagement
{
    // Forecast nutrition held by candidate ingredient stacks inside one stored-feed pool.
    public struct IngredientSource
    {
        public readonly int Pool;
        public readonly double Nutrition;
        public IngredientSource(int pool,double nutrition){Pool=pool;Nutrition=nutrition;}
    }
    // One recipe ingredient: every stack vanilla may pick for it, and an upper bound
    // (forecast nutrition units) of what all batches together can remove from them.
    public sealed class IngredientDemand
    {
        public double MaxRemoval;
        public readonly List<IngredientSource> Sources=new List<IngredientSource>();
    }
    public sealed class MaterialDebit
    {
        public readonly Dictionary<int,double> Debits=new Dictionary<int,double>();
        // Non-null: the removal cannot be bounded safely; production must be refused.
        public string Blocked;
    }
    public static class IngredientDebit
    {
        const double Epsilon=1e-9;
        static bool Valid(double n){return !double.IsNaN(n)&&!double.IsInfinity(n)&&n>=0;}
        // Debits of different ingredients that draw on the same pool are ADDED (#25).
        // An ingredient whose candidate stacks span several pools is refused: vanilla picks
        // stacks by nutrition value and distance, so the split is not fixed, and in this
        // competitive model no pool is uniformly "less valuable" (food shared with animals
        // is not better for humans than human-only food). Neither a per-pool worst case nor
        // a debit of the full bound from every pool is used; both mis-state the real loss.
        public static MaterialDebit Build(IList<FeedPool> pools,IList<IngredientDemand> ingredients)
        {
            var result=new MaterialDebit();
            if(pools==null||ingredients==null){result.Blocked="AM_IngredientUnbounded";return result;}
            var claimed=new double[pools.Count];
            foreach(var ingredient in ingredients)
            {
                if(ingredient==null||!Valid(ingredient.MaxRemoval)){result.Blocked="AM_IngredientUnbounded";return result;}
                int pool=-1;double available=0;
                foreach(var source in ingredient.Sources)
                {
                    if(source.Pool<0||source.Pool>=pools.Count||!Valid(source.Nutrition)||pools[source.Pool]==null||
                        pools[source.Pool].Pasture||pools[source.Pool].Standing){result.Blocked="AM_IngredientUnbounded";return result;}
                    if(source.Nutrition<=Epsilon)continue;
                    if(pool>=0&&pool!=source.Pool){result.Blocked="AM_IngredientPools";return result;}
                    pool=source.Pool;available+=source.Nutrition;
                }
                if(pool<0||ingredient.MaxRemoval<=Epsilon)continue;
                claimed[pool]+=available;
                if(claimed[pool]>pools[pool].Nutrition*(1+1e-9)+1e-6){result.Blocked="AM_IngredientUnbounded";return result;}
                double take=Math.Min(available,ingredient.MaxRemoval);
                double sum;result.Debits.TryGetValue(pool,out sum);result.Debits[pool]=sum+take;
            }
            return result;
        }
    }
}
