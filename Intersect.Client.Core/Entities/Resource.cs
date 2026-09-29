using System.Collections.Concurrent;
using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Entities;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Maps;
using Intersect.Client.General;
using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.Professions;
using Intersect.Network.Packets.Server;
using Microsoft.Extensions.Logging;

namespace Intersect.Client.Entities;

public partial class Resource : Entity, IResource
{
    private readonly record struct OpaqueTopCacheKey(
        string TextureName,
        int SourceX,
        int SourceY,
        int SourceWidth,
        int SourceHeight
    );

    private static readonly ConcurrentDictionary<OpaqueTopCacheKey, int> OpaqueTopCache = [];

    private FloatRect _renderBoundsDest = FloatRect.Empty;
    private FloatRect _renderBoundsSrc = FloatRect.Empty;

    private bool _waitingForTilesets;
    private bool _recalculateRenderBounds;

    private bool _isDead;
    private int _maximumHealthForStates;
    private ResourceStateDescriptor? _currentState;
    private ResourceDescriptor? _descriptor;
    private AnimationDescriptor? _animationDescriptor;
    private IAnimation? _activeAnimation;
    private IAnimation? _stateAnimation;

    private readonly int _tileWidth = Options.Instance.Map.TileWidth;
    private readonly int _tileHeight = Options.Instance.Map.TileHeight;
    private readonly int _mapHeight = Options.Instance.Map.MapHeight;

    /// <inheritdoc />
    public override bool CanBeAttacked => !IsDead;

    public ResourceStateDescriptor? CurrentState => _currentState;

    public Resource(Guid id, ResourceEntityPacket packet) : base(id, packet, EntityType.Resource)
    {
        mRenderPriority = 0;
    }

    public ResourceDescriptor? Descriptor
    {
        get => _descriptor;
        set
        {
            if (value == _descriptor)
            {
                return;
            }

            _descriptor = value;
            UpdateCurrentState();
        }
    }

    public bool IsDead
    {
        get => _isDead;
        set
        {
            if (value == _isDead)
            {
                return;
            }

            _isDead = value;
        }
    }

    public override string Sprite
    {
        get => _sprite;
        set
        {
            if (value == _sprite)
            {
                return;
            }

            if (Descriptor == null)
            {
                return;
            }

            _sprite = value;
            ReloadSpriteTexture();
        }
    }

    private void ReloadSpriteTexture()
    {
        if (Descriptor == null)
        {
            return;
        }

        switch (_currentState?.TextureType)
        {
            case ResourceTextureSource.Tileset:
                if (GameContentManager.Current.TilesetsLoaded)
                {
                    Texture = GameContentManager.Current.GetTexture(TextureType.Tileset, _currentState?.TextureName);
                }
                else
                {
                    _waitingForTilesets = true;
                }
                break;

            case ResourceTextureSource.Resource:
                Texture = GameContentManager.Current.GetTexture(TextureType.Resource, _currentState?.TextureName);
                break;

            case ResourceTextureSource.Animation:
                if (_stateAnimation?.Descriptor?.Id == _currentState.AnimationId)
                {
                    return;
                }

                _stateAnimation?.Dispose();
                _stateAnimation = null;

                if (MapInstance is not { } mapInstance)
                {
                    return;
                }

                if (_animationDescriptor is not { } animationDescriptor)
                {
                    return;
                }

                var animation = mapInstance.AddTileAnimation(animationDescriptor, X, Y, Direction.Up);
                if (animation is { IsDisposed: false })
                {
                    animation.InfiniteLoop = true;
                }
                _stateAnimation = animation;
                break;
        }

        if (Texture == default)
        {
            Texture = Graphics.Renderer.WhitePixel;
        }

        _recalculateRenderBounds = true;
    }

