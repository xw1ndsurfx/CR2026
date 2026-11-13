using System.Globalization;
using Intersect.Editor.Content;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms;
internal static class ImportedResourceDeclarationControls
{
    private static string T(string source)=>QualityOfLifeText.Get(source,CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    internal static async Task DeclareSelected(Form owner,string? source,string cache)
    {
        if(source==null || !File.Exists(source))return;
        var answer=MessageBox.Show(owner,T("Has this resource already been imported into the game?"),T("Import tracking"),MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
        if(answer==DialogResult.Cancel)return;
        await Save(owner,[(source,cache)],answer==DialogResult.Yes);
    }
    internal static async Task ConfirmAfterImport(Form owner,IReadOnlyList<(string Source,string Cache)> sources)
    {
        if(sources.Count==0 || owner.IsDisposed || owner.Disposing)return;
        var answer=MessageBox.Show(owner,string.Format(T("Import completed. Mark the {0} imported resource(s) as already imported?"),sources.Count),
            T("Import tracking"),MessageBoxButtons.YesNo,MessageBoxIcon.Question);
        await Save(owner,sources,answer==DialogResult.Yes);
    }
    private static async Task Save(Form owner,IReadOnlyList<(string Source,string Cache)> sources,bool imported)
    {
        var failures=await Task.Run(()=>
        {
            var count=0;
            foreach(var (source,cache) in sources)
                try{ImportedResourceDeclarations.Write(source,cache,imported);}
                catch(Exception error) when(error is IOException or UnauthorizedAccessException){count++;}
            return count;
        });
        if(failures>0 && !owner.IsDisposed)MessageBox.Show(owner,T("Import tracking could not be saved. Check write access to the game folder."),T("Import tracking"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }

    private sealed class BadgeState {internal bool Imported;internal string? Caption;}
    private const string BadgePrefix="__imported_badge__:";
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListViewItem,BadgeState> States=new();
    internal static void RefreshThumbnailBadge(ListViewItem item)
    {
        if(item.ListView is not {} view || view.LargeImageList is not {} images)return;
        var key=item.ImageKey;var original=key.StartsWith(BadgePrefix,StringComparison.Ordinal)?key[BadgePrefix.Length..]:key;
        if(!States.TryGetValue(item,out var state) || !state.Imported){item.ImageKey=original;return;}
        if(!images.Images.ContainsKey(original))return;
        var marked=BadgePrefix+original;
        if(!images.Images.ContainsKey(marked))
        {
            using var bitmap=new Bitmap(images.Images[original]!);
            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
            using(var fill=new SolidBrush(System.Drawing.Color.FromArgb(15,85,43)))
            using(var border=new Pen(System.Drawing.Color.FromArgb(133,240,164),2))
            using(var font=new Font(SystemFonts.MessageBoxFont.FontFamily,8,FontStyle.Bold))
            {
                var badge=new Rectangle(1,1,bitmap.Width-2,23);graphics.FillRectangle(fill,badge);graphics.DrawRectangle(border,badge);
                TextRenderer.DrawText(graphics,T("IMPORTED"),font,badge,System.Drawing.Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            }
            images.Images.Add(marked,bitmap);_=images.Handle; // Copy to the native image list before releasing the bitmap.
        }
        item.ImageKey=marked;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListView,CancellationTokenSource> Pending=new();
    internal static void CancelBadges(ListView view)
    {
        if(Pending.TryGetValue(view,out var pending)){pending.Cancel();pending.Dispose();Pending.Remove(view);}
    }
    internal static void RefreshBadges(ListView view,Func<ListViewItem,string> source,Func<ListViewItem,string> cache)
    {
        CancelBadges(view);
        var pending=new CancellationTokenSource();Pending.Add(view,pending);var token=pending.Token;
        var items=view.Items.Cast<ListViewItem>().Select(item=>(Item:item,Source:source(item),Cache:cache(item))).ToArray();
        var prefix=T("[Imported] ");
        _=Task.Run(()=>
        {
            foreach(var item in items)
            {
                if(token.IsCancellationRequested)return;
                bool marked;
                try{marked=ImportedResourceDeclarations.Read(item.Source,item.Cache,token)==true;}
                catch(OperationCanceledException){return;}
                catch(Exception error) when(error is IOException or UnauthorizedAccessException){continue;}
                if(view.IsDisposed || !view.IsHandleCreated)return;
                try{view.BeginInvoke((Action)(()=>
                {
                    if(token.IsCancellationRequested || view.IsDisposed || item.Item.ListView!=view)return;
                    var state=States.GetValue(item.Item,_=>new BadgeState());
                    state.Caption??=item.Item.Text;
                    item.Item.Text=(marked?prefix:"")+state.Caption;
                    item.Item.ForeColor=marked?System.Drawing.Color.LightGreen:view.ForeColor;
                    state.Imported=marked;
                    RefreshThumbnailBadge(item.Item);
                    if(view.View==View.Details && view.Columns.ContainsKey("ImportDeclarationStatus"))
                    {
                        var index=view.Columns["ImportDeclarationStatus"]!.Index;
                        while(item.Item.SubItems.Count<=index)item.Item.SubItems.Add("");
                        item.Item.UseItemStyleForSubItems=false;
                        var cell=item.Item.SubItems[index];cell.Text=T(marked?"IMPORTED":"Not marked");
                        cell.BackColor=marked?System.Drawing.Color.FromArgb(15,85,43):view.BackColor;
                        cell.ForeColor=marked?System.Drawing.Color.White:System.Drawing.Color.Silver;
                    }
                }));}
                catch(InvalidOperationException){return;}
            }
        },token);
    }
}
