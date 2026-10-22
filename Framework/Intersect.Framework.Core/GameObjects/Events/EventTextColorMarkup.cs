using System.Text.RegularExpressions;
namespace Intersect.Framework.Core.GameObjects.Events;
public static class EventTextColorMarkup
{
    private static readonly Regex Tags=new(@"\\c\{[^}]*\}",RegexOptions.Compiled);
    public static string ApplySelection(string text,int start,int length,Color color)
    {
        if(start<0 || length<=0 || start>text.Length-length)return text;
        var end=start+length;
        foreach(Match tag in Tags.Matches(text))
        {
            if(tag.Index<start && tag.Index+tag.Length>start)start=tag.Index;
            if(tag.Index<end && tag.Index+tag.Length>end)end=tag.Index+tag.Length;
        }
        var restore="";
        foreach(Match tag in Tags.Matches(text[..end]))restore=tag.Value[3..^1];
        var selected=Tags.Replace(text[start..end],"");
        return text[..start]+"\\c{"+color+"}"+selected+"\\c{"+restore+"}"+text[end..];
    }
}
