namespace Intersect.Client.Interface.Shared;
internal sealed class AppearanceStyleComparer:IComparer<string>
{
    internal static readonly AppearanceStyleComparer Instance=new();
    public int Compare(string? left,string? right)
    {
        if(left==null || right==null)return StringComparer.OrdinalIgnoreCase.Compare(left,right);
        var a=0;var b=0;
        while(a<left.Length && b<right.Length)
        {
            if(char.IsAsciiDigit(left[a]) && char.IsAsciiDigit(right[b]))
            {
                var endA=a;var endB=b;
                while(endA<left.Length && char.IsAsciiDigit(left[endA]))endA++;
                while(endB<right.Length && char.IsAsciiDigit(right[endB]))endB++;
                while(a<endA && left[a]=='0')a++;
                while(b<endB && right[b]=='0')b++;
                var length=(endA-a).CompareTo(endB-b);if(length!=0)return length;
                var number=left.AsSpan(a,endA-a).CompareTo(right.AsSpan(b,endB-b),StringComparison.Ordinal);
                if(number!=0)return number;a=endA;b=endB;continue;
            }
            var character=char.ToUpperInvariant(left[a++]).CompareTo(char.ToUpperInvariant(right[b++]));
            if(character!=0)return character;
        }
        var remaining=(left.Length-a).CompareTo(right.Length-b);
        return remaining!=0?remaining:StringComparer.OrdinalIgnoreCase.Compare(left,right);
    }
}
