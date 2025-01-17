﻿using Sandbox;
using System;
using System.Linq;

namespace Paintball;

public enum RoundState : byte
{
	None,
	/// <summary>
	/// Players aren't able to move and can buy weapons.
	/// </summary>
	Freeze,
	/// <summary>
	/// Players are able to move freely and do objectives.
	/// </summary>
	Play,
	/// <summary>
	/// The bomb has been planted and it can explode or be defused.
	/// </summary>
	Bomb,
	/// <summary>
	/// Rest period between rounds.
	/// </summary>
	End
}

/// <summary>
/// Bomb defusal or Team Deathmatch.
/// </summary>
public partial class GameplayState : BaseState
{
	[ConVar.Replicated( "pb_freeze_duration", Help = "The duration of the freeze period." )]
	public static int FreezeDuration { get; set; } = 5;
	[ConVar.Replicated( "pb_play_duration", Help = "The duration of the play period." )]
	public static int PlayDuration { get; set; } = 60;
	[ConVar.Replicated( "pb_end_duration", Help = "The duration of the end period." )]
	public static int EndDuration { get; set; } = 5;
	[ConVar.Replicated( "pb_bomb_duration", Help = "The time needed for the bomb to explode." )]
	public static int BombDuration { get; set; } = 30;
	[ConVar.Replicated( "pb_bomb_enabled" )]
	public static bool BombEnabled { get; set; } = true;
	[ConVar.Replicated( "pb_buy_duration", Help = "The duration of the buy period." )]
	public static int BuyDuration { get; set; } = 15;
	[ConVar.Replicated( "pb_round_limit", Help = "The amount of rounds." )]
	public static int RoundLimit { get; set; } = 12;

	[Net] public TimeUntil BuyTimeExpire { get; private set; } = 0;
	public PlantedBomb Bomb { get; set; }
	public override bool CanBuy => !BuyTimeExpire;
	public int Round { get; private set; } = 1;
	public RoundState RoundState { get; set; }
	public int ToWinScore => (RoundLimit >> 1) + 1;
	public override bool UpdateTimer => RoundState != RoundState.End;
	private bool _firstBlood = false;

	public GameplayState() : base() { }

	public GameplayState( bool bombEnabled ) : base() => BombEnabled = bombEnabled;

	public override void OnPlayerJoin( Player player )
	{
		base.OnPlayerJoin( player );

		if ( RoundState == RoundState.Play || RoundState == RoundState.Bomb )
			if ( Client.All.Count == 2 )
				RoundStateFinish();
	}

	public override void OnPlayerLeave( Player player )
	{
		base.OnPlayerLeave( player );

		player.Inventory.DropBomb();

		CheckRoundOver();
	}

	public override void OnPlayerSpawned( Player player )
	{
		AdjustTeam( player.Team, 1 );

		if ( player.Inventory.HasFreeSlot( SlotType.Secondary ) )
			player.Inventory.Add( new Pistol() );
		if ( player.Inventory.HasFreeSlot( SlotType.Melee ) )
			player.Inventory.Add( new Knife() );
	}

	public override void OnPlayerKilled( Player player )
	{
		base.OnPlayerKilled( player );

		if ( !_firstBlood && player.LastAttacker is Player )
		{
			Audio.AnnounceAll( "first_blood", Audio.Priority.Medium );
			_firstBlood = true;
		}

		player.MakeSpectator();
		CheckRoundOver();
	}

	public override void OnPlayerChangedTeam( Player player, Team oldTeam )
	{
		Host.AssertServer();

		if ( !player.Alive() )
			return;

		AdjustTeam( oldTeam, -1 );
		AdjustTeam( player.Team, 1 );
		player.TakeDamage( DamageInfo.Generic( float.MaxValue ) );
	}

	public override void Tick()
	{
		if ( Bomb.IsValid() )
			Bomb.Tick();

		if ( UntilStateEnds )
			TimeUp();
	}

	public override void TimeUp()
	{
		base.TimeUp();

		if ( Host.IsServer )
			RoundStateFinish();
	}

	public override void Start()
	{
		base.Start();

		if ( !Host.IsServer )
			return;

		RoundState = RoundState.Freeze;

		foreach ( var client in Client.All )
		{
			var player = client.Pawn as Player;
			player.Reset();
		}

		RoundStateStart();
	}

