using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Core;
using Microsoft.Extensions.Logging;

namespace Intersect.Client.Interface.Game.Admin;
public partial class AdminWindow
{
    private void AddResourceRefreshControls()
    {
        var button=new Button(this,"ReloadLocalResources") {Dock=Pos.Top,Height=32,Font=_defaultFont,FontSize=12,
            Text=ClientLanguages.Text("Refresh local resources"),
            TooltipText=ClientLanguages.Text("Find new PNG images, WAV sounds and OGG music in this client's resources folder.")};
        button.Clicked+=(_,_)=>
        {
            button.IsDisabled=true;
            try
            {
                var result=GameContentManager.Current.RefreshLocalResources();
                _spriteTexturePicker.InvalidateSelector();_faceTexturePicker.InvalidateSelector();
                button.Text=ClientLanguages.Format("Refresh resources ({0} added, {1} failed)",result.Images+result.Sounds+result.Music,result.Failures);
                button.TooltipText=ClientLanguages.Format("Added: {0} images, {1} sounds, {2} music tracks. Failed: {3}.",result.Images,result.Sounds,result.Music,result.Failures);
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
            {
                ApplicationContext.Context.Value?.Logger.LogWarning(error,"Local resource refresh failed");
                button.Text=ClientLanguages.Text("Resource refresh failed. Check the client log.");
            }
            finally{button.IsDisabled=false;}
        };
    }
}
