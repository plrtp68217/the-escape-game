using Godot;

namespace EscapeGame.Services;

/// <summary>
/// Абстракция над сетевым пиром Godot. Скрывает прямую работу с
/// <see cref="MultiplayerApi"/u003e и позволяет подменять сеть в тестах.
/// </summary>
public interface INetworkService
{
	bool IsServer { get; }
	bool HasPeer { get; }
	int LocalPeerId { get; }

	void Rpc(string method, params Variant[] args);
	void RpcId(long peerId, string method, params Variant[] args);
}
