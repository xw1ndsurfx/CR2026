using System.Globalization;
using Intersect.Editor.Content;
using Intersect.Editor.Forms;
using Intersect.Framework.Core.QualityOfLife;

internal static class ImportedResourceDeclarationTests
{
    internal static void Run()
    {
        foreach(var text in new[]{"Declare import status...","Has this resource already been imported into the game?",
            "Import tracking","Import completed. Mark the {0} imported resource(s) as already imported?",
            "Import tracking could not be saved. Check write access to the game folder.","Marked as imported.",
            "Not marked as imported.","Not marked; matching game copy: {0}","[Imported] ","IMPORTED","Not marked","Import status"})
        {
            foreach(var language in new[]{"en","fr","es"})
                Check(QualityOfLifeText.Catalog.Entries[text].ContainsKey(language) &&
                    Intersect.Localization.GameTextTokens.Compatible(text,QualityOfLifeText.Get(text,language)),"Complete translations and compatible placeholders: "+text);
            Check(QualityOfLifeText.Get(text,"unknown")==text,"Unknown language falls back to English: "+text);
        }
        var root=Path.Combine(Path.GetTempPath(),"import-declarations-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var previousDirectory=Environment.CurrentDirectory;
        try
        {
            var a=Path.Combine(root,"original.wav");var b=Path.Combine(root,"different-name.wav");var cache=Path.Combine(root,"marks");
            File.WriteAllText(a,"same content");File.Copy(a,b);
            Check(ImportedResourceDeclarations.Read(a,cache)==null,"Unmarked files are distinct from an explicit No");
            ImportedResourceDeclarations.Write(a,cache,true);
            Check(ImportedResourceDeclarations.Read(b,cache)==true,"A renamed duplicate retains its declaration by content hash");
            ImportedResourceDeclarations.Write(b,cache,false);
            Check(ImportedResourceDeclarations.Read(a,cache)==false,"Explicit No persists across names and fresh reads");
            ImportedResourceDeclarations.Write(a,cache,true);File.WriteAllText(b,"different content");
            Check(ImportedResourceDeclarations.Read(b,cache)==null,"Replaced file content must not inherit an old declaration");
            Check(ImportedResourceDeclarations.Read(a,Path.Combine(root,"voice-marks"))==null,"Audio destination contexts stay independent");
            Check(Directory.GetFiles(cache,"*.tmp").Length==0,"Atomic saves leave no staging files");

            using(var host=new Form {ShowInTaskbar=false,Opacity=0,Location=new(-20000,-20000),StartPosition=FormStartPosition.Manual})
            using(var list=new ListView {Dock=DockStyle.Fill})
            using(var images=new ImageList {ImageSize=new Size(112,112),ColorDepth=ColorDepth.Depth32Bit})
            {
                using(var original=new Bitmap(112,112)){using var graphics=System.Drawing.Graphics.FromImage(original);graphics.Clear(System.Drawing.Color.RoyalBlue);images.Images.Add("original",original);_=images.Handle;}
                list.LargeImageList=images;
                host.Controls.Add(list);var item=new ListViewItem("Original") {Tag=a,ImageKey="original"};list.Items.Add(item);host.Show();Application.DoEvents();
                ImportedResourceDeclarationControls.RefreshBadges(list,i=>(string)i.Tag!,_=>cache);
                var prefix=QualityOfLifeText.Get("[Imported] ",CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                var deadline=DateTime.UtcNow.AddSeconds(10);
                while(!item.Text.StartsWith(prefix,StringComparison.Ordinal) && DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(10);}
                Check(item.Text==prefix+"Original" && item.ForeColor==System.Drawing.Color.LightGreen,"Native list displays the persistent imported badge");
                using(var thumbnail=new Bitmap(images.Images[item.ImageKey]!))
                    Check(thumbnail.GetPixel(4,4).G>70 && thumbnail.GetPixel(4,4).B<65,"A contrasting green IMPORTED badge is drawn directly on the thumbnail");
                ImportedResourceDeclarations.Write(a,cache,false);ImportedResourceDeclarationControls.RefreshBadges(list,i=>(string)i.Tag!,_=>cache);
                deadline=DateTime.UtcNow.AddSeconds(10);
                while(item.Text.StartsWith(prefix,StringComparison.Ordinal) && DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(10);}
                Check(item.Text=="Original" && item.ImageKey=="original","Unmarking restores the original caption and thumbnail");host.Close();
            }
            Directory.CreateDirectory(Path.Combine(root,"resources"));Environment.CurrentDirectory=root;
            using(var animation=new FrmAnimationImport())
            using(var sound=new FrmSoundImport())
            {
                foreach(var form in new Form[]{animation,sound})
                {
                    var button=form.Controls.Find("DeclareImportedResource",true).OfType<Button>().Single();
                    Check(button.Text==QualityOfLifeText.Get("Declare import status...",CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),"Both importers expose the localized declaration action");
                    Check(!button.Enabled,"Declaration is disabled without a selected source");
                }
            }
            VerifyImporterPreviews(root);
            Console.WriteLine("PASS manual import tracking: Yes/No persistence, renamed duplicates, changed content, destination isolation, native list badges and both importer buttons");
        }
        finally{Environment.CurrentDirectory=previousDirectory;var target=Path.GetFullPath(root);var temporaryRoot=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!target.StartsWith(temporaryRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Test cleanup escaped the temporary directory");
            Directory.Delete(target,true);}
    }
    private static void VerifyImporterPreviews(string root)
    {
        var animationSource=Path.Combine(root,"animationimport","Combat","already-in-game.png");Directory.CreateDirectory(Path.GetDirectoryName(animationSource)!);
        foreach(var pair in new[]{(animationSource,System.Drawing.Color.RoyalBlue),(Path.Combine(Path.GetDirectoryName(animationSource)!,"new-resource.png"),System.Drawing.Color.OrangeRed)})
        {
            using var bitmap=new Bitmap(64,64);using(var graphics=System.Drawing.Graphics.FromImage(bitmap)){graphics.Clear(pair.Item2);graphics.FillEllipse(Brushes.Gold,12,12,40,40);}bitmap.Save(pair.Item1,System.Drawing.Imaging.ImageFormat.Png);
        }
        ImportedResourceDeclarations.Write(animationSource,Path.Combine(root,".animationimport-cache","imported"),true);
        var soundSource=Path.Combine(SoundImporter.LibraryFolder(Path.Combine(root,"soundimport"),SoundImportKind.Sound),"Combat","already-in-game.wav");Directory.CreateDirectory(Path.GetDirectoryName(soundSource)!);
        foreach(var pair in new[]{(soundSource,(short)0),(Path.Combine(Path.GetDirectoryName(soundSource)!,"new-resource.wav"),(short)100)})
        {
            using var writer=new BinaryWriter(File.Create(pair.Item1));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(16036);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(16000);for(var i=0;i<8000;i++)writer.Write(pair.Item2);
        }
        ImportedResourceDeclarations.Write(soundSource,Path.Combine(root,".soundimport-cache","imported","sounds"),true);
        var previous=CultureInfo.CurrentUICulture;CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("fr");
        try
        {
            foreach(var form in new Form[]{new FrmAnimationImport(),new FrmSoundImport()})
            using(form)
            {
                form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new(-20000,-20000);form.Opacity=0;form.Show();
                var animation=form is FrmAnimationImport;
                var list=(ListView)form.GetType().GetField(animation?"_assetList":"_soundList",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
                var deadline=DateTime.UtcNow.AddSeconds(20);ListViewItem? marked=null;
                while(DateTime.UtcNow<deadline)
                {
                    Application.DoEvents();
                    marked=list.Items.Cast<ListViewItem>().FirstOrDefault(item=>item.Text.StartsWith("[Importé] ",StringComparison.Ordinal));
                    if(marked!=null && (animation?marked.ImageKey.EndsWith("already-in-game.png",StringComparison.OrdinalIgnoreCase):marked.SubItems.Count>5 && marked.SubItems[5].Text=="IMPORTÉ"))break;
                    Thread.Sleep(10);
                }
                Check(marked!=null && marked.Text.StartsWith("[Importé] ",StringComparison.Ordinal),"Marked resource is immediately visible in the actual importer library");
                if(animation)Check(marked!.ImageKey.EndsWith("already-in-game.png",StringComparison.OrdinalIgnoreCase),"The imported badge stays on the real animation thumbnail after it loads");
                if(!animation)Check(list.Columns["ImportDeclarationStatus"]!.DisplayIndex==0 && marked!.SubItems[5].Text=="IMPORTÉ","Audio has a dedicated visible import-status column");
                var preview=Environment.GetEnvironmentVariable("IMPORT_DECLARATION_PREVIEW");
                if(!string.IsNullOrEmpty(preview))
                {
                    Directory.CreateDirectory(preview);using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));
                    image.Save(Path.Combine(preview,animation?"animation-import.png":"sound-import.png"),System.Drawing.Imaging.ImageFormat.Png);
                }
                form.Close();Application.DoEvents();
            }
        }
        finally{CultureInfo.CurrentUICulture=previous;}
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
