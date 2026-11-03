namespace Intersect.Editor.Networking;

// Only the native form is linked into this offline layout test; never send an editor packet.
internal static class Network
{
    internal static object? LastPacket;
    public static void SendPacket(object packet) => LastPacket = packet;
}
