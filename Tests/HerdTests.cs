using System;
using System.Collections.Generic;
using Pengin.AnimalManagement;
static class HerdTests
{
    public static void Run()
    {
        var scenario=new FeedScenario(new[]{new Demand{Daily=1},new Demand{Daily=1}},new[]{new FeedPool{Nutrition=21,Access=new[]{true,true}}},d=>new List<FeedPool>(),true);
        var r=HerdCapacityPlanner.Calculate(scenario,new[]{true,false},7,10,0,0);
        if(!r.Feasible||r.Sustainable!=2)throw new Exception("other species competes for shared feed");
        var limited=new FeedScenario(new[]{new Demand{Daily=1},new Demand{Daily=1}},new[]{new FeedPool{Nutrition=100,Access=new[]{true,false}}},d=>new List<FeedPool>(),true);
        if(!HerdCapacityPlanner.Calculate(limited,new[]{true,false},7,10,0,0).Feasible)throw new Exception("unrelated species deficit must not invalidate capacity");
        if(HerdCapacityPlanner.Calculate(scenario,new[]{true,false},30,10,0,0).Sustainable!=0)throw new Exception("winter reserve limits capacity to zero without worsening other shortages");
        if(HerdCapacityPlanner.Calculate(scenario,new[]{true,false},7,10,5,0).Sustainable>=r.Sustainable)throw new Exception("birth scenario reduces headroom");
        // An unrelated hungry species must not prevent evaluating a fully-fed species.
        var independent=new FeedScenario(new[]{new Demand{Daily=1},new Demand{Daily=1}},
            new[]{new FeedPool{Nutrition=21,Access=new[]{false,true}}},d=>new List<FeedPool>(),true);
        var independentCap=HerdCapacityPlanner.Calculate(independent,new[]{false,true},7,10,0,0);
        if(!independentCap.Feasible||independentCap.Sustainable!=3)
            throw new Exception("unrelated hunger must not make fed species unsolved");
        // A capacity increase must never steal shared stock from a different hungry species.
        var shared=new FeedScenario(new[]{new Demand{Daily=2},new Demand{Daily=1}},
            new[]{new FeedPool{Nutrition=7,Access=new[]{true,true}}},d=>new List<FeedPool>(),true);
        var shareCap=HerdCapacityPlanner.Calculate(shared,new[]{false,true},7,10,0,0);
        if(!shareCap.Feasible||shareCap.Sustainable>1)
            throw new Exception("other species shortage cannot be made worse");
        Console.WriteLine("PASS 6 herd capacity scenarios");
    }
}
