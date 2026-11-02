namespace Pengin.AnimalManagement
{
    public static class ManagedBillPolicy
    {
        public static bool CanCreate(ActionMode mode,bool permitted,bool reliable,bool duplicate,int active,int cap,int now,int last,int cooldown)
        {return mode!=ActionMode.Off&&(mode!=ActionMode.Auto||permitted)&&reliable&&!duplicate&&active<cap&&now>=last&&(long)now-last>=cooldown;}
        public static bool Owns(string expected,string current,bool suspended){return !suspended&&expected==current;}
    }
}
