using System.Threading.Tasks;
namespace Sandbox;

/// <summary>
/// Creates a networked game lobby and assigns player prefabs to connected clients.
/// </summary>
[Title( "Unique Network Helper" )]
[Category( "Networking" )]
[Icon( "electrical_services" )]
public sealed class UniqueNetworkHelper : Component, Component.INetworkListener
{
	/// <summary>
	/// Create a server (if we're not joining one)
	/// </summary>
	[Property] public bool StartServer { get; set; } = true;

	/// <summary>
	/// The prefab to spawn for the player to control.
	/// </summary>
	[Property] public GameObject PlayerPrefab { get; set; }

	/// <summary>
	/// A list of points to choose from randomly to spawn the player in. If not set, we'll spawn at the
	/// location of the NetworkHelper object.
	/// </summary>
	[Property] public List<GameObject> SpawnPoints { get; set; }

	// Host-side: which seat (spawn-point index) each connected player holds.
	private readonly Dictionary<Connection, int> _seats = new();

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;

		if ( StartServer && !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() { MaxPlayers = 2 } );
		}
	}

	/// <summary>
	/// A client is fully connected to the server. This is called on the host.
	/// </summary>
	public void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.Name}' has joined the game" );

		if ( !PlayerPrefab.IsValid() )
			return;

		//
		// Claim a seat (spawn-point index), filled in order.
		//
		int seat = ClaimSeat( channel );
		if ( seat < 0 )
		{
			Log.Warning( $"No free seat for '{channel.Name}' — the match is full" );
			return;
		}

		var startLocation = GetSeatTransform( seat ).WithScale( 1 );

		// Spawn the player (rotated to the spawn point) and give the client ownership.
		// The view direction is set client-side by SpawnFacing, because EyeAngles is
		// owned by the controlling client, not the host.
		var player = PlayerPrefab.Clone( startLocation, name: $"Player - {channel.Name}" );
		player.NetworkSpawn( channel );
	}

	/// <summary>
	/// A client has left. Free their seat so a reconnecting player can reuse it.
	/// </summary>
	public void OnDisconnected( Connection channel )
	{
		if ( _seats.Remove( channel, out var seat ) )
			Log.Info( $"Player '{channel.Name}' left, freeing seat {seat}" );
	}

	/// <summary>
	/// Claim the first unoccupied spawn-point index, or -1 if the match is full.
	/// </summary>
	int ClaimSeat( Connection channel )
	{
		int count = SpawnPoints?.Count ?? 0;
		for ( int i = 0; i < count; i++ )
		{
			if ( SpawnPoints[i] is null || _seats.ContainsValue( i ) )
				continue;

			_seats[channel] = i;
			return i;
		}

		return -1;
	}

	Transform GetSeatTransform( int seat )
	{
		if ( SpawnPoints is not null && seat >= 0 && seat < SpawnPoints.Count && SpawnPoints[seat] is not null )
			return SpawnPoints[seat].WorldTransform;

		return WorldTransform;
	}
}