    public override void Load(EntityPacket? packet)
    {
        base.Load(packet);
        _recalculateRenderBounds = true;

        if (packet is not ResourceEntityPacket resourceEntityPacket)
        {
            return;
        }

        var wasDead = IsDead;
        IsDead = resourceEntityPacket.IsDead;

        var descriptorId = resourceEntityPacket.ResourceId;

        var justDied = !wasDead && resourceEntityPacket.IsDead;
        if (!ResourceDescriptor.TryGet(descriptorId, out var descriptor))
        {
            if (justDied)
            {
                ApplicationContext.CurrentContext.Logger.LogError(
                    "Unable to play resource exhaustion animation because resource {EntityId} ({EntityName}) is missing the descriptor ({DescriptorId})",
                    Id,
                    Name,
                    descriptorId
                );
            }

            return;
        }

        _maximumHealthForStates = (int)(descriptor.UseExplicitMaxHealthForResourceStates
            ? descriptor.MaxHp
            : MaxVital[(int)Enums.Vital.Health]);

        Descriptor = descriptor;

        if (!justDied)
        {
            return;
        }

        if (MapInstance is { } mapInstance)
        {
            var animation = mapInstance.AddTileAnimation(descriptor.DeathAnimationId, X, Y, Direction.Up);
            if (animation is { IsDisposed: false })
            {
                animation.Finished += OnAnimationDisposedOrFinished;
                animation.Disposed += OnAnimationDisposedOrFinished;
            }
            _activeAnimation = animation;
        }
        else
        {
            ApplicationContext.CurrentContext.Logger.LogError(
                "Unable to play resource exhaustion animation because resource {EntityId} ({EntityName}) has no reference to the map instance for map {MapId}",
                Id,
                Name,
                MapId
            );
        }
    }

    private void OnAnimationDisposedOrFinished(IAnimation animation)
    {
        if (_activeAnimation != animation)
        {
            return;
        }

        _activeAnimation = null;
        animation.Disposed -= OnAnimationDisposedOrFinished;
        animation.Finished -= OnAnimationDisposedOrFinished;
    }

    public void UpdateCurrentState()
    {
        if (Descriptor == default)
        {
            return;
        }

        var graphicStates = Descriptor.States;
        var currentHealthPercentage = Math.Floor((float)Vital[(int)Enums.Vital.Health] / _maximumHealthForStates * 100);

        if (_currentState is { } currentState &&
            currentHealthPercentage >= currentState?.MinimumHealth &&
            currentHealthPercentage <= currentState?.MaximumHealth
        )
        {
            return;
        }

        currentState = graphicStates.Values.FirstOrDefault(
            s => currentHealthPercentage >= s.MinimumHealth && currentHealthPercentage <= s.MaximumHealth
        );

        // dispose animation if animation is not used in this current state,
        // but the previous state was an animation
        if (
            _currentState?.TextureType == ResourceTextureSource.Animation &&
            currentState?.TextureType != ResourceTextureSource.Animation
        )
        {
            _stateAnimation?.Dispose();
            _stateAnimation = default;
        }

        _currentState = currentState;

        if (currentState is { TextureType: ResourceTextureSource.Animation } && currentState.AnimationId != Guid.Empty)
        {
            _ = AnimationDescriptor.TryGet(currentState.AnimationId, out _animationDescriptor);
        }

        ReloadSpriteTexture();
    }

    public override void Dispose()
    {
        if (RenderList != null)
        {
            _ = RenderList.Remove(this);
            RenderList = null;
        }

        ClearAnimations();
        GC.SuppressFinalize(this);
        mDisposed = true;
    }

    public override bool Update()
    {
        if (mDisposed)
        {
            LatestMap = null;
            return false;
        }

        if (Descriptor is { IsDeleted: true } deletedDescriptor)
        {
            _ = ResourceDescriptor.TryGet(deletedDescriptor.Id, out var descriptor);
            Descriptor = descriptor;
        }

        if (!Maps.MapInstance.TryGet(MapId, out var map) || !map.InView())
        {
            LatestMap = map;
            Globals.EntitiesToDispose.Add(Id);
            return false;
        }

        if (_recalculateRenderBounds)
        {
            CalculateRenderBounds();
        }

        if (!Graphics.WorldViewport.IntersectsWith(_renderBoundsDest))
        {
            if (RenderList != null)
            {
                _ = RenderList.Remove(this);
            }

            return true;
        }

        var result = base.Update();
        if (!result)
        {
            if (RenderList != null)
            {
                _ = RenderList.Remove(this);
            }
        }

        return result;
    }

