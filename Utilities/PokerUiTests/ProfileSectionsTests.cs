using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.General;
using Intersect.Network.Packets.Server;
using Base=Intersect.Client.Framework.Gwen.Control.Base;
internal static class ProfileSectionsTests
{
    [ModuleInitializer] internal static void Run()
    {
        try
        {
            _=new TestContentManager();using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(1280,768)};
            const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
            var assembly=typeof(GameInterface).Assembly;var type=assembly.GetType("Intersect.Client.Interface.Game.PlayerProfileWindow",true)!;
            var window=(Base)Activator.CreateInstance(type,[canvas])!;
            var profile=new PlayerProfilePacket{Found=true,PlayerId=Guid.NewGuid(),Name="Friend",Level=10,Stats=[1,2,3,4,5],Professions=Enumerable.Range(1,8).Select(i=>new PlayerProfessionProfilePacket{ProfessionId=Guid.NewGuid(),Name="Profession "+i,Level=2,MaximumLevel=50,LevelExperience=25,LevelExperienceRequired=100}).ToArray(),Equipment=Enumerable.Range(0,9).Select(i=>new PlayerEquipmentProfilePacket{SlotIndex=i,SlotName="Slot "+i}).ToArray()};
            void Apply()=>type.GetMethod("Apply")!.Invoke(window,[profile]);
            Base Find(string name)=>Descendants(window).Single(c=>c.Name==name);
            void Click(string name)=>typeof(Button).GetMethod("OnKeyReturn",hidden)!.Invoke(Find(name),[false]);
            void Check(bool ok,string message){if(!ok)throw new Exception(message);}
            Apply();Check(!Descendants(window).OfType<ScrollControl>().Any(),"Profile must not have a long shared scrollbar");
            Check(!Descendants(window).Any(c=>c.Name=="CHARACTERTitle"),"Header details must not be repeated");
            Click("ProfileTab2");Check(Descendants(window).Count(c=>c.Name=="ExperienceBar")==3,"Profession page must keep three complete experience bars visible");
            Click("ProfileNextPage");Check(Descendants(window).OfType<Label>().Any(l=>l.Text?.StartsWith("Profession 4")==true),"Profession next page must show different content");
            Click("ProfileTab1");Check(Descendants(window).Count(c=>c.Name?.StartsWith("ProfileEquipmentCard")==true)==4,"Equipment must use a compact four-item page");
            Click("ProfileNextPage");Click("ProfileNextPage");Check(Descendants(window).Count(c=>c.Name?.StartsWith("ProfileEquipmentCard")==true)==1,"Final equipment page must retain the last item");
            Click("ProfileTab0");Check(Find("ProfileNextPage").IsHidden,"Stats must not retain item pagination");
            profile.MasteriesAvailable = true; profile.Masteries = Enumerable.Range(1, 5).Select(i => new PlayerMasteryProfilePacket {
                Key = "mastery-" + i, Name = "Mastery " + i, Level = 2, MaximumLevel = 30, LevelExperience = 50, RequiredExperience = 400, AvailablePoints = 1 }).ToArray();
            Apply(); Click("ProfileTab3");
            Check(Descendants(window).Count(c => c.Name == "MasteryExperienceBar") == 2, "Public mastery pages must show two complete XP bars");
            Click("ProfileNextPage"); Click("ProfileNextPage");
            Check(Descendants(window).Count(c => c.Name == "MasteryExperienceBar") == 1, "Final mastery page lost its last category");
            profile.MasteriesAvailable = false; Apply();
            Check(Descendants(window).Any(c => c.Name == "MasteriesUnavailable"), "Unavailable public masteries displayed invented progress");
            profile.IsSelf=true;Apply();Check(!Find("VisitProfileHouse").IsDisabled,"Self profile must retain house access");
            var petsType=assembly.GetType("Intersect.Client.Interface.Game.PetsWindow",true)!;
            var pets=(Base)Activator.CreateInstance(petsType,[canvas,false])!;
            var accessories=Descendants(pets).Single(c=>c.Name=="PetAccessories");var list=Descendants(pets).Single(c=>c.Name=="PetsScroll");
            Check(accessories.X==list.X && accessories.Width==list.Width && accessories.Y>list.Bottom && accessories.Bottom<pets.Height-30,"Pet accessories footer must align and stay inside the window");
            System.Console.WriteLine("PASS profile section buttons, no duplicate header, professions/equipment paging, self/other profiles and aligned Pets footer");
        }
        catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}
    }
    private static IEnumerable<Base> Descendants(Base p)=>p.Children.SelectMany(c=>new[]{c}.Concat(Descendants(c)));
}
