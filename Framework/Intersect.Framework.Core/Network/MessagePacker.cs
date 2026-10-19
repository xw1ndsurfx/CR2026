using Intersect.Core;
using MessagePack;
using Intersect.Memory;
using Microsoft.Extensions.Logging;

namespace Intersect.Network;

public enum PacketDecodeFailure { None, ProtocolMismatch, Malformed }

public partial class MessagePacker
{
    public static readonly MessagePacker Instance = new();

    private readonly MessagePackSerializerOptions mOptions  = MessagePackSerializerOptions.Standard.
        WithResolver(MessagePack.Resolvers.CompositeResolver.Create(
                MessagePack.Resolvers.NativeGuidResolver.Instance,
            MessagePack.Resolvers.NativeDateTimeResolver.Instance,
            MessagePack.Resolvers.NativeDecimalResolver.Instance,
            MessagePack.Resolvers.StandardResolver.Instance
            )
        ).WithSecurity(MessagePackSecurity.UntrustedData);

    private readonly MessagePackSerializerOptions mCompressedOptions = MessagePackSerializerOptions.Standard
        .WithCompression(MessagePackCompression.Lz4Block).WithSecurity(MessagePackSecurity.UntrustedData);

    public byte[] Serialize(IntersectPacket packet)
    {
        var packedPacket = new PackedIntersectPacket(packet)
        {
            Data = MessagePackSerializer.Serialize((object)packet, mOptions)
        };

        return MessagePackSerializer.Serialize((object)packedPacket, mCompressedOptions);
    }

    public object? Deserialize(byte[] packetData, bool silent = false) => Deserialize(packetData, out _, silent);

    public object? Deserialize(byte[] packetData, out PacketDecodeFailure failure, bool silent = false)
    {
        failure = PacketDecodeFailure.Malformed;
        try
        {
            var packedPacket = MessagePackSerializer.Deserialize<PackedIntersectPacket>(packetData, mCompressedOptions);
            if (packedPacket == null || packedPacket.Data == null) return null;
            // Numeric IDs shift whenever packet types are added. Never deserialize a foreign
            // ordinal as a local packet type, including status requests before connection approval.
            if (PackedIntersectPacket.CurrentProtocolFingerprint.Length == 0 ||
                !packedPacket.ProtocolFingerprint.AsSpan().SequenceEqual(PackedIntersectPacket.CurrentProtocolFingerprint))
            {
                failure = PacketDecodeFailure.ProtocolMismatch;
                if (!silent) ApplicationContext.Context.Value?.Logger.LogDebug(
                    "Rejected incompatible packet protocol. Update server, client and editor from the same build.");
                return null;
            }
            if (!PackedIntersectPacket.KnownKeys.TryGetValue(packedPacket.Key, out var packetType)) return null;
            var intersectPacket = MessagePackSerializer.Deserialize(packetType, packedPacket.Data, mOptions);
            failure = PacketDecodeFailure.None;
            return intersectPacket;
        }
        catch (Exception exception)
        {
#if DIAGNOSTIC
            Logger?.Debug($"Invalid packet: {Convert.ToHexString(data)}");
#endif

            if (!silent)
            {
                ApplicationContext.Context.Value?.Logger.LogError(exception, "Failed to deserialize packet data");
            }

            return null;
        }
    }

    public TPacket? Deserialize<TPacket>(byte[] packetData, bool silent = false) where TPacket : IntersectPacket
    {
        return Deserialize(packetData, silent) as TPacket;
    }

    public object Deserialize(IBuffer buffer) => Deserialize(buffer.ReadBytes(buffer.Remaining));

    public object? Deserialize(IBuffer buffer, out PacketDecodeFailure failure) =>
        Deserialize(buffer.ReadBytes(buffer.Remaining), out failure);

}