    public override HashSet<Entity>? DetermineRenderOrder(HashSet<Entity>? renderList, IMapInstance? map)
    {
        if (CurrentState is not { } graphicState || !graphicState.RenderBelowEntities)
        {
            return base.DetermineRenderOrder(renderList, map);
        }

        //Otherwise we are alive or dead and we want to render below players/npcs
        if (renderList != null)
        {
            _ = renderList.Remove(this);
        }

        if (map == null)
        {
            return null;
        }

        if (Globals.MapGrid == default)
        {
            return null;
        }

        if (Globals.Me?.MapInstance == null)
        {
            return null;
        }

        if (Graphics.RenderingEntities == default)
        {
            return null;
        }

        var gridX = Globals.Me.MapInstance.GridX;
        var gridY = Globals.Me.MapInstance.GridY;
        for (var x = gridX - 1; x <= gridX + 1; x++)
        {
            for (var y = gridY - 1; y <= gridY + 1; y++)
            {
                if (x >= 0 &&
                    x < Globals.MapGridWidth &&
                    y >= 0 &&
                    y < Globals.MapGridHeight &&
                    Globals.MapGrid[x, y] != Guid.Empty)
                {
                    if (Globals.MapGrid[x, y] == MapId)
                    {
                        var priority = mRenderPriority;
                        if (Z != 0)
                        {
                            priority += 3;
                        }

                        HashSet<Entity> renderSet;

                        if (y == gridY - 1)
                        {
                            renderSet = Graphics.RenderingEntities[priority, Y];
                        }
                        else if (y == gridY)
                        {
                            renderSet = Graphics.RenderingEntities[priority, _mapHeight + Y];
                        }
                        else
                        {
                            renderSet = Graphics.RenderingEntities[priority, _mapHeight * 2 + Y];
                        }

                        _ = renderSet.Add(this);
                        renderList = renderSet;
                        return renderList;

                    }
                }
            }
        }

        return renderList;
    }

    private void CalculateRenderBounds()
    {
        if (Descriptor == default)
        {
            return;
        }

        if (MapInstance is not { } map)
        {
            return;
        }

        if (_waitingForTilesets)
        {
            if (GameContentManager.Current.TilesetsLoaded)
            {
                ReloadSpriteTexture();
                _waitingForTilesets = false;
            }
            else
            {
                // No textures yet
                return;
            }
        }

        if (Texture is not { } texture)
        {
            return;
        }

        if (CurrentState is not { } graphicState)
        {
            return;
        }

        _renderBoundsSrc.X = 0;
        _renderBoundsSrc.Y = 0;

        switch(graphicState.TextureType)
        {
            case ResourceTextureSource.Resource:
                _renderBoundsSrc.Width = texture.Width;
                _renderBoundsSrc.Height = texture.Height;
                break;

            case ResourceTextureSource.Tileset:
                ResourceStateDescriptor? selectedGraphic = null;

                if (IsDead && graphicState is { MaximumHealth: 0 } deadGraphic)
                {
                    selectedGraphic = deadGraphic;
                }
                else if (graphicState is { MinimumHealth: > 0 } aliveGraphic)
                {
                    selectedGraphic = aliveGraphic;
                }

                _renderBoundsSrc = selectedGraphic is null
                    ? default
                    : new (
                        selectedGraphic.X * _tileWidth,
                        selectedGraphic.Y * _tileHeight,
                        (selectedGraphic.Width + 1) * _tileWidth,
                        (selectedGraphic.Height + 1) * _tileHeight
                    );
                break;

            case ResourceTextureSource.Animation:
                _renderBoundsSrc = default;
                break;

            default:
                return;
        }

        _renderBoundsDest.Width = _renderBoundsSrc.Width;
        _renderBoundsDest.Height = _renderBoundsSrc.Height;
        _renderBoundsDest.Y = (int) (map.Y + Y * _tileHeight + OffsetY);
        _renderBoundsDest.X = (int) (map.X + X * _tileWidth + OffsetX);

        if (_renderBoundsSrc.Height > _tileHeight)
        {
            _renderBoundsDest.Y -= _renderBoundsSrc.Height - _tileHeight;
        }

        if (_renderBoundsSrc.Width > _tileWidth)
        {
            _renderBoundsDest.X -= (_renderBoundsSrc.Width - _tileWidth) / 2;
        }

        _recalculateRenderBounds = false;
    }

    protected override (int X, int Y) GetHpBarPosition(IGameTexture boundingTexture)
    {
        if (_renderBoundsDest.Width <= 0 ||
            _renderBoundsDest.Height <= 0 ||
            Texture == null)
        {
            return base.GetHpBarPosition(boundingTexture);
        }

        // Anchor the bar to the first visible (non-transparent) pixel row,
        // not to the top edge of the PNG/tileset rectangle. This keeps
        // transparent padding around large resource sprites from pushing the
        // HP bar too far away from the actual tree/rock/etc.
        var visibleTopOffset = GetFirstOpaqueRow(Texture, _renderBoundsSrc);

        var x = (int)Math.Round(_renderBoundsDest.X + _renderBoundsDest.Width / 2f);
        var visibleTopY = _renderBoundsDest.Y + visibleTopOffset;
        var y = (int)Math.Round(visibleTopY - boundingTexture.Height / 2f - 6f);
        return (x, y);
    }

