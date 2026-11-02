using System;
using Pengin.AnimalManagement;
static class OwnershipTests
{
    public static void Run()
    {
        if(!ManagedBillPolicy.CanCreate(ActionMode.Confirm,true,true,false,0,2,60000,0,60000))throw new Exception("confirmed safe creation");
        if(ManagedBillPolicy.CanCreate(ActionMode.Auto,false,true,false,0,2,60000,0,60000))throw new Exception("target permission required");
        if(ManagedBillPolicy.CanCreate(ActionMode.Auto,true,false,false,0,2,60000,0,60000))throw new Exception("partial forecast blocked");
        if(ManagedBillPolicy.CanCreate(ActionMode.Auto,true,true,true,0,2,60000,0,60000))throw new Exception("manual/duplicate bill protected");
        if(ManagedBillPolicy.CanCreate(ActionMode.Auto,true,true,false,2,2,60000,0,60000))throw new Exception("map operation cap");
        if(ManagedBillPolicy.CanCreate(ActionMode.Auto,true,true,false,0,2,59999,0,60000))throw new Exception("cooldown");
        if(ManagedBillPolicy.Owns("before","edited",false))throw new Exception("manual edits revoke ownership");
        if(ManagedBillPolicy.Owns("same","same",true))throw new Exception("suspended bill revokes ownership");
        Console.WriteLine("PASS 8 production ownership policy assertions");
    }
}
