using System.Buffers.Binary;
using System.IO.Compression;
using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
namespace Intersect.Client.Interface.Game;

internal static class ItemIconTexture
{
    private static readonly Dictionary<string,IGameTexture> Portraits = [];
    private static readonly Dictionary<Guid,NPCDescriptor> LinkedPets = [];
    private static long _lookupAt;
    internal static IGameTexture? Get(ItemDescriptor item)
    {
        if (Environment.TickCount64 >= _lookupAt)
        {
            LinkedPets.Clear();
            foreach (var npc in NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().Where(n => n.IsPet && n.PetSummonItemId != Guid.Empty && !string.IsNullOrWhiteSpace(n.Sprite))) LinkedPets.TryAdd(npc.PetSummonItemId,npc);
            _lookupAt=Environment.TickCount64+2000;
        }
        LinkedPets.TryGetValue(item.Id,out var pet);
        if (pet == null) return GameContentManager.Current.GetTexture(item.UsesSpellIcon ? TextureType.Spell : TextureType.Item,item.DisplayIcon);
        var sheet = GameContentManager.Current.GetTexture(TextureType.Entity,pet.Sprite);
        if (sheet == null) return GameContentManager.Current.GetTexture(TextureType.Item,item.Icon);
        var width = sheet.Width / Math.Max(1,Options.Instance.Sprites.NormalFrames);
        var height = sheet.Height / Math.Max(1,Options.Instance.Sprites.Directions);
        if (width<=0 || height<=0) return null;
        var key = $"{pet.Sprite}/{width}/{height}";
        if (Portraits.TryGetValue(key,out var portrait)) return portrait;
        var bounds = Intersect.Client.Entities.SpriteVisibleBounds.Get(sheet,new FloatRect(0,0,width,height));
        if (bounds.Width<=0 || bounds.Height<=0) return null;
        const int size=64;
        var scale=Math.Min(60f/bounds.Width,60f/bounds.Height);
        var drawWidth=Math.Max(1,(int)(bounds.Width*scale));var drawHeight=Math.Max(1,(int)(bounds.Height*scale));
        var data = new byte[(size*4+1)*size];
        for(var y=0;y<drawHeight;y++) for(var x=0;x<drawWidth;x++)
        {
            var color=sheet.GetPixel((int)bounds.X+Math.Min((int)bounds.Width-1,(int)(x/scale)),(int)bounds.Y+Math.Min((int)bounds.Height-1,(int)(y/scale)));
            var offset=((size-drawHeight)/2+y)*(size*4+1)+1+((size-drawWidth)/2+x)*4;
            data[offset]=color.R;data[offset+1]=color.G;data[offset+2]=color.B;data[offset+3]=color.A;
        }
        var png=Encode(size,data);
        portrait=Graphics.Renderer.CreateTextureFromStreamFactory("pet-portrait/"+key,()=>new MemoryStream(png,false));
        Portraits[key]=portrait;return portrait;
    }
    private static byte[] Encode(int size,byte[] pixels)
    {
        using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string name,byte[] data)
        {
            var length=new byte[4];BinaryPrimitives.WriteInt32BigEndian(length,data.Length);output.Write(length);
            var tag=System.Text.Encoding.ASCII.GetBytes(name);output.Write(tag);output.Write(data);
            uint crc=uint.MaxValue;
            foreach(var b in tag.Concat(data)){crc^=b;for(var bit=0;bit<8;bit++)crc=(crc>>1)^(0xedb88320u & (uint)-(int)(crc&1));}
            BinaryPrimitives.WriteUInt32BigEndian(length,~crc);output.Write(length);
        }
        var header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,size);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),size);header[8]=8;header[9]=6;Chunk("IHDR",header);
        using var compressed=new MemoryStream();using(var zlib=new ZLibStream(compressed,CompressionLevel.Fastest,true))zlib.Write(pixels);
        Chunk("IDAT",compressed.ToArray());Chunk("IEND",[]);return output.ToArray();
    }
}
