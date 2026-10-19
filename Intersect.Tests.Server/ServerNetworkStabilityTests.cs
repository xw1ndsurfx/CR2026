using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using Intersect.Network;
using Intersect.Network.LiteNetLib;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Client;
using LiteNetLib.Utils;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public class ServerNetworkStabilityTests
{
    [OneTimeSetUp]
    public void RegisterPackets()
    {
        Options.EnsureCreated();
        var registry = new PacketTypeRegistry(NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(), Is.True);
        PackedIntersectPacket.AddKnownTypes(registry.Types);
    }

    private static ISymmetricAlgorithm Cipher(bool gcm) => gcm ? new AesGcmAlgorithm() : new AesAlgorithm();

    [TestCase(true), TestCase(false)]
    public void EveryTruncatedEncryptedPacketIsRejectedWithoutThrowing(bool gcm)
    {
        using var algorithm = Cipher(gcm);
        var original = new byte[51];
        RandomNumberGenerator.Fill(original);
        Assert.That(algorithm.TryEncrypt(original, out var encrypted), Is.EqualTo(EncryptionResult.Success));
        var packet = encrypted.ToArray();
        for (var length = 0; length < packet.Length; length++)
            Assert.That(algorithm.TryDecrypt(packet.AsSpan(0, length), out _), Is.Not.EqualTo(EncryptionResult.Success), $"prefix {length}");
        Assert.That(algorithm.TryDecrypt(packet, out var decrypted), Is.EqualTo(EncryptionResult.Success));
        Assert.That(decrypted.ToArray(), Is.EqualTo(original));
    }

    [TestCase(true), TestCase(false)]
    public void ForgedLengthsAndInvalidRangesDoNotAllocateOrThrow(bool gcm)
    {
        using var algorithm = Cipher(gcm);
        Assert.That(algorithm.TryEncrypt(new byte[32], out var encrypted), Is.EqualTo(EncryptionResult.Success));
        var packet = encrypted.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(1), int.MaxValue);
        Assert.That(algorithm.TryDecrypt(packet, out _), Is.EqualTo(EncryptionResult.SizeMismatch));
        foreach (var (offset, length) in new[] { (-1, 1), (0, -1), (int.MaxValue, 5), (1, int.MaxValue) })
        {
            Assert.That(algorithm.TryDecrypt(packet, offset, length, out _), Is.EqualTo(EncryptionResult.SizeMismatch));
            Assert.That(algorithm.TryEncrypt(packet, offset, length, out _), Is.EqualTo(EncryptionResult.SizeMismatch));
        }
        Assert.That(algorithm.SetKey(new byte[1]), Is.False);
    }

    [Test]
    public void AuthenticationFailureReturnsCipherToPoolAndNeverExposesPlaintext()
    {
        using var algorithm = new AesGcmAlgorithm();
        Assert.That(algorithm.TryEncrypt(new byte[] { 1, 2, 3 }, out var encrypted), Is.EqualTo(EncryptionResult.Success));
        var original = encrypted.ToArray();
        var damaged = original.ToArray();
        damaged[7 + damaged[5]] ^= 0x80;
        for (var i = 0; i < 200; i++)
        {
            Assert.That(algorithm.TryDecrypt(damaged, out var plaintext), Is.EqualTo(EncryptionResult.Error));
            Assert.That(plaintext.Length, Is.Zero);
        }
        var instances = (HashSet<AesGcm>)typeof(AesGcmAlgorithm).GetField("_algorithmInstances", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(algorithm)!;
        Assert.That(instances, Has.Count.EqualTo(1), "Failed decryptions must not leak a native cipher instance per packet.");
        Assert.That(algorithm.TryDecrypt(original, out var recovered), Is.EqualTo(EncryptionResult.Success));
        Assert.That(recovered.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
        algorithm.Dispose();
        algorithm.Dispose();
    }

    [TestCase(true), TestCase(false)]
    public void RandomMalformedEncryptedPacketsNeverEscapeTheTryApi(bool gcm)
    {
        using var algorithm = Cipher(gcm);
        var random = new Random(5731);
        for (var i = 0; i < 1000; i++)
        {
            var packet = new byte[random.Next(0, 128)]; random.NextBytes(packet);
            if (packet.Length > 0) packet[0] = gcm ? (byte)1 : (byte)0;
            var result = algorithm.TryDecrypt(packet, out var plaintext);
            Assert.That(result, Is.Not.EqualTo(EncryptionResult.Success));
            Assert.That(plaintext.Length, Is.Zero);
        }
    }

    [TestCase(-1), TestCase(0), TestCase(int.MaxValue), TestCase(400)]
    public void NetworkFrameRejectsInvalidLengthBeforeAllocation(int length)
    {
        var writer = new NetDataWriter(); writer.Put(length); writer.Put(new byte[] { 1, 2, 3 });
        var reader = new NetDataReader(); reader.SetSource(writer);
        object?[] arguments = [reader, null];
        var method = typeof(LiteNetLibConnection).GetMethod("TryReadCipherData", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.That(method.Invoke(null, arguments), Is.EqualTo(false));
        Assert.That((byte[])arguments[1]!, Is.Empty);
    }

    [Test]
    public void NetworkFrameReadsValidBytesAndRejectsMissingLength()
    {
        var method = typeof(LiteNetLibConnection).GetMethod("TryReadCipherData", BindingFlags.NonPublic | BindingFlags.Static)!;
        var writer = new NetDataWriter(); writer.Put(3); writer.Put(new byte[] { 1, 2, 3 });
        var reader = new NetDataReader(); reader.SetSource(writer);
        object?[] arguments = [reader, null];
        Assert.That(method.Invoke(null, arguments), Is.EqualTo(true));
        Assert.That(arguments[1], Is.EqualTo(new byte[] { 1, 2, 3 }));
        reader.SetSource(new byte[] { 1, 2 });
        Assert.That(method.Invoke(null, arguments), Is.EqualTo(false));
    }

    [Test]
    public void MatchingPacketEnvelopeRoundTripsThroughTheProductionPacker()
    {
        var original = new MovePacket(Guid.NewGuid(), 4, 5, Intersect.Enums.Direction.Down);
        var decoded = MessagePacker.Instance.Deserialize(MessagePacker.Instance.Serialize(original), out var failure);
        Assert.That(failure, Is.EqualTo(PacketDecodeFailure.None));
        Assert.That(decoded, Is.TypeOf<MovePacket>());
        Assert.That(((MovePacket)decoded!).MapId, Is.EqualTo(original.MapId));
    }

    [Test]
    public void MatchingEncryptedHailSurvivesTheNewCompatibilityEnvelope()
    {
        using var serverKey = RSA.Create(4096);
        using var clientKey = RSA.Create(2048);
        var secret = RandomNumberGenerator.GetBytes(32);
        var hail = new HailPacket(serverKey, secret, SharedConstants.VersionData, clientKey.ExportParameters(false));
        Assert.That(hail.Encrypt(), Is.True);
        var decoded = MessagePacker.Instance.Deserialize<HailPacket>(MessagePacker.Instance.Serialize(hail));
        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded!.Decrypt(serverKey), Is.True);
        Assert.That(decoded.HandshakeSecret, Is.EqualTo(secret));
        Assert.That(decoded.VersionData, Is.EqualTo(SharedConstants.VersionData));
    }

    [Test]
    public void FailedDecryptionNeverRunsSpeculativeDebugEncryption()
    {
        var connection = (LiteNetLibConnection)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(LiteNetLibConnection));
        typeof(LiteNetLibConnection).GetField("_symmetric", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(connection, new RejectingCipher());
        var writer = new NetDataWriter(); writer.Put(20); writer.Put(new byte[20]);
        var reader = (global::LiteNetLib.NetPacketReader)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(global::LiteNetLib.NetPacketReader));
        reader.SetSource(writer);
        object?[] arguments = [null, reader, (byte)0, global::LiteNetLib.DeliveryMethod.ReliableOrdered, null];
        Assert.That(typeof(LiteNetLibConnection).GetMethod("TryProcessInboundMessage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(connection, arguments), Is.EqualTo(false));
        Assert.That(arguments[4], Is.Null);
    }

    private sealed class RejectingCipher : ISymmetricAlgorithm
    {
        public void Dispose() { }
        public bool SetKey(ReadOnlySpan<byte> key) => false;
        public EncryptionResult TryDecrypt(ReadOnlySpan<byte> data, out ReadOnlySpan<byte> plain) { plain = default; return EncryptionResult.Error; }
        public EncryptionResult TryDecrypt(ReadOnlySpan<byte> data, int offset, int length, out ReadOnlySpan<byte> plain) => TryDecrypt(data, out plain);
        public EncryptionResult TryEncrypt(ReadOnlySpan<byte> data, out ReadOnlySpan<byte> cipher) => throw new NotSupportedException();
        public EncryptionResult TryEncrypt(ReadOnlySpan<byte> data, int offset, int length, out ReadOnlySpan<byte> cipher) => throw new NotSupportedException();
#if DEBUG
        public EncryptionResult TryEncrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> nonce, out ReadOnlySpan<byte> cipher) => throw new NotSupportedException();
#endif
    }

    [Test]
    public void ForeignAndLegacyTablesAreRejectedBeforeDeserializingWrongPacketType()
    {
        var compressed = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
        var envelope = new PackedIntersectPacket(new MovePacket())
        {
            // This has the bin field that previously got read as a TranslationSettings/WorldMap string.
            Data = MessagePackSerializer.Serialize(new HailPacket { EncryptedData = new byte[300] }),
            ProtocolFingerprint = new byte[32],
        };
        Assert.That(MessagePacker.Instance.Deserialize(MessagePackSerializer.Serialize(envelope, compressed), out var failure, silent: true), Is.Null);
        Assert.That(failure, Is.EqualTo(PacketDecodeFailure.ProtocolMismatch));
        var legacy = MessagePackSerializer.Serialize(new object[] { envelope.Key, envelope.Data }, compressed);
        Assert.That(MessagePacker.Instance.Deserialize(legacy, out failure, silent: true), Is.Null);
        Assert.That(failure, Is.EqualTo(PacketDecodeFailure.ProtocolMismatch));
    }

    [Test]
    public void UnknownKeysAndMalformedPayloadsRemainContained()
    {
        var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
        var envelope = new PackedIntersectPacket(new MovePacket()) { Key = ushort.MaxValue, Data = [1] };
        Assert.That(MessagePacker.Instance.Deserialize(MessagePackSerializer.Serialize(envelope, options), out var failure, silent: true), Is.Null);
        Assert.That(failure, Is.EqualTo(PacketDecodeFailure.Malformed));
        envelope.Key = PackedIntersectPacket.KnownTypes[typeof(MovePacket)];
        Assert.That(MessagePacker.Instance.Deserialize(MessagePackSerializer.Serialize(envelope, options), out failure, silent: true), Is.Null);
        Assert.That(failure, Is.EqualTo(PacketDecodeFailure.Malformed));
    }

    [Test]
    public void FingerprintIsIndependentOfEnumerationOrderAndCurrentCulture()
    {
        var pairs = PackedIntersectPacket.KnownKeys.ToArray();
        var expected = PacketProtocolFingerprint.Compute(pairs);
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ar-SA");
            Assert.That(PacketProtocolFingerprint.Compute(pairs.AsEnumerable().Reverse()), Is.EqualTo(expected));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        pairs[0] = new((ushort)60000, pairs[0].Value);
        Assert.That(PacketProtocolFingerprint.Compute(pairs), Is.Not.EqualTo(expected));
    }

    [Test]
    public void FingerprintDetectsChangedContractsWithIdenticalTypeNames()
    {
        Type Contract(int key, Type value)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("WireTest" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("Test").DefineType("WireTest.SameName", TypeAttributes.Public);
            type.SetCustomAttribute(new CustomAttributeBuilder(typeof(MessagePackObjectAttribute).GetConstructor([typeof(bool)])!, [false]));
            type.DefineField("Value", value, FieldAttributes.Public).SetCustomAttribute(new CustomAttributeBuilder(typeof(KeyAttribute).GetConstructor([typeof(int)])!, [key]));
            return type.CreateType()!;
        }
        byte[] Hash(Type type) => PacketProtocolFingerprint.Compute([new(0, type)]);
        Assert.That(Hash(Contract(0, typeof(int))), Is.EqualTo(Hash(Contract(0, typeof(int)))));
        Assert.That(Hash(Contract(0, typeof(int))), Is.Not.EqualTo(Hash(Contract(1, typeof(int)))));
        Assert.That(Hash(Contract(0, typeof(int))), Is.Not.EqualTo(Hash(Contract(0, typeof(string)))));
    }
}
