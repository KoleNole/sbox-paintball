﻿using Sandbox;
using System.Linq;

namespace PaintBall
{
	public partial class Player : Sandbox.Player
	{
		[Net, Change] public Team Team { get; set; }
		[Net] public TimeSince TimeSinceSpawned { get; private set; }
		public DamageInfo LastDamageInfo { get; set; }
		public ProjectileSimulator Projectiles { get; set; }

		public Player()
		{
			Inventory = new Inventory( this );
			Projectiles = new( this );
			EnableTouch = true;

			LifeState = LifeState.Dead;
		}

		public override void Respawn()
		{
			TimeSinceSpawned = 0f;

			RemoveCorpse();

			SetModel( "models/citizen/citizen.vmdl" );

			Controller = new CustomWalkController();

			Animator = new StandardPlayerAnimator();

			Camera = new FirstPersonCamera();

			EnableAllCollisions = true;
			EnableDrawing = true;
			EnableHideInFirstPerson = true;
			EnableShadowInFirstPerson = false;

			TimeSinceSpawned = 0f;
			RenderColor = Team.GetColor();

			base.Respawn();

			Game.Instance.CurrentGameState.OnPlayerSpawned( this );
		}

		public override void Simulate( Client cl )
		{
			using ( LagCompensation() )
				Projectiles.Simulate();

			var controller = GetActiveController();

			SimulateActiveChild( cl, ActiveChild );

			if ( Input.ActiveChild != null )
				ActiveChild = Input.ActiveChild;

			if ( LifeState != LifeState.Alive )
			{
				ChangeSpectateCamera();

				return;
			}

			if ( Input.Pressed( InputButton.Drop ) )
			{
				var dropped = Inventory.DropActive();

				if ( dropped != null )
				{
					if ( dropped.PhysicsGroup != null )
					{
						dropped.PhysicsGroup.Velocity = Velocity + (EyeRot.Forward + EyeRot.Up) * 300;

						SwitchToBestWeapon();
					}
				}
			}

			controller?.Simulate( cl, this, GetActiveAnimator() );
		}

		public override void StartTouch( Entity other )
		{
			if ( IsClient )
				return;

			if ( other is Weapon weapon && weapon.PreviousOwner == this && weapon.TimeSinceDropped < 2f )
				return;

			if ( other is PickupTrigger )
			{
				StartTouch( other.Parent );
				return;
			}

			Inventory?.Add( other, Inventory.Active == null );
		}

		public void Reset()
		{
			Inventory.DeleteContents();

			Client.SetInt( "deaths", 0 );
			Client.SetInt( "kills", 0 );

			LastAttacker = null;
			LastDamageInfo = default;
		}

		public void SetTeam( Team newTeam )
		{
			Team oldTeam = Team;
			Tags.Remove( $"{oldTeam.GetString()}player" );
			Team = newTeam;
			Tags.Add( $"{newTeam.GetString()}player" );
			Client.SetInt( "team", (int)newTeam );

			Hud.OnTeamChanged( To.Everyone, Client, newTeam );

			Game.Instance.CurrentGameState.OnPlayerChangedTeam( this, oldTeam, newTeam );
		}

		public void OnTeamChanged( Team oldTeam, Team newTeam )
		{
			if ( IsLocalPawn )
			{
				Local.Hud.RemoveClass( oldTeam.GetString() );
				Local.Hud.AddClass( newTeam.GetString() );
			}
		}

		public override void Spawn()
		{
			EnableLagCompensation = true;

			base.Spawn();
		}

		public override void OnKilled()
		{
			base.OnKilled();

			Inventory.DropActive();
			Inventory.DeleteContents();

			BecomeRagdollOnClient( LastDamageInfo.Force, GetHitboxBone( LastDamageInfo.HitboxIndex ) );

			RemoveAllDecals();

			var attacker = LastAttacker;

			if ( attacker.IsValid() )
			{
				if ( attacker is Player killer )
					killer?.OnPlayerKill();

				Game.Instance.CurrentGameState?.OnPlayerKilled( this, attacker, LastDamageInfo );
			}
			else
			{
				Game.Instance.CurrentGameState?.OnPlayerKilled( this, null, LastDamageInfo );
			}
		}

		protected void OnPlayerKill()
		{
		}

		public override void TakeDamage( DamageInfo info )
		{
			// Spawnprotection
			if ( TimeSinceSpawned < 1f )
				return;

			LastDamageInfo = info;

			base.TakeDamage( info );
		}

		private void SwitchToBestWeapon()
		{
			var best = Children.Select( x => x as Weapon )
			.Where( x => x.IsValid() )
			.OrderByDescending( x => x.Bucket )
			.FirstOrDefault();

			if ( best == null ) return;

			ActiveChild = best;
		}
	}
}
