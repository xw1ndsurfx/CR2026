using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

var root=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Documentation/NavalCannonArt/Assets.json")));
var entries=manifest.RootElement.EnumerateArray().ToArray();
if(entries.Length!=28 || entries.Select(entry=>entry.GetProperty("ModelId").GetString()).Distinct().Count()!=28)
    throw new Exception("Expected 28 distinct combat ship models.");
var factions=new[]{"royal","adventurer","herbonia","majestya","skeleton","ghost","pirate"};
var kinds=new[]{"three-mast","sloop","raft","line"};
const int cellWidth=320,cellHeight=150;
using var sheet=new Bitmap(cellWidth*5,cellHeight*factions.Length);
using var graphics=Graphics.FromImage(sheet);
graphics.Clear(Color.FromArgb(24,30,38));
graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
using var font=new Font("Segoe UI",11,FontStyle.Regular,GraphicsUnit.Pixel);
var reports=new List<object>();var errors=new List<string>();
foreach(var entry in entries)
{
    var id=entry.GetProperty("ModelId").GetString()!;
    var name=entry.GetProperty("Graphic").GetString()!;
    var path=Path.Combine(root,"resources/ocean",name);
    var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    if(hash!=entry.GetProperty("Sha256").GetString())throw new Exception("Generated pixels changed: "+name);
    using var image=new Bitmap(path);
    using var sidecar=JsonDocument.Parse(File.ReadAllText(path+".crew.json"));var art=sidecar.RootElement;
    if(image.Width!=entry.GetProperty("Width").GetInt32() || image.Height!=entry.GetProperty("Height").GetInt32() ||
        art.GetProperty("ImageWidth").GetInt32()!=image.Width || art.GetProperty("ImageHeight").GetInt32()!=image.Height)
        throw new Exception("Image dimensions mismatch: "+name);
    var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
    byte[] pixels;int stride;
    try
    {
        stride=Math.Abs(data.Stride);pixels=new byte[stride*image.Height];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
    }
    finally {image.UnlockBits(data);}
    int Alpha(int x,int y)=>pixels[y*stride+x*4+3];
    var transparent=0;var translucent=0;var opaque=0;
    for(var y=0;y<image.Height;y++)for(var x=0;x<image.Width;x++)
    {var alpha=Alpha(x,y);if(alpha==0)transparent++;else if(alpha<255)translucent++;else opaque++;}
    if(transparent<image.Width*image.Height/100)errors.Add("No clear transparent ocean area: "+name+"; alpha at top center="+Alpha(image.Width/2,10)+" zero="+transparent+" partial="+translucent+" opaque="+opaque);
    if(id.StartsWith("ghost-") && translucent<Math.Max(1,(translucent+opaque)/8))
        errors.Add("Ghost material must be translucent: "+name);
    Rectangle Frame(string key)
    {
        var frame=art.GetProperty(key);
        return new(frame.GetProperty("X").GetInt32(),frame.GetProperty("Y").GetInt32(),frame.GetProperty("Width").GetInt32(),frame.GetProperty("Height").GetInt32());
    }
    var frameKeys=entry.GetProperty("Rows").GetInt32()==6
        ?new[]{"LeftFrame","Frame","RightFrame","LowerLeftFrame","LowerFrame","LowerRightFrame"}
        :new[]{"LeftFrame","Frame","RightFrame"};
    foreach(var key in frameKeys)
    {
        var frame=Frame(key);
        if(frame.X<0 || frame.Y<0 || frame.Right>image.Width || frame.Bottom>image.Height || frame.Width<=0 || frame.Height<=0)
            throw new Exception("Pose outside texture: "+name+" "+key);
        var left=false;var right=false;var clear=0;
        for(var y=frame.Y;y<frame.Bottom;y++)for(var x=frame.X;x<frame.Right;x++)
        {
            var alpha=Alpha(x,y);if(alpha==0)clear++;
            if(y>=frame.Y+frame.Height/2 && alpha>64)
            {if(x<frame.X+8)left=true;if(x>=frame.Right-8)right=true;}
        }
        if(!left || !right)errors.Add("Foreground fails to reach both edges: "+name+" "+key);
        if(clear<frame.Width*frame.Height/200)errors.Add("Pose has no transparent view: "+name+" "+key);
        if(!key.StartsWith("Lower",StringComparison.Ordinal))
        {
            var topInk=0;var sampleWidth=frame.Width*3/5;
            for(var y=frame.Y;y<Math.Min(frame.Y+2,frame.Bottom);y++)
                for(var x=frame.X+frame.Width/5;x<frame.X+frame.Width/5+sampleWidth;x++)
                    if(Alpha(x,y)>64)topInk++;
            if(topInk>sampleWidth/2)errors.Add("Previous-pose line at top of frame: "+name+" "+key);
        }
    }
    var faction=Array.FindIndex(factions,f=>id.StartsWith(f+"-",StringComparison.Ordinal));
    var kind=Array.FindIndex(kinds,k=>id.EndsWith("-"+k,StringComparison.Ordinal));
    if(faction<0 || kind<0)throw new Exception("Unknown model "+id);
    void Preview(string key,int column,string caption)
    {
        var x=column*cellWidth;var y=faction*cellHeight;var frame=Frame(key);
        using var ocean=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(x,y+20,cellWidth,cellHeight-20),
            Color.FromArgb(116,172,203),Color.FromArgb(20,109,143),90f);
        graphics.FillRectangle(ocean,x,y+20,cellWidth,cellHeight-20);
        var height=cellWidth*frame.Height/(float)frame.Width;
        graphics.DrawImage(image,new RectangleF(x,y+cellHeight-height,cellWidth,height),frame,GraphicsUnit.Pixel);
        graphics.DrawString(caption,font,Brushes.White,x+4,y+2);
    }
    Preview("Frame",kind,id);
    if(kind==3)Preview("LowerFrame",4,id+" / lower battery");
    reports.Add(new {ModelId=id,Graphic=name,Width=image.Width,Height=image.Height,TransparentPixels=transparent,
        TranslucentPixels=translucent,OpaquePixels=opaque,Poses=frameKeys.Length,UnmodifiedSha256=hash});
}
sheet.Save(Path.Combine(output,"cannon-stations-contact-sheet.png"),ImageFormat.Png);
File.WriteAllText(Path.Combine(output,"cannon-assets-validation.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(output,"cannon-art-errors.json"),JsonSerializer.Serialize(errors,new JsonSerializerOptions{WriteIndented=true}));
if(errors.Count>0){foreach(var error in errors)Console.WriteLine("FAIL: "+error);Environment.ExitCode=1;}
else Console.WriteLine("PASS: 28 atlases, source hashes, all pose bounds, alpha, ghost translucency, full-width deck edges and contact sheet.");
