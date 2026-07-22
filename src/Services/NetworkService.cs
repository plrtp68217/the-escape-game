using Godot;

namespace EscapeGame.Services;

/// <summary>
/// Адаптер <see cref="MultiplayerApi"/u003e под <see cref="INetworkService"/u003e.
/// </summary>
public class NetworkService : INetworkService
{
	private readonly SceneTree _tree;

	public NetworkService(SceneTree tree)
	{
		_tree = tree;
	}

	public bool IsServer => HasPeer && _tree.GetMultiplayer().IsServer();

	public bool HasPeer => _tree.GetMultiplayer().MultiplayerPeer != null;

	public int LocalPeerId => _tree.GetMultiplayer().GetUniqueId();

	public void Rpc(string method, params Variant[] args)
	{
		var multiplayer = _tree.GetMultiplayer();
		var array = new Godot.Collections.Array(args);
		multiplayer.Rpc(multiplayer.GetRemoteSenderId(), null, method, array);
	}

	public void RpcId(long peerId, string method, params Variant[] args)
	{
		var array = new Godot.Collections.Array(args);
		_tree.GetMultiplayer().Rpc((int)peerId, _tree.Root, method, array);
	}
}
