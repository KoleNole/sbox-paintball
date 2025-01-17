﻿using Sandbox;

namespace Paintball;

[Hammer.Skip]
[Library( "paintball", Title = "PaintBall" )]
public partial class Game : Sandbox.Game
{
	public new static Game Current => Sandbox.Game.Current as Game;
	[Net, Change] public BaseState State { get; private set; }
	public Settings Settings { get; set; }
	private BaseState _lastState { get; set; }

	public Game()
	{
		if ( IsServer )
		{
			new UI.Hud
			{
				Parent = this
			};
		}
	}

	public override void Simulate( Client cl )
	{
		State?.Tick();

		base.Simulate( cl );
	}

	public void ChangeState( BaseState state )
	{
		Assert.NotNull( state );

		var oldState = State;

		State?.Finish();
		State = state;
		State?.Start();

		Event.Run( PBEvent.Game.StateChanged, oldState, state );
	}

	public override bool CanHearPlayerVoice( Client source, Client dest )
	{
		return true;
	}

	public override void ClientJoined( Client client )
	{
		var player = new Player();

		client.Pawn = player;

		base.ClientJoined( client );

		State?.OnPlayerJoin( player );
		Event.Run( PBEvent.Client.Joined, client );
		RPC.ClientJoined( To.Everyone, client );
	}

	public override void ClientDisconnect( Client client, NetworkDisconnectionReason reason )
	{
		State?.OnPlayerLeave( client.Pawn as Player );
		Event.Run( PBEvent.Client.Disconnected, client.PlayerId, reason );
		RPC.ClientDisconnected( To.Everyone, client.PlayerId, reason );

		base.ClientDisconnect( client, reason );
	}

	public override void MoveToSpawnpoint( Entity pawn )
	{
		if ( pawn is Player player )
		{
			Team team = player.Team;

			if ( player.Team == Team.None )
				team = (Team)Rand.Int( 1, 2 );

			var spawnpoints = team == Team.Blue ? Map.BlueSpawnPoints : Map.RedSpawnPoints;

			if ( spawnpoints.Count > 0 )
			{
				var spawnpoint = spawnpoints[Rand.Int( 0, spawnpoints.Count - 1 )];
				pawn.Transform = spawnpoint.Transform;

				return;
			}

			Log.Warning( $"Couldn't find team spawnpoint for {player}!" );
		}

		base.MoveToSpawnpoint( pawn );
	}

	public override void PostLevelLoaded()
	{
		base.PostLevelLoaded();

		ChangeState( new WaitingForPlayersState() );

		if ( !Settings.IsValid() )
			Settings = new Settings();
	}

	public override void Shutdown()
	{
		base.Shutdown();

		State = null;
		_lastState = null;
	}

	public override void DoPlayerDevCam( Client player )
	{
		if ( player.PlayerId != 76561198087434609 )
			return;

		base.DoPlayerDevCam( player );
	}

	public override void DoPlayerNoclip( Client player )
	{
		if ( player.PlayerId != 76561198087434609 )
			return;

		base.DoPlayerNoclip( player );
	}

	public override void DoPlayerSuicide( Client cl )
	{
		if ( State?.CanPlayerSuicide == false )
			return;

		base.DoPlayerSuicide( cl );
	}

	private void OnStateChanged()
	{
		if ( _lastState == State )
			return;

		var oldState = _lastState;

		_lastState?.Finish();
		_lastState = State;
		_lastState.Start();

		Event.Run( PBEvent.Game.StateChanged, oldState, _lastState );
	}
}
