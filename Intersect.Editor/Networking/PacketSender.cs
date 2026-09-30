using System.Drawing.Imaging;
using Intersect.Editor.Core;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.GameObjects;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Models;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.WorldEvents;

namespace Intersect.Editor.Networking;


public static partial class PacketSender
{

    public static void SendPing()
    {
        Network.SendPacket(new PingPacket());
    }

    public static void SendLogin(string username, string password)
    {
        Network.SendPacket(new LoginPacket(username, password));
    }

    public static void SendNeedMap(Guid mapId)
    {
        Network.SendPacket(new NeedMapPacket(mapId));
    }

    public static void SendWikiMapPreview(Guid mapId, byte[] pngData)
    {
        if (mapId == Guid.Empty ||
            pngData == null ||
            pngData.Length < 8 ||
            pngData.Length > 8 * 1024 * 1024)
        {
            return;
        }

        Network.SendPacket(new WikiMapPreviewPacket(mapId, pngData));
    }


    public static void SendMap(MapInstance map)
    {
        Network.SendPacket(new MapUpdatePacket(map.Id, map.JsonData, map.GenerateTileData(), map.AttributeData));

        if (Globals.CurrentMap?.Id != map.Id)
        {
            return;
        }

        try
        {
            byte[] previewBytes;
            using (var screenshot = Intersect.Editor.Core.Graphics.ScreenShotMap())
            {
                using var stream = new MemoryStream();
                screenshot.Save(stream, ImageFormat.Png);
                previewBytes = stream.ToArray();
            }

            SendWikiMapPreview(map.Id, previewBytes);
        }
        catch
        {
            // A wiki preview must never prevent saving the map itself.
        }
    }

    public static void SendCreateMap(int location, Guid currentMapId, MapListItem parent)
    {
        if (location > -1)
        {
            Network.SendPacket(new CreateMapPacket(currentMapId, (byte) location));
            return;
        }

        switch (parent)
        {
            case null:
                Network.SendPacket(new CreateMapPacket(0, Guid.Empty));
                break;

            case MapListMap map:
                Network.SendPacket(new CreateMapPacket(1, map.MapId));
                break;

            case MapListFolder folder:
                Network.SendPacket(new CreateMapPacket(0, folder.FolderId));
                break;
        }
    }

    public static void SendMapListMove(int srcType, Guid srcId, int destType, Guid destId)
    {
        Network.SendPacket(new MapListUpdatePacket(MapListUpdate.MoveItem, srcType, srcId, destType, destId, ""));
    }

    public static void SendAddFolder(MapListItem parent)
    {
        switch (parent)
        {
            case null:
                Network.SendPacket(new MapListUpdatePacket(MapListUpdate.AddFolder, 0, Guid.Empty, 0, Guid.Empty, string.Empty));
                break;

            case MapListMap map:
                Network.SendPacket(
                    new MapListUpdatePacket(
                        MapListUpdate.AddFolder, 0, Guid.Empty, 1, map.MapId, string.Empty
                    )
                );
                break;

            case MapListFolder folder:
                Network.SendPacket(
                    new MapListUpdatePacket(
                        MapListUpdate.AddFolder, 0, Guid.Empty, 0, folder.FolderId, string.Empty
                    )
                );
                break;
        }
    }

    public static void SendRename(MapListItem parent, string name)
    {
        switch (parent)
        {
            case MapListMap map:
                Network.SendPacket(
                    new MapListUpdatePacket(MapListUpdate.Rename, 1, map.MapId, 0, Guid.Empty, name)
                );
                break;

            case MapListFolder folder:
                Network.SendPacket(
                    new MapListUpdatePacket(
                        MapListUpdate.Rename, 0, folder.FolderId, 0, Guid.Empty, name
                    )
                );
                break;
        }
    }

    public static void SendDelete(MapListItem target)
    {
        switch (target)
        {
            case MapListMap map:
                Network.SendPacket(
                    new MapListUpdatePacket(MapListUpdate.Delete, 1, map.MapId, 0, Guid.Empty, string.Empty)
                );
                break;

            case MapListFolder folder:
                Network.SendPacket(
                    new MapListUpdatePacket(
                        MapListUpdate.Delete, 0, folder.FolderId, 0, Guid.Empty, string.Empty
                    )
                );
                break;
        }
    }

