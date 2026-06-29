using System;

namespace MultiplayerChess;

public sealed class PieceSFX : Component, Component.ICollisionListener
{
	[Property] public SoundEvent PlaceSound { get; set; }
	[Property, Range( 0f, 1f )] public float Volume { get; set; } = 1f;

	/// Only play when colliding with an object that has this tag.</summary>
	[Property] public string BoardTag { get; set; } = "board";

	/// Ignore soft touches/drags below this impact speed (units/s).
	[Property] public float MinImpactSpeed { get; set; } = 10f;

	/// Minimum time between plays so a bouncing/settling piece doesn't rattle.
	[Property] public float Cooldown { get; set; } = 0.1f;

	private TimeSince _timeSinceLastPlay;

	public void OnCollisionStart( Collision collision )
	{
		if ( PlaceSound is null )
			return;

		// Only react to the board, not other pieces or the floor.
		if ( !string.IsNullOrEmpty( BoardTag ) && !collision.Other.GameObject.Tags.Has( BoardTag ) )
			return;

		// Skip gentle grazes while dragging; only fire on a real landing impact.
		if ( MathF.Abs( collision.Contact.NormalSpeed ) < MinImpactSpeed )
			return;

		if ( _timeSinceLastPlay < Cooldown )
			return;

		_timeSinceLastPlay = 0f;
		PlayPlace();
	}

	[Rpc.Broadcast]
	public void PlayPlace()
	{
		if ( PlaceSound is null )
			return;

		var handle = Sound.Play( PlaceSound, WorldPosition );

		if ( handle is not null )
			handle.Volume = Volume;
	}
}
