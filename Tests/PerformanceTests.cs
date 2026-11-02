using System;
using System.Collections.Generic;
using System.Linq;
using Pengin.AnimalManagement;
static class PerformanceTests
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static (int groups,long bytes,double ms) Measure(bool compact,int count,int consumers)
    {
        var masks=new bool[80][];
        for(int m=0;m<masks.Length;m++)
        {
            masks[m]=new bool[consumers];
            for(int i=0;i<consumers;i++)masks[m][i]=((m*31+i*13)%19)<7;
        }
        GC.Collect();var timer=System.Diagnostics.Stopwatch.StartNew();
        long start=GC.GetAllocatedBytesForCurrentThread();int groups;
        if(compact)
        {
            var grouped=new Dictionary<AccessSignature,int>();
            for(int n=0;n<count;n++)
            {
                var key=AccessSignature.Create(masks[n%80],n%4);
                int value;grouped.TryGetValue(key,out value);grouped[key]=value+1;
            }
            groups=grouped.Count;
        }
        else
        {
            var grouped=new Dictionary<string,int>();
            for(int n=0;n<count;n++)
            {
                var key=new string(masks[n%80].Select(v=>v?'1':'0').ToArray())+":"+(n%4);
                int value;grouped.TryGetValue(key,out value);grouped[key]=value+1;
            }
            groups=grouped.Count;
        }
        long bytes=GC.GetAllocatedBytesForCurrentThread()-start;timer.Stop();
        return (groups,bytes,timer.Elapsed.TotalMilliseconds);
    }
    public static void Run()
    {
        // Synthetic access-key grouping only: NOT a map scan, GC/TPS gameplay benchmark.
        foreach(int consumers in new[]{100,400})
        foreach(int count in new[]{1000,12000})
        {
            var legacy=Measure(false,count,consumers);
            var compact=Measure(true,count,consumers);
            Check(legacy.groups==compact.groups,"compact and legacy grouping parity");
            Check(compact.bytes<legacy.bytes,"access signatures lower allocation");
            Console.WriteLine($"Access-key benchmark: consumers={consumers} stacks={count}, groups={compact.groups}, legacy {legacy.bytes} B / {legacy.ms:F2} ms, compact {compact.bytes} B / {compact.ms:F2} ms (synthetic, not TPS)");
        }
    }
}
