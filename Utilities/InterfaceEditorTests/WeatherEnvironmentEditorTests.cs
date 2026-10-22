using System.ComponentModel;
using System.Reflection;
using Intersect.Editor.Forms.DockingElements;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps;

internal static class WeatherEnvironmentEditorTests
{
    internal static void Run()
    {
        var editor=typeof(FrmMapProperties).Assembly;
        var sectionType=editor.GetType("Intersect.Editor.Maps.MapPropertySection",true)!;
        var capture=sectionType.GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)!;
        var apply=sectionType.GetMethod("Apply",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var source=new MapDescriptor(Guid.NewGuid()){Name="Source",Fog="ordinary-fog",Brightness=91,WeatherIntensity=77};
        var animation=Guid.NewGuid();typeof(MapDescriptor).GetProperty(nameof(MapDescriptor.WeatherAnimationId))!.SetValue(source,animation);
        source.WeatherEffects.RainFog="rain-fog";source.WeatherEffects.OverrideRainFog=true;
        var snapshot=capture.Invoke(null,[source,"weather"])!;
        source.WeatherEffects.RainFog="changed-after-copy";
        var target=new MapDescriptor(Guid.NewGuid()){Name="Destination",Fog="keep-fog",Brightness=55,WeatherIntensity=10};
        var id=target.Id;
        apply.Invoke(snapshot,[target]);
        if(target.Id!=id || target.Name!="Destination" || target.Fog!="keep-fog" || target.Brightness!=55 || target.WeatherIntensity!=77 || target.WeatherAnimationId!=animation || target.WeatherEffects.RainFog!="rain-fog")
            throw new Exception($"Section paste mismatch: ID={target.Id==id}, name={target.Name}, fog={target.Fog}, brightness={target.Brightness}, intensity={target.WeatherIntensity}, animation={target.WeatherAnimationId==animation}, rainFog={target.WeatherEffects.RainFog}");
        target.WeatherEffects.RainFog="target-only";var second=new MapDescriptor(Guid.NewGuid());apply.Invoke(snapshot,[second]);
        if(second.WeatherEffects.RainFog!="rain-fog" || ReferenceEquals(second.WeatherEffects,target.WeatherEffects))throw new Exception("Pasted weather sections must never share mutable configuration.");
        source.FogTransparency=177;source.FogXSpeed=-3;
        var fogCopy=capture.Invoke(null,[source,"fog"])!;apply.Invoke(fogCopy,[target]);
        if(target.FogTransparency!=177 || target.FogXSpeed!=-3 || target.WeatherIntensity!=77)
            throw new Exception("Fog section must include opacity and speed without changing weather.");
        using var properties=new FrmMapProperties();
        var grid=(PropertyGrid)typeof(FrmMapProperties).GetField("gridMapProperties",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(properties)!;
        if(grid.ContextMenuStrip?.Items.Count!=2)throw new Exception("Map Properties needs section Copy/Paste actions.");
        using var events=new FrmEvent(new MapDescriptor(Guid.NewGuid()));
        var commandType=editor.GetType("Intersect.Editor.Forms.Editors.Events.Event_Commands.EventCommandPlayerConvenience",true)!;
        using var control=(Control)Activator.CreateInstance(commandType,BindingFlags.Instance|BindingFlags.NonPublic,null,[new ForceRainCommand(),events],null)!;
        var duration=control.Controls.OfType<NumericUpDown>().Single();
        if(duration.Value!=900 || duration.Maximum!=604800 || duration.Right>control.Width)throw new Exception("Rain command needs a visible configurable duration with a fifteen-minute default.");
        var commandTree=(TreeView)typeof(FrmEvent).GetField("lstCommands",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(events)!;
        bool Contains(TreeNode node)=>node.Tag is int value && value==(int)EventCommandType.ForceRain || node.Nodes.Cast<TreeNode>().Any(Contains);
        if(!commandTree.Nodes.Cast<TreeNode>().Any(Contains))throw new Exception("Rain command must be selectable in the event command tree.");
        Console.WriteLine("PASS weather section copy/paste: category isolation, detached snapshots, stable IDs and event rain duration controls");
    }
}
