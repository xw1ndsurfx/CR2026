namespace Intersect.Framework.Core.Naval;

public sealed class NavalRepairParticipant
{
    public Guid PlayerId {get;set;}
    public string Name {get;set;}="";
    public SailingProgress Progress {get;set;}
}

public static class NavalRepairPuzzle
{
    public const int Columns=6;
    public static string Caption(NavalRepairKind kind)=>kind==NavalRepairKind.Pumping?"Hull panels (2 x 2)":kind==NavalRepairKind.Caulking?"Sealing strips (3 cells)":"Wooden planks (2 cells)";
    public static byte[] Create(NavalRepairKind kind,int pieces)
    {
        pieces=Math.Clamp(pieces,3,30);
        if(kind!=NavalRepairKind.Pumping)
        {
            var count=pieces*(kind==NavalRepairKind.Caulking?3:2);
            return Enumerable.Range(0,((count+5)/6)*6).Select(i=>(byte)(i<count?1:0)).ToArray();
        }
        var cells=new byte[((pieces+2)/3)*12];
        for(var n=0;n<pieces;n++)foreach(var offset in new[]{0,1,6,7})cells[n/3*12+n%3*2+offset]=1;
        return cells;
    }
    public static int[] Shape(NavalRepairKind kind,bool rotated)=>kind==NavalRepairKind.Pumping?[0,1,6,7]:
        kind==NavalRepairKind.Caulking?(rotated?[0,6,12]:[0,1,2]):rotated?[0,6]:[0,1];
    public static bool CanPlace(byte[] cells,NavalRepairKind kind,int cell,bool rotated)
    {
        if(!Enum.IsDefined(kind) || cells.Length is <6 or >120 || cell<0 || cell>=cells.Length)return false;
        return Shape(kind,rotated).All(offset=>cell+offset<cells.Length && cell%6+offset%6<6 && cells[cell+offset]==1);
    }
    public static bool Place(byte[] cells,NavalRepairKind kind,int cell,bool rotated)
    {
        if(!CanPlace(cells,kind,cell,rotated))return false;
        foreach(var offset in Shape(kind,rotated))cells[cell+offset]=2;
        return true;
    }
}
