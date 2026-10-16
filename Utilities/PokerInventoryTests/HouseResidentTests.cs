using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.Housing;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Server.Entities;
internal static class HouseResidentTests
{
    [ModuleInitializer] internal static void Run()
    {
        var oldOptions=Options.Instance;
        try { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,new Options()); Verify(); }
        catch(Exception e) { System.Console.Error.WriteLine(e);Environment.Exit(1); }
        finally { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,oldOptions); }
    }
    private static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
    private static void Verify()
    {
        var used=new HashSet<(int,int)>{(4,4),(5,4),(6,4),(5,5)};
        var first=HousePetPlacement.Find(5,5,12,12,(x,y)=>!used.Contains((x,y))&&y>=5);used.Add(first);
        var second=HousePetPlacement.Find(5,5,12,12,(x,y)=>!used.Contains((x,y))&&y>=5);
        Check(first!=second&&first.Y>=5&&second.Y>=5,"House residents avoid blocked/occupied tiles and spread into distinct positions");
        var edge=HousePetPlacement.Find(0,0,2,2,(x,y)=>x==1&&y==1);
        Check(edge==(1,1),"Resident placement clips correctly at map borders");
        try { HousePetPlacement.Find(1,1,2,2,(_,_)=>false);throw new Exception("Full house accepted a pet"); } catch(InvalidOperationException) { }
        var runtime=typeof(Player).Assembly.GetType("Intersect.Server.Housing.HousingRuntime",true)!;
        var field=runtime.GetField("_config",BindingFlags.Static|BindingFlags.NonPublic)!;var previous=field.GetValue(null);
        var npc=new NPCDescriptor(Guid.NewGuid()){IsPet=true,Sprite="resident.png"};NPCDescriptor.Lookup[npc.Id]=npc;
        try
        {
            field.SetValue(null,new HousingConfiguration{TemplateMapId=Guid.Empty,EntryX=5,EntryY=5});
            var house=new HouseProfile{Pets=[new(){Id=npc.Id,Name="Existing pet",X=0,Y=0},new(){Id=Guid.NewGuid(),Name="Other pet",X=0,Y=0}]};
            runtime.GetMethod("ApplyInteractionLinks",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[house]);
            Check(house.Pets[0].Sprite=="resident.png"&&house.Pets.All(p=>p.X!=0||p.Y!=0)&&house.Pets.Select(p=>(p.X,p.Y)).Distinct().Count()==2,"Existing corner residents are repaired, retain their names and receive their NPC sprite");
            Check(house.Pets[0].Name=="Existing pet"&&house.Pets[1].Name=="Other pet","Repair preserves existing pets");
        }
        finally{field.SetValue(null,previous);NPCDescriptor.Lookup.DeleteAt(npc.Id);}
        System.Console.WriteLine("PASS house resident collision-free placement, map bounds, full interior and legacy resident sprite/position repair");
    }
}