    public static void SendNeedGrid(Guid mapId)
    {
        Network.SendPacket(new RequestGridPacket(mapId));
    }

    public static void SendUnlinkMap(Guid mapId)
    {
        Network.SendPacket(new UnlinkMapPacket(mapId, Globals.CurrentMap.Id));
    }

    public static void SendLinkMap(Guid adjacentMapId, Guid linkMapId, int gridX, int gridY)
    {
        Network.SendPacket(new LinkMapPacket(linkMapId, adjacentMapId, gridX, gridY));
    }

    public static void SendCreateObject(GameObjectType type)
    {
        Network.SendPacket(new CreateGameObjectPacket(type));
    }

    public static void SendOpenEditor(GameObjectType type)
    {
        if (Globals.CurrentEditor != -1)
        {
            return;
        }

        Network.SendPacket(new RequestOpenEditorPacket(type));
    }

    public static void SendDeleteObject(IDatabaseObject obj)
    {
        Network.SendPacket(new DeleteGameObjectPacket(obj.Type, obj.Id));
    }

    public static void SendSaveObject(IDatabaseObject obj)
    {
        Network.SendPacket(new SaveGameObjectPacket(obj.Type, obj.Id, obj.JsonData));
        TrySendWikiGameAsset(obj);
    }

    private static void TrySendWikiGameAsset(IDatabaseObject obj)
    {
        try
        {
            switch (obj)
            {
                case ItemDescriptor item when !string.IsNullOrWhiteSpace(item.Icon):
                    TrySendPngFile("items", item.Id, Path.Combine("resources", "items", item.Icon));
                    break;

                case SpellDescriptor spell when !string.IsNullOrWhiteSpace(spell.Icon):
                    TrySendPngFile("spells", spell.Id, Path.Combine("resources", "spells", spell.Icon));
                    break;

                case ResourceDescriptor resource:
                    TrySendResourcePreview(resource);
                    break;
            }
        }
        catch
        {
            // Wiki images must never block saving game data.
        }
    }

    private static void TrySendPngFile(string category, Guid objectId, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length is <= 0 or > 8 * 1024 * 1024)
        {
            return;
        }

