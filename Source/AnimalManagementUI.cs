using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Pengin.AnimalManagement
{
    static class ForecastText
    {
        static string Day(int day){return day==0?"AM_NoShortage".Translate().ToString():"AM_ShortageDay".Translate(day).ToString();}
        // A definitive warning needs a complete scan: omitted stacks could cover the deficit.
        public static bool Warns(FeedForecast f)
        {
            ForecastSnapshot s=f.Snapshot;
            return !f.Failed&&!s.Partial&&s.Full!=null&&s.Full.FirstAnimalShortageDay>0&&
                s.Full.FirstAnimalShortageDay<=AnimalManagementMod.Settings.WarnDays&&s.SettingsSignature==AnimalManagementMod.Settings.Signature;
        }
        public static void Draw(Listing_Standard list,FeedForecast f)
        {
            var settings=AnimalManagementMod.Settings;
            if(list.ButtonText("AM_Refresh".Translate()))f.Recalculate();
            ForecastSnapshot s=f.Snapshot;
            if(f.Failed)list.Label("AM_Failed".Translate());
            if(s.SettingsSignature!=settings.Signature)list.Label("AM_Stale".Translate());
            list.Label("AM_Animals".Translate(s.AnimalCount));
            list.Label("AM_Demand".Translate(s.DailyDemand.ToString("0.00")));
            list.Label("AM_HumanDemand".Translate(s.HumanDemand.ToString("0.00")));
            list.Label("AM_Stock".Translate(s.StockNutrition.ToString("0.0"),s.FeedStacks));
            list.Label("AM_Standing".Translate(s.StandingNutrition.ToString("0.0")));
            if(s.StockOnly!=null)
            {
                list.Label("AM_CoveredDays".Translate(s.StockOnly.CoveredWholeDays,s.StockOnly.FirstShortageDay==0?"+":""));
                list.Label("AM_StockShortage".Translate(Day(s.StockOnly.FirstShortageDay)));
                list.Label("AM_AnimalStockShortage".Translate(Day(s.StockOnly.FirstAnimalShortageDay)));
            }
            if(s.Full!=null)
            {
                int day=s.Full.FirstAnimalShortageDay;
                list.Label("AM_AnimalShortage".Translate(Day(day)));
                if(day>0&&day<=settings.WarnDays)
                    list.Label(s.Partial?"AM_WarningPartial".Translate():"AM_Warning".Translate());
            }
            if(s.Partial)list.Label("AM_Partial".Translate());
            list.Label("AM_Assumptions".Translate());
            list.Label("AM_NutritionAssumptions".Translate());
            foreach(string line in s.Lines)list.Label(line);
            list.Label("AM_Invalidation".Translate());
            list.Label("AM_Scan".Translate(s.Tick,f.ScanMilliseconds.ToString("0.00"),s.AccessChecks));
        }
    }

    // Non-modal forecast window: the map stays visible and movable.
    public sealed class Window_FeedForecast : Window
    {
        Vector2 scroll;float height=1200;
        public Window_FeedForecast()
        {
            doCloseX=true;draggable=true;resizeable=true;
            absorbInputAroundWindow=false;closeOnClickedOutside=false;preventCameraMotion=false;forcePause=false;
        }
        public override Vector2 InitialSize { get { return new Vector2(720f,600f); } }
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font=GameFont.Medium;
            Widgets.Label(new Rect(inRect.x,inRect.y,inRect.width-40,34),"Animal Management");
            Text.Font=GameFont.Small;
            Rect body=new Rect(inRect.x,inRect.y+38,inRect.width,inRect.height-38);
            Map map=Find.CurrentMap;
            FeedForecast f=map==null?null:map.GetComponent<FeedForecast>();
            if(f==null){Widgets.Label(body,"AM_NoMap".Translate());return;}
            Rect content=new Rect(0,0,body.width-20,Math.Max(body.height,height));
            Widgets.BeginScrollView(body,ref scroll,content);
            var list=new Listing_Standard();list.Begin(content);
            ForecastText.Draw(list,f);
            height=list.CurHeight+20;list.End();Widgets.EndScrollView();
        }
    }

    // Standard right-side alert for a complete forecast that predicts an animal feed shortage.
    public sealed class Alert_FeedShortage : Alert
    {
        readonly List<string> lines=new List<string>();
        int earliest;
        public Alert_FeedShortage(){defaultPriority=AlertPriority.High;}
        public override AlertReport GetReport()
        {
            lines.Clear();earliest=0;
            if(AnimalManagementMod.Settings==null||!AnimalManagementMod.Settings.ShowAlert)return AlertReport.Inactive;
            foreach(Map map in Find.Maps)
            {
                FeedForecast f=map.GetComponent<FeedForecast>();
                if(f==null||!ForecastText.Warns(f))continue;
                int day=f.Snapshot.Full.FirstAnimalShortageDay;
                if(earliest==0||day<earliest)earliest=day;
                lines.Add("- "+map.Parent.LabelCap+": "+"AM_ShortageDay".Translate(day));
            }
            return lines.Count>0?AlertReport.Active:AlertReport.Inactive;
        }
        public override string GetLabel(){return "AM_AlertLabel".Translate(earliest);}
        public override TaggedString GetExplanation()
        {
            return "AM_AlertExplanation".Translate()+"\n\n"+string.Join("\n",lines)+"\n\n"+"AM_AlertClick".Translate();
        }
        protected override void OnClick()
        {
            if(!Find.WindowStack.IsOpen<Window_FeedForecast>())Find.WindowStack.Add(new Window_FeedForecast());
        }
    }

    public sealed class AnimalManagementMod : Mod
    {
        public static AnimalSettings Settings;
        Vector2 scroll;float contentHeight=1600;
        public AnimalManagementMod(ModContentPack pack):base(pack){Settings=GetSettings<AnimalSettings>();}
        public override string SettingsCategory(){return "Animal Management";}
        public override void DoSettingsWindowContents(Rect rect)
        {
            Rect content=new Rect(0,0,Math.Max(100,rect.width-20),Math.Max(rect.height,contentHeight));
            Widgets.BeginScrollView(rect,ref scroll,content);
            var list=new Listing_Standard();list.Begin(content);
            list.Label("AM_Description".Translate());
            list.Label("AM_WarnDays".Translate(Settings.WarnDays));Settings.WarnDays=(int)list.Slider(Settings.WarnDays,1,15);
            list.CheckboxLabeled("AM_Forbidden".Translate(),ref Settings.IncludeForbidden);
            list.CheckboxLabeled("AM_ShowAlert".Translate(),ref Settings.ShowAlert);
            list.CheckboxLabeled("AM_PopulationScenario".Translate(),ref Settings.PopulationScenario);
            if(Settings.PopulationScenario)
            {
                list.Label("AM_Births".Translate(Settings.Births));Settings.Births=(int)list.Slider(Settings.Births,0,100);
                list.Label("AM_Deaths".Translate(Settings.Deaths));Settings.Deaths=(int)list.Slider(Settings.Deaths,0,100);
            }
            Map current=Find.CurrentMap;
            FeedForecast f=current==null?null:current.GetComponent<FeedForecast>();
            if(f!=null)
            {
                if(list.ButtonText("AM_OpenWindow".Translate()))
                {
                    list.End();Widgets.EndScrollView();
                    Find.WindowStack.TryRemove(typeof(Dialog_ModSettings));
                    if(!Find.WindowStack.IsOpen<Window_FeedForecast>())Find.WindowStack.Add(new Window_FeedForecast());
                    return;
                }
                ForecastText.Draw(list,f);
            }
            else list.Label("AM_NoMap".Translate());
            contentHeight=list.CurHeight+20;list.End();Widgets.EndScrollView();
        }
    }
}
