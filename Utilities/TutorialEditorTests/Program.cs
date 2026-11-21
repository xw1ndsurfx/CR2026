using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Intersect.Framework.Core.Tutorials;
using Intersect.Network.Packets.Tutorials;
using Newtonsoft.Json;

internal static class Program
{
 [STAThread] private static void Main()
 {
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
    using var form=(Form)Activator.CreateInstance(formType,BindingFlags.Instance|BindingFlags.NonPublic,null,[new TutorialEditorPacket{Json=JsonConvert.SerializeObject(config),Revision="test"}],null)!;
    var grid=(PropertyGrid)formType.GetField("_properties",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
    if(grid.SelectedObject is not ICustomTypeDescriptor)throw new Exception("Editor did not bind localized property adapter");
    Console.WriteLine("PASS tutorial editor: translated property/choices, editable values and persistence ("+language+")");
   }
  }
  finally{CultureInfo.CurrentUICulture=original;}
 }
}
