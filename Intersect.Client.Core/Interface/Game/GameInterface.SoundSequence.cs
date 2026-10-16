using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private readonly SoundSequencePlayer _soundSequence = new();
    public void PlaySoundSequence(PlaySoundSequencePacket packet) => _soundSequence.Play(packet.First, packet.Second);
    private void UpdateSoundSequence() => _soundSequence.Update();
    private void DisposeSoundSequence() => _soundSequence.Stop();
}
