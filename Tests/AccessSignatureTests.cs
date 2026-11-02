using System;
using Pengin.AnimalManagement;
static class AccessSignatureTests
{
    public static void Run()
    {
        var access=new bool[400];access[0]=true;access[65]=true;access[399]=true;
        var one=AccessSignature.Create(access,3);
        var copy=AccessSignature.Create((bool[])access.Clone(),3);
        if(!one.Equals(copy)||one.GetHashCode()!=copy.GetHashCode())throw new Exception("same access masks and expiry pool together");
        access[255]=true;
        if(one.Equals(AccessSignature.Create(access,3)))throw new Exception("high consumer index distinguishes pools");
        access[255]=false;
        if(one.Equals(AccessSignature.Create(access,4)))throw new Exception("expiry distinguishes pools");
        bool rejected=false;
        try{AccessSignature.Create(new bool[449],0);}catch(ArgumentException){rejected=true;}
        if(!rejected)throw new Exception("mask cannot silently truncate consumers");
        Console.WriteLine("PASS 4 access mask scenarios");
    }
}