        Network.SendPacket(new WikiGameAssetUploadPacket(category, objectId, bytes));
    }

    private static void TrySendResourcePreview(ResourceDescriptor resource)
    {
        var state = resource.States?.Values
            .Where(value => value != null)
            .OrderByDescending(value => value.MaximumHealth)
            .ThenByDescending(value => value.MinimumHealth)
            .FirstOrDefault();

        if (state == null)
        {
            return;
        }

        string? sourcePath = null;
        System.Drawing.Rectangle? crop = null;

        switch (state.TextureType)
        {
            case ResourceTextureSource.Resource:
                sourcePath = ResolvePngPath(Path.Combine("resources", "resources"), state.TextureName);
                crop = ResourceCrop(state);
                break;

            case ResourceTextureSource.Tileset:
                sourcePath = ResolvePngPath(Path.Combine("resources", "tilesets"), state.TextureName);
                crop = ResourceCrop(state);
                break;

            case ResourceTextureSource.Animation:
                var animation = AnimationDescriptor.Get(state.AnimationId);
                var layer = !string.IsNullOrWhiteSpace(animation?.Lower?.Sprite)
                    ? animation.Lower
                    : animation?.Upper;

                if (layer == null || string.IsNullOrWhiteSpace(layer.Sprite))
                {
                    return;
                }

                sourcePath = ResolvePngPath(Path.Combine("resources", "animations"), layer.Sprite);
                if (sourcePath != null)
                {
                    using var probe = System.Drawing.Image.FromFile(sourcePath);
                    var frameWidth = Math.Max(1, probe.Width / Math.Max(1, layer.XFrames));
                    var frameHeight = Math.Max(1, probe.Height / Math.Max(1, layer.YFrames));
                    crop = new System.Drawing.Rectangle(0, 0, frameWidth, frameHeight);
                }
                break;
        }

        if (sourcePath == null || !File.Exists(sourcePath))
        {
            return;
        }

        using var image = System.Drawing.Image.FromFile(sourcePath);
        using var bitmap = new System.Drawing.Bitmap(image);

        System.Drawing.Bitmap output;
        if (crop is { } rect &&
            rect.Width > 0 &&
            rect.Height > 0 &&
            rect.X >= 0 &&
            rect.Y >= 0 &&
            rect.Right <= bitmap.Width &&
            rect.Bottom <= bitmap.Height)
        {
            output = bitmap.Clone(rect, PixelFormat.Format32bppArgb);
        }
        else
        {
            output = new System.Drawing.Bitmap(bitmap);
        }

        using (output)
        using (var stream = new MemoryStream())
        {
            output.Save(stream, ImageFormat.Png);
            var bytes = stream.ToArray();
            if (bytes.Length is > 0 and <= 8 * 1024 * 1024)
            {
                Network.SendPacket(new WikiGameAssetUploadPacket("resources", resource.Id, bytes));
            }
        }
    }

    private static string? ResolvePngPath(string directory, string? configuredName)
    {
        if (string.IsNullOrWhiteSpace(configuredName))
        {
            return null;
        }

        var fileName = Path.GetFileName(configuredName);
        if (Path.GetExtension(fileName).Length == 0)
        {
            fileName += ".png";
        }

        var path = Path.Combine(directory, fileName);
        return File.Exists(path) ? path : null;
    }

    private static System.Drawing.Rectangle? ResourceCrop(ResourceStateDescriptor state)
    {
        if (state.Width <= 0 || state.Height <= 0)
        {
            return null;
        }

        return new System.Drawing.Rectangle(
            Math.Max(0, state.X),
            Math.Max(0, state.Y),
            state.Width,
            state.Height
        );
    }

    public static void SendSaveTime(string timeJson)
    {
        Network.SendPacket(new SaveTimeDataPacket(timeJson));
    }

    public static void SendRequestRewardConfiguration()
    {
        Network.SendPacket(new RequestRewardConfigurationPacket());
    }

    public static void SendSaveRewardConfiguration(string configurationJson)
    {
        Network.SendPacket(new SaveRewardConfigurationPacket(configurationJson));
    }

    public static void SendRequestLogiCoinShopConfiguration()
    {
        Network.SendPacket(new RequestLogiCoinShopConfigurationPacket());
    }

    public static void SendSaveLogiCoinShopConfiguration(string configurationJson)
    {
        Network.SendPacket(new SaveLogiCoinShopConfigurationPacket(configurationJson));
    }

    public static void SendRequestProfessionConfiguration(bool openEditor)
    {
        Network.SendPacket(new RequestProfessionConfigurationPacket(openEditor));
    }

    public static void SendSaveProfessionConfiguration(string configurationJson)
    {
        Network.SendPacket(new SaveProfessionConfigurationPacket(configurationJson));
    }

    public static void SendRequestAchievementConfiguration(bool openEditor)
    {
        Network.SendPacket(new RequestAchievementConfigurationPacket(openEditor));
    }

    public static void SendSaveAchievementConfiguration(string configurationJson)
    {
        Network.SendPacket(new SaveAchievementConfigurationPacket(configurationJson));
    }

    public static void SendRequestInvasionConfiguration()
    {
        Network.SendPacket(new RequestInvasionConfigurationPacket());
    }

    public static void SendSaveInvasionConfiguration(string configurationJson)
    {
        Network.SendPacket(new SaveInvasionConfigurationPacket(configurationJson));
    }

    public static void SendStartInvasionNow(Guid invasionId)
    {
        Network.SendPacket(new StartInvasionNowPacket(invasionId));
    }

    public static void SendNewTilesets(string[] tilesets)
    {
        Network.SendPacket(new AddTilesetsPacket(tilesets));
    }

    public static void SendEnterMap(Guid mapId)
    {
        Network.SendPacket(new EnterMapPacket(mapId));
    }

}
