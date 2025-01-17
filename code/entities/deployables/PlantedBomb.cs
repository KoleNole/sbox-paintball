﻿using Paintball.UI;
using Sandbox;
using Sandbox.UI;
using Sandbox.Component;

namespace Paintball;

[Hammer.Skip]
public partial class PlantedBomb : ModelEntity, IUse, ILook
{
	[Net, Change] public Player Defuser { get; set; }
	[Net] public TimeSince TimeSinceStartedBeingDefused { get; set; } = 0f;
	[Net] public TimeUntil TimeUntilExplode { get; set; }
	public Bombsite Bombsite { get; set; }
	public Player Planter { get; set; }
	public bool Disabled { get; set; }
	public Panel LookPanel { get; set; }
	public TimeUntil UntilTickSound { get; set; }
	private GameplayState _gameplayState;
	private Glow _glow;

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/rust_props/small_junk/newspaper_stack_a.vmdl" );

		PhysicsEnabled = false;
		UsePhysicsCollision = false;
		SetInteractsAs( CollisionLayer.All );
		SetInteractsWith( CollisionLayer.WORLD_GEOMETRY );
	}

	public void Initialize()
	{
		TimeUntilExplode = GameplayState.BombDuration;
		_gameplayState = Game.Current.State as GameplayState;
		_gameplayState.Bomb = this;

		if ( _gameplayState.RoundState == RoundState.Play )
		{
			_gameplayState.RoundState = RoundState.Bomb;
			_gameplayState.RoundStateStart();
		}

		Planter.AddMoney( 1000 );

		Event.Run( PBEvent.Round.Bomb.Planted, this );
		Bombsite.OnBombPlanted.Fire( this );
		OnPlanted( Planter );
	}

	public void Tick()
	{
		if ( Disabled )
			return;

		if ( IsClient )
		{
			if ( UntilTickSound )
			{
				Sound.FromEntity( "bomb_tick", this );
				UntilTickSound = 1f;
			}

			return;
		}

		if ( Defuser?.Using != this )
		{
			Defuser = null;
			TimeSinceStartedBeingDefused = 0f;
		}

		if ( TimeSinceStartedBeingDefused >= 5f )
		{
			Disabled = true;
			Defuser.AddMoney( 1000 );

			Event.Run( PBEvent.Round.Bomb.Defused, this );
			Bombsite.OnBombDefused.Fire( this );
			OnDisabled( Defuser );
		}
		else if ( TimeUntilExplode )
		{
			Disabled = true;
			Defuser = null;

			Event.Run( PBEvent.Round.Bomb.Explode, this );
			Bombsite.OnBombExplode.Fire( this );
			OnDisabled( Defuser );
		}

		if ( Disabled && _gameplayState.RoundState == RoundState.Bomb )
			_gameplayState.RoundStateFinish();

	}

	[ClientRpc]
	public void OnPlanted( Player planter )
	{
		Planter = planter;
		_gameplayState = Game.Current.State as GameplayState;
		UntilTickSound = _gameplayState.UntilStateEnds.Relative % 1;

		Sound.FromEntity( "bomb_plant", this );

		if ( (Local.Pawn as Player).Team != Team.Red )
		{
			_glow = Components.GetOrCreate<Glow>();
			_glow.Active = true;
			_glow.RangeMin = 0;
			_glow.RangeMax = 1000;
			_glow.Color = Color.Red;
		}

		if ( _gameplayState.RoundState == RoundState.Play )
		{
			_gameplayState.RoundState = RoundState.Bomb;
			Notification.Create( "Bomb has been planted!", 3 );
			Audio.Announce( "bomb_planted", Audio.Priority.Medium );
		}

		_gameplayState.Bomb = this;

		Event.Run( PBEvent.Round.Bomb.Planted, this );
	}

	[ClientRpc]
	public void OnDisabled( Player defuser )
	{
		Disabled = true;
		Defuser = defuser;
		_glow.Active = false;

		if ( Defuser != null )
			Event.Run( PBEvent.Round.Bomb.Defused, this );
		else
			Event.Run( PBEvent.Round.Bomb.Explode, this );
	}

	bool IUse.IsUsable( Entity user )
	{
		return !Disabled && user is Player player && player.Team == Team.Blue && Defuser == null && user.GroundEntity != null;
	}

	bool IUse.OnUse( Entity user )
	{
		Defuser = user as Player;
		return !Disabled;
	}

	bool ILook.IsLookable( Entity viewer )
	{
		if ( viewer is not Player player )
			return false;

		return !Disabled && player.Team == Team.Blue && (Defuser == null || Defuser == player);
	}

	void ILook.StartLook( Entity viewer )
	{
		if ( Defuser == null && viewer == Local.Pawn )
		{
			LookPanel = Local.Hud.AddChild<WeaponLookAt>();
			(LookPanel as WeaponLookAt).InputHint.Context.Text = "Hold to defuse";
			(LookPanel as WeaponLookAt).Icon.SetTexture( "ui/weapons/bomb.png" );
		}
		else if ( Defuser == viewer )
		{
			LookPanel = Local.Hud.AddChild<BombDefuse>();
		}
	}

	void ILook.EndLook( Entity viewer )
	{
		LookPanel?.Delete();
	}

	private void OnDefuserChanged( Player oldDefuser, Player newDefuser )
	{
		if ( !Disabled && newDefuser != null )
			Sound.FromEntity( "bomb_disarm", this );

		if ( (Local.Pawn as Player).Looking != this )
			return;

		if ( newDefuser == (Local.Pawn as Player).CurrentPlayer && LookPanel is not BombDefuse )
		{
			LookPanel?.Delete();
			LookPanel = Local.Hud.AddChild<BombDefuse>();
		}
		else if ( newDefuser == null && LookPanel is BombDefuse )
		{
			LookPanel?.Delete();
			LookPanel = null;

			if ( !Local.Pawn.Alive() )
				return;

			LookPanel = Local.Hud.AddChild<WeaponLookAt>();
			(LookPanel as WeaponLookAt).InputHint.Context.Text = "Press E to defuse";
			(LookPanel as WeaponLookAt).Icon.SetTexture( "ui/weapons/bomb.png" );
		}
	}
}