    private static int GetFirstOpaqueRow(IGameTexture texture, FloatRect source)
    {
        var sourceX = Math.Clamp((int)Math.Floor(source.X), 0, Math.Max(0, texture.Width - 1));
        var sourceY = Math.Clamp((int)Math.Floor(source.Y), 0, Math.Max(0, texture.Height - 1));
        var sourceWidth = Math.Clamp((int)Math.Ceiling(source.Width), 0, texture.Width - sourceX);
        var sourceHeight = Math.Clamp((int)Math.Ceiling(source.Height), 0, texture.Height - sourceY);

        if (sourceWidth <= 0 || sourceHeight <= 0)
            return 0;

        var key = new OpaqueTopCacheKey(
            texture.Name,
            sourceX,
            sourceY,
            sourceWidth,
            sourceHeight
        );

        return OpaqueTopCache.GetOrAdd(
            key,
            _ =>
            {
                for (var localY = 0; localY < sourceHeight; ++localY)
                {
                    var pixelY = sourceY + localY;
                    for (var localX = 0; localX < sourceWidth; ++localX)
                    {
                        var pixel = texture.GetPixel(sourceX + localX, pixelY);
                        if (pixel.A > 0)
                            return localY;
                    }
                }

                return 0;
            }
        );
    }

    public override void DrawHpBar()
    {
        base.DrawHpBar();
        DrawTreeHarvestProgress();
    }

    private void DrawTreeHarvestProgress()
    {
        if (!ShouldDrawHpBar ||
            IsDead ||
            Descriptor == null ||
            !IsTreeResource(Descriptor) ||
            Graphics.Renderer == null)
        {
            return;
        }

        var maxHealth = MaxVital[(int)Enums.Vital.Health];
        var currentHealth = Vital[(int)Enums.Vital.Health];
        if (maxHealth <= 0 || currentHealth <= 0 || currentHealth >= maxHealth)
            return;

        // Progress represents how close the tree is to being felled:
        // 0% at full health, 100% when the final hit lands.
        var progress = 1d - Math.Clamp(currentHealth / (double)maxHealth, 0d, 1d);
        var percentage = (int)Math.Clamp(
            Math.Round(progress * 100d, MidpointRounding.AwayFromZero),
            1d,
            99d
        );
        var text = $"{percentage}%";

        var barTexture = GetBoundingHpBarTexture();
        var (x, barY) = GetHpBarPosition(barTexture);
        var textSize = Graphics.Renderer.MeasureText(
            text,
            Graphics.EntityNameFont,
            Graphics.EntityNameFontSize,
            1
        );

        var textX = x - (int)Math.Ceiling(textSize.X / 2f);
        var textY = barY - barTexture.Height / 2 - (int)Math.Ceiling(textSize.Y) - 4;

        Graphics.Renderer.DrawString(
            text,
            Graphics.EntityNameFont,
            Graphics.EntityNameFontSize,
            textX,
            textY,
            1,
            Color.White,
            true,
            null,
            Color.Black
        );
    }

    private static bool IsTreeResource(ResourceDescriptor descriptor)
    {
        var resourceName = descriptor.Name ?? string.Empty;
        if (resourceName.Contains("tree", StringComparison.OrdinalIgnoreCase) ||
            resourceName.Contains("arbre", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var profession = ProfessionConfiguration.Instance.FindResource(descriptor.Id)?.Profession;
        if (profession != null)
        {
            var professionName = profession.Name ?? string.Empty;
            if (professionName.Contains("woodcut", StringComparison.OrdinalIgnoreCase) ||
                professionName.Contains("lumber", StringComparison.OrdinalIgnoreCase) ||
                professionName.Contains("bûcher", StringComparison.OrdinalIgnoreCase) ||
                professionName.Contains("bucher", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var toolIndex = descriptor.Tool;
        if (toolIndex >= 0 && toolIndex < Options.Instance.Equipment.ToolTypes.Count)
        {
            var toolName = Options.Instance.Equipment.ToolTypes[toolIndex] ?? string.Empty;
            if (toolName.Contains("axe", StringComparison.OrdinalIgnoreCase) ||
                toolName.Contains("hache", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    //Rendering Resources
    public override void Draw()
    {
        if (MapInstance == null)
        {
            return;
        }

        if (Texture == null)
        {
            return;
        }

        // TODO: Add an option to show the exhausted sprite until the exhaustion animation is finished, but this is not necessary if the graphics line up like Blinkuz' sample provided to fix #2572
        if (_activeAnimation != null)
        {
            return;
        }

        Graphics.DrawGameTexture(Texture, _renderBoundsSrc, _renderBoundsDest, Color.White);
    }
}
