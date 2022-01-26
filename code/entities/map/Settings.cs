﻿using Sandbox;

namespace Paintball;

[Library( "pb_map_settings", Title = "Map Settings", Spawnable = true )]
public partial class MapSettings : Entity
{
	[Net, Property] public string BlueTeamName { get; set; } = "Blue";
	[Net, Property] public string RedTeamName { get; set; } = "Red";
	protected Output OnRoundStart { get; set; }
	protected Output OnRoundEnd { get; set; }
	protected Output OnRoundNew { get; set; }

	public override void Spawn()
	{
		if ( Game.Current.Map.Settings.IsValid() )
		{
			Log.Warning( "You have several Map Settings. This is not allowed!" );
			Delete();
			return;
		}

		base.Spawn();

		Game.Current.Map.Settings = this;
		Transmit = TransmitType.Always;

		Event.Run( PBEvent.Game.Map.SettingsLoaded );
	}

	public override void ClientSpawn()
	{
		if ( IsClientOnly )
		{
			Log.Warning( "Can't create Map Settings on the client!" );
			Delete();
			return;
		}

		base.ClientSpawn();

		Game.Current.Map.Settings = this;
		Event.Run( PBEvent.Game.Map.SettingsLoaded );
	}

	[PBEvent.Round.Start]
	private void RoundStart()
	{
		OnRoundStart.Fire( this );
	}

	[PBEvent.Round.End]
	private void RoundEnd( Team winner )
	{
		OnRoundEnd.Fire( this );
	}

	[PBEvent.Round.New]
	private void NewRound()
	{
		OnRoundNew.Fire( this );
	}
}