using System;
namespace Pengin.AnimalManagement
{
    /// <summary>
    /// Require improvement under BOTH observed seasonal growth and a no-grazing
    /// bound. Weather forecasts may be wrong, but neither may authorize a
    /// material conversion that harms human food or fails to feed animals.
    /// </summary>
    public static class WeatherFeedSafety
    {
        const double Epsilon=1e-6;
        public static bool CanImprove(FeedScenario observed,FeedScenario noGrazing,
            FeedCandidate output,int items,MaterialDebit debit,int days)
        {
            if(observed==null||noGrazing==null||!noGrazing.Reliable||output==null||debit==null||
                debit.Blocked!=null||items<=0||days<1||days>30||observed.Count!=noGrazing.Count)return false;
            return Improves(observed,output,items,debit,days)&&Improves(noGrazing,output,items,debit,days);
        }
        static bool Improves(FeedScenario scenario,FeedCandidate output,int items,MaterialDebit debit,int days)
        {
            var before=scenario.Run(days,0,0);
            var after=scenario.Convert(days,output,items,debit);
            // A global improvement can still deprive an individual species with
            // a restricted diet. Demand allocation must be non-worsening for each
            // modelled consumer, not only for aggregate animals and humans.
            for(int i=0;i<scenario.Count;i++)
                if(after.Unmet[i]>before.Unmet[i]+Epsilon)return false;
            return scenario.AnimalDeficit(after)<scenario.AnimalDeficit(before)-Epsilon;
        }
        public static bool DelaySafe(FeedScenario observed,FeedScenario noGrazing,
            FeedCandidate output,MaterialDebit debit,int days)
        {
            if(observed==null||noGrazing==null||!noGrazing.Reliable||debit==null||debit.Blocked!=null)return false;
            return NotWorse(observed,output,debit,days)&&NotWorse(noGrazing,output,debit,days);
        }
        static bool NotWorse(FeedScenario scenario,FeedCandidate output,MaterialDebit debit,int days)
        {
            var before=scenario.Run(days,0,0);
            var withoutProduct=scenario.Convert(days,output,0,debit);
            for(int i=0;i<scenario.Count;i++)
                if(withoutProduct.Unmet[i]>before.Unmet[i]+Epsilon)return false;
            return true;
        }
    }
}
