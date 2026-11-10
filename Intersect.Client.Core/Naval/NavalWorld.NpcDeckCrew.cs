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
    internal static IEnumerable<(NavalNpcDeckMember Npc,ShipCrewAnchor Anchor)> NpcDeckCrew(ShipSnapshot ship,ShipSpriteFrame pose)
    {
        var used=new HashSet<(ShipCrewRole,int)>();
        foreach(var member in (ship.NpcDeckMembers??[]).Take(13))
        {
            if(member.NpcId==Guid.Empty || !NavalPresentation.SafeAsset(member.Sprite) || string.IsNullOrWhiteSpace(member.Sprite) ||
                !Enum.IsDefined(member.Role) || !used.Add((member.Role,member.Index)))continue;
            var anchor=pose.CrewAnchors.FirstOrDefault(a=>a.Role==member.Role && a.Index==member.Index);
            if(anchor==null)continue;
            if(ShipDeck.Supports(ship.Graphics) && pose.DeckMasts.Length==3 && member.Role!=ShipCrewRole.Passenger &&
                !ShipDeck.LowerGunDeck(ShipDeck.Kind(ship.Graphics),member.Index))
            {
                var kind=ShipDeck.Kind(ship.Graphics);var helm=member.Role==ShipCrewRole.Helm;
                var local=helm?ShipDeck.Helm:ShipDeck.Station(member.Role==ShipCrewRole.StarboardCannon,member.Index,kind);
                var point=ShipDeckProjection.Project(pose,local);
                anchor=new(){Role=member.Role,Index=member.Index,X=point.X,Y=point.Y,
                    Facing=DeckFacing(pose,local,helm || kind==ShipDeckKind.Raft?0:member.Role==ShipCrewRole.StarboardCannon?1:-1,helm || kind==ShipDeckKind.Raft?1:0)};
            }
            yield return(member,anchor);
        }
    }
    private static void DrawNpcOnDeck(NavalNpcDeckMember npc,ShipCrewAnchor anchor,ShipSpriteFrame pose,float x,float y,float scale,float avatarScale)
    {
        if(Globals.ContentManager?.GetTexture(TextureType.Entity,npc.Sprite) is not {} texture)return;
        int columns=Math.Max(1,Options.Instance.Sprites.NormalFrames),rows=Math.Max(1,Options.Instance.Sprites.Directions);
        int width=texture.Width/columns,height=texture.Height/rows;if(width<1 || height<1)return;
        var point=DeckPoint(pose,anchor,x,y,scale);
        int row=point.Facing switch {Direction.Left=>1,Direction.Right=>2,Direction.Up=>3,_=>0};
        if(row>=rows)return;
        var source=new FloatRect(0,row*height,width,height);var bounds=SpriteVisibleBounds.Get(texture,source);
        var destination = new FloatRect(point.X - (bounds.X + bounds.Width / 2f) * avatarScale,
            point.Y - (bounds.Y + bounds.Height) * avatarScale, width * avatarScale, height * avatarScale);
        Graphics.DrawGameTexture(texture, source, destination, Color.White);
    }
}
