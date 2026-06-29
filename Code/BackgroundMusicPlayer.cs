namespace MultiplayerChess;

public sealed class BackgroundMusicPlayer : Component
{
	[Property] public SoundEvent Track { get; set; }
	[Property, Range( 0f, 1f )] public float Volume { get; set; } = 0.5f;
	[Property] public bool Loop { get; set; } = true;

	private SoundHandle _handle;

	protected override void OnStart()
	{
		Play();
	}

	protected override void OnUpdate()
	{
		// restart once the track finishes.
		if ( Loop && _handle is not null && _handle.Finished )
			Play();
	}

	protected override void OnEnabled()
	{
		// Resume if re-enabled at runtime after being disabled.
		if ( Game.IsPlaying && _handle is null )
			Play();
	}

	protected override void OnDisabled()
	{
		Stop();
	}

	protected override void OnDestroy()
	{
		Stop();
	}

	private void Play()
	{
		if ( Track is null )
			return;

		_handle?.Stop();
		_handle = Sound.Play( Track );

		if ( _handle is not null )
			_handle.Volume = Volume;
	}

	private void Stop()
	{
		_handle?.Stop();
		_handle = null;
	}
}
