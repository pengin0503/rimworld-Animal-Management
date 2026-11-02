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
        if(HerdCapacityPlanner.Calculate(limited,new[]{true,false},7,10,0,0).Feasible)throw new Exception("other species diet cannot borrow surplus");
        if(HerdCapacityPlanner.Calculate(scenario,new[]{true,false},30,10,0,0).Feasible)throw new Exception("winter reserve limits capacity");
        if(HerdCapacityPlanner.Calculate(scenario,new[]{true,false},7,10,5,0).Sustainable>=r.Sustainable)throw new Exception("birth scenario reduces headroom");
        Console.WriteLine("PASS 4 herd capacity scenarios");
    }
}
