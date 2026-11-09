using Intersect.Client.Entities;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Enums;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Graphics=Intersect.Client.Core.Graphics;

namespace Intersect.Client.Naval;
internal static partial class NavalWorld
{
    private static IEnumerable<(NavalNpcDeckMember Npc,ShipCrewAnchor Anchor)> NpcDeckCrew(ShipSnapshot ship,ShipSpriteFrame pose)
    {
        var used=new HashSet<(ShipCrewRole,int)>();
        foreach(var member in (ship.NpcDeckMembers??[]).Take(7))
        {
            if(member.NpcId==Guid.Empty || !NavalPresentation.SafeAsset(member.Sprite) || string.IsNullOrWhiteSpace(member.Sprite) ||
                !Enum.IsDefined(member.Role) || !used.Add((member.Role,member.Index)))continue;
            var anchor=pose.CrewAnchors.FirstOrDefault(a=>a.Role==member.Role && a.Index==member.Index);
            if(anchor!=null)yield return(member,anchor);
        }
    }
    private static void DrawNpcOnDeck(NavalNpcDeckMember npc,ShipCrewAnchor anchor,ShipSpriteFrame pose,float x,float y,float scale)
    {
        if(Globals.ContentManager?.GetTexture(TextureType.Entity,npc.Sprite) is not {} texture)return;
        int columns=Math.Max(1,Options.Instance.Sprites.NormalFrames),rows=Math.Max(1,Options.Instance.Sprites.Directions);
        int width=texture.Width/columns,height=texture.Height/rows;if(width<1 || height<1)return;
        var point=DeckPoint(pose,anchor,x,y,scale);
        int row=point.Facing switch {Direction.Left=>1,Direction.Right=>2,Direction.Up=>3,_=>0};
        if(row>=rows)return;
        var source=new FloatRect(0,row*height,width,height);var bounds=SpriteVisibleBounds.Get(texture,source);
        float s=ShipDeck.AvatarScale;
        Graphics.DrawGameTexture(texture,source,new FloatRect(point.X-(bounds.X+bounds.Width/2f)*s,point.Y-(bounds.Y+bounds.Height)*s,width*s,height*s),Color.White);
    }
}
