namespace Intersect.Framework.Core.Naval;

public sealed class NavalEffectAtlas
{
    public int Version {get;set;}=1;
    public int ImageWidth {get;set;}
    public int ImageHeight {get;set;}
    public Dictionary<string,ShipSpriteFrame[]> Clips {get;set;}=[];
    public bool IsValid(int width,int height)=>Version==1 && width==ImageWidth && height==ImageHeight && width is >0 and <=16384 && height is >0 and <=16384 &&
        Clips!=null && Clips.Count is >0 and <=16 && Clips.Values.All(row=>row!=null && row.Length is >0 and <=16 && row.All(f=>f!=null &&
            f.X>=0 && f.Y>=0 && f.Width>0 && f.Height>0 && f.X<=width-f.Width && f.Y<=height-f.Height &&
            double.IsFinite(f.AnchorX) && double.IsFinite(f.AnchorY) && f.AnchorX>=0 && f.AnchorX<=f.Width && f.AnchorY>=0 && f.AnchorY<=f.Height));
    public ShipSpriteFrame? Frame(string clip,long elapsed,int milliseconds)=>Clips.TryGetValue(clip,out var row)?row[(int)Math.Clamp(elapsed/Math.Max(20,milliseconds),0,row.Length-1)]:null;
}