	public void RoundStateStart()
	{
		switch ( RoundState )
		{
			case RoundState.Freeze:
			{
				Map.CleanUp();

				if ( Host.IsClient )
				{
					if ( BlueScore == ToWinScore - 1 || RedScore == ToWinScore - 1 )
						UI.Notification.Create( "Matchpoint!", FreezeDuration, true );

					return;
				}

				Bomb = null;
				_firstBlood = false;

				TeamBalance();

				Map.BlueSpawnPoints.Shuffle();
				Map.RedSpawnPoints.Shuffle();
				int bluei = 0, redi = 0;
				int index = BombEnabled ? Rand.Int( 1, Team.Red.GetCount() ) : int.MaxValue;
				foreach ( var client in Client.All )
				{
					var player = client.Pawn as Player;

					if ( !player.IsValid() || player.Team == Team.None )
						continue;

					player.Respawn();

					if ( Map.BlueSpawnPoints.Count == 0 || Map.RedSpawnPoints.Count == 0 )
					{
						Game.Current.MoveToSpawnpoint( player );
						continue;
					}

					if ( player.Team == Team.Blue )
					{
						if ( bluei >= Map.BlueSpawnPoints.Count )
							bluei = 0;

						player.Transform = Map.BlueSpawnPoints[bluei++].Transform;
					}
					else if ( player.Team == Team.Red )
					{
						if ( redi >= Map.RedSpawnPoints.Count )
							redi = 0;

						player.Transform = Map.RedSpawnPoints[redi++].Transform;

						if ( --index == 0 )
							player.Inventory.Add( new Bomb() );
					}
				}
				UntilStateEnds = FreezeDuration;
				BuyTimeExpire = BuyDuration;

				Event.Run( PBEvent.Round.New );
				RPC.OnRoundStateChanged( RoundState.Freeze );

				break;
			}
			case RoundState.Play:
			{
				if ( Host.IsClient )
				{
					Audio.Announce( "prepare", Audio.Priority.High );

					return;
				}

				Event.Run( PBEvent.Round.Start );
				RPC.OnRoundStateChanged( RoundState.Play );

				UntilStateEnds = PlayDuration;

				break;
			}
			case RoundState.Bomb:
			{
				// clientside is handled somewhere else

				UntilStateEnds = Bomb.TimeUntilExplode;

				break;
			}
			case RoundState.End:
			{
				if ( Host.IsClient )
					return;

				Team winner = GetWinner();
				Event.Run( PBEvent.Round.End, winner );
				RPC.OnRoundStateChanged( RoundState.End, winner );

				UntilStateEnds = EndDuration;

				break;
			}
		}
	}

	public void RoundStateFinish()
	{
		Host.AssertServer();

		switch ( RoundState )
		{
			case RoundState.Freeze:
			{
				RoundState = RoundState.Play;

				break;
			}
			case RoundState.Play:
			{
				RoundState = RoundState.End;

				Team winner = GetWinner();

				if ( winner == Team.None )
					break;
				_ = winner == Team.Blue ? BlueScore++ : RedScore++;

				break;
			}
			case RoundState.Bomb:
			{
				RoundState = RoundState.End;

				Team winner = GetWinner();
				_ = winner == Team.Blue ? BlueScore++ : RedScore++;

				break;
			}
			case RoundState.End:
			{
				Round++;

				if ( BlueScore == ToWinScore || RedScore == ToWinScore || Round > RoundLimit )
				{
					Bomb?.Delete();
					Bomb = null;
					Game.Current.ChangeState( new MapSelectState() );

					return;
				}

				RoundState = RoundState.Freeze;

				AliveBlue = 0;
				AliveRed = 0;

				break;
			}
		}

		RoundStateStart();
	}

	private Team GetWinner()
	{
		if ( Bomb.IsValid() && Bomb.Disabled )
		{
			if ( Bomb.Defuser != null )
				return Team.Blue;
			else
				return Team.Red;
		}

		if ( AliveBlue != 0 && AliveRed != 0 )
			return BombEnabled ? Team.Blue : Team.None;

		if ( AliveBlue > AliveRed )
			return Team.Blue;
		else
			return Team.Red;
	}

	private void CheckRoundOver()
	{
		if ( RoundState == RoundState.Play )
		{
			if ( AliveBlue == 0 || AliveRed == 0 )
				RoundStateFinish();
		}
		else if ( RoundState == RoundState.Bomb )
		{
			if ( AliveBlue == 0 )
				RoundStateFinish();
		}
	}

	private void TeamBalance()
	{
		var teamBlue = Team.Blue.GetAll();
		var teamRed = Team.Red.GetAll();

		int diff = Math.Abs( teamBlue.Count() - teamRed.Count() ) >> 1;

		if ( diff <= 0 )
			return;

		UI.Notification.Create( To.Everyone, "Teams have been Auto-Balanced!", 3 );

		Team teamLess = teamBlue.Count() > teamRed.Count() ? Team.Red : Team.Blue;
		var teamMore = teamLess == Team.Blue ? teamRed : teamBlue;

		foreach ( var player in teamMore )
		{
			player.SetTeam( teamLess );

			if ( --diff == 0 )
				break;
		}
	}
}
