using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Intersect.Framework.Core.Tutorials;
using Intersect.Network.Packets.Tutorials;
using Newtonsoft.Json;
using Intersect.Framework.Core.GameObjects.Maps;
using EventDescriptor = Intersect.Framework.Core.GameObjects.Events.EventDescriptor;

internal static class Program
{
 [STAThread] private static void Main()
 {
  typeof(Intersect.Options).GetMethod("EnsureCreated",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
  var assembly=typeof(Intersect.Editor.Forms.Editors.FrmDeathConfiguration).Assembly;
  var adapterType=assembly.GetType("Intersect.Editor.Forms.Tutorials.TutorialStepProperties",true)!;
  var formType=assembly.GetType("Intersect.Editor.Forms.Tutorials.FrmTutorial",true)!;
  var original=CultureInfo.CurrentUICulture;
  try
  {
   foreach(var (language,targetCaption,minimapCaption) in new[]{("en","Highlight target","Minimap"),("fr","Cible de surbrillance","Mini-carte"),("es","Objetivo del resaltado","Minimapa")})
   {
    CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(language);
    var config=TutorialConfiguration.CreateDefault();var step=config.Steps[0];
    var adapter=(ICustomTypeDescriptor)Activator.CreateInstance(adapterType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[step],null)!;
    var properties=adapter.GetProperties();var target=properties[nameof(TutorialStep.HighlightTarget)]!;
    if(target.DisplayName!=targetCaption)throw new Exception("Untranslated target property: "+language);
    if((string)target.Converter.ConvertTo(TutorialHighlightTarget.Minimap,typeof(string))! !=minimapCaption)throw new Exception("Untranslated target choice: "+language);
    foreach(var property in new[]{target,properties[nameof(TutorialStep.HighlightStyle)]!})
     foreach(var value in Enum.GetValues(property.PropertyType))
     {
      var caption=property.Converter.ConvertTo(value,typeof(string))!;
      if(!Equals(property.Converter.ConvertFrom(caption),value))throw new Exception("Localized enum lost its identifier");
     }
    target.SetValue(adapter,TutorialHighlightTarget.Hotbar);
    if(step.HighlightTarget!=TutorialHighlightTarget.Hotbar)throw new Exception("Property edit did not reach actual tutorial step");
    var loaded=JsonConvert.DeserializeObject<TutorialConfiguration>(JsonConvert.SerializeObject(config))!;
    if(!loaded.IsValid||loaded.Steps[0].HighlightTarget!=TutorialHighlightTarget.Hotbar)throw new Exception("Settings did not survive save/reopen");
    var map = new MapDescriptor(Guid.NewGuid());
    var evt = new EventDescriptor(Guid.NewGuid(), map.Id, 4, 5) { Name="Tutor" };
    var duplicate = new EventDescriptor(Guid.NewGuid(), map.Id, 6, 5) { Name="Tutor" };
    map.LocalEvents[evt.Id]=evt; map.LocalEvents[duplicate.Id]=duplicate; MapDescriptor.Lookup.Set(map.Id,map); step.MapId=map.Id;
    using var form=(Form)Activator.CreateInstance(formType,BindingFlags.Instance|BindingFlags.NonPublic,null,[new TutorialEditorPacket{Json=JsonConvert.SerializeObject(config),Revision="test"}],null)!;
    var grid=(PropertyGrid)formType.GetField("_properties",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
    if(grid.SelectedObject is not ICustomTypeDescriptor)throw new Exception("Editor did not bind localized property adapter");
    object Field(string name)=>formType.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
    var targets=(ComboBox)Field("_highlightTarget");var x=(NumericUpDown)Field("_worldX");var y=(NumericUpDown)Field("_worldY");
    var actual=(TutorialStep)Field("_selected");
    targets.SelectedItem=TutorialHighlightTarget.WorldTile;
    if(actual.HighlightTarget!=TutorialHighlightTarget.WorldTile||!x.Enabled||!y.Enabled)throw new Exception("World picker did not edit the actual step");
    var tileCaption=targets.GetItemText(TutorialHighlightTarget.WorldTile);
    if(tileCaption!=(language=="fr"?"Destination sur la carte":language=="es"?"Destino en el mapa":"World tile"))throw new Exception("Untranslated visual target picker");
    x.Value=8;y.Value=9;
    if(actual.TargetX!=8||actual.TargetY!=9)throw new Exception("Coordinate controls lost edits");
    if(x.Maximum!=Math.Min(255,Intersect.Options.Instance.Map.MapWidth-1))throw new Exception("Editor permits tiles outside map");
    targets.SelectedItem=TutorialHighlightTarget.WorldNpc;
    if(!((ComboBox)Field("_worldNpc")).Enabled||x.Enabled)throw new Exception("Wrong controls enabled for NPC target");
    var preview=(Control)Field("_preview");preview.Size=new System.Drawing.Size(800,465);preview.CreateControl();
    using(var bitmap=new System.Drawing.Bitmap(800,465))preview.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,800,465));
    var objectives=(ComboBox)Field("_completionObjective");var events=(ComboBox)Field("_worldEvent");
    if(!events.Enabled||events.Items.Count!=3)throw new Exception("Cannot choose a loaded map event before creating an interaction lesson");
    if(events.GetItemText(events.Items[1])==events.GetItemText(events.Items[2]))throw new Exception("Same-name events are ambiguous");
    objectives.SelectedItem=TutorialObjective.InteractEvent; targets.SelectedItem=TutorialHighlightTarget.WorldEvent;events.SelectedIndex=1;
    if(actual.Objective!=TutorialObjective.InteractEvent||actual.TargetEventId==Guid.Empty)throw new Exception("Event selection not stored");
    if(objectives.GetItemText(TutorialObjective.InteractEvent)=="InteractEvent"||targets.GetItemText(TutorialHighlightTarget.WorldEvent)=="WorldEvent")throw new Exception("Raw interaction enum visible");
    if(properties[nameof(TutorialStep.TargetEventId)]!.DisplayName==nameof(TutorialStep.TargetEventId))throw new Exception("Untranslated event property");
    var roundtrip=JsonConvert.DeserializeObject<TutorialStep>(JsonConvert.SerializeObject(actual))!;
    if(roundtrip.TargetEventId!=actual.TargetEventId)throw new Exception("Event target lost after serialization");
    preview.Size=new System.Drawing.Size(800,465);
    preview.GetType().GetMethod("OnMouseClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(preview,[new MouseEventArgs(MouseButtons.Left,1,573,170,0)]);
    if(actual.HighlightTarget!=TutorialHighlightTarget.WorldEvent)throw new Exception("Preview click loses event target");
    using(var bitmap=new System.Drawing.Bitmap(800,465))preview.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,800,465));
    ((ComboBox)Field("_worldMap")).SelectedIndex=0;
    if(actual.TargetEventId!=Guid.Empty)throw new Exception("Changing maps retains old event");
    MapDescriptor.Lookup.DeleteAt(map.Id);
    Console.WriteLine("PASS tutorial editor: translated choices, map event identity, preview clicks and persistence ("+language+")");
   }
  }
  finally{CultureInfo.CurrentUICulture=original;}
  foreach(var name in new[]{"guide_npc_v1.png","guide_ground_v1.png"})
  {
   using var image=new System.Drawing.Bitmap(Path.Combine("resources","gui","royal-guide",name));
   // Generated soft edges can retain one alpha quantum (1/255); reject a visible backing.
   if(image.GetPixel(0,0).A>1||image.GetPixel(image.Width-1,image.Height-1).A>1)throw new Exception("Marker background is not transparent");
   if(name.Contains("ground")&&image.GetPixel(image.Width/2,image.Height/2).A!=0)throw new Exception("Ground marker hides terrain");
   var visible=0;var red=0;
   for(var row=0;row<image.Height;row+=2)for(var column=0;column<image.Width;column+=2)
   {var pixel=image.GetPixel(column,row);if(pixel.A>100){visible++;if(pixel.R>pixel.G*2&&pixel.R>pixel.B*2)red++;}}
   if(visible<100)throw new Exception("Empty marker image");
   Console.WriteLine($"PASS marker alpha: {name} {image.Width}x{image.Height}; visible samples {visible}; red samples {red}");
  }
 }
}
