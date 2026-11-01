using System;
using System.IO;
using System.Reflection;
using Pengin.AnimalManagement;
using Verse;
class Program
{
    static void Main()
    {
        _=ParseHelper.FromString<string>("fixture");
        // Real default PrefsData for DeepProfiler; no engine or playable-map initialization.
        typeof(Prefs).GetField("data",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new PrefsData());
        string path=Path.Combine(Path.GetTempPath(),"am-"+Guid.NewGuid()+".xml");
        try
        {
            var settings=new AnimalSettings {WarnDays=9,IncludeForbidden=true,PopulationScenario=true,Births=4,Deaths=2};
            Scribe.saver.InitSaving(path,"fixture");settings.ExposeData();Scribe.saver.FinalizeSaving();
            settings=new AnimalSettings();
            Scribe.loader.InitLoading(path);settings.ExposeData();Scribe.loader.FinalizeLoading();
            if(settings.WarnDays!=9||!settings.IncludeForbidden||!settings.PopulationScenario||settings.Births!=4||settings.Deaths!=2)
                throw new Exception("Animal settings Scribe round trip failed");
            Console.WriteLine("PASS real-game Scribe animal settings round trip (not map save/load)");
        }
        finally {if(File.Exists(path))File.Delete(path);}
    }
}
