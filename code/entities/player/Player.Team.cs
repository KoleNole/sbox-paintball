﻿using Sandbox;
using Sandbox.UI;

namespace Paintball;

public partial class Player : ITeamEntity
{
	[Net, Change] public Team Team { get; set; }
	public TimeSince TimeSinceTeamChanged { get; private set; } = 5f;

	public void SetTeam( Team newTeam )
	{
		TimeSinceTeamChanged = 0f;

		Team oldTeam = Team;
		Tags.Remove( $"{oldTeam.GetTag()}" );

		Team = newTeam;
		Tags.Add( $"{newTeam.GetTag()}" );
		RenderColor = newTeam.GetColor();

		Client.SetInt( "team", (int)newTeam );

		if ( newTeam != Team.None )
			ChatBox.AddInformation( To.Everyone, $"{Client.Name} has joined {newTeam.GetName()}", $"avatar:{Client.PlayerId}" );
		else
			ChatBox.AddInformation( To.Everyone, $"{Client.Name} has started spectating" );

		Game.Current.State.OnPlayerChangedTeam( this, oldTeam );
		Event.Run( PBEvent.Player.Team.Changed, this, oldTeam );
	}

	public void OnTeamChanged( Team oldTeam, Team newTeam )
	{
		if ( IsLocalPawn && !IsSpectatingPlayer )
		{
			Local.Hud.RemoveClass( oldTeam.GetTag() );
			Local.Hud.AddClass( newTeam.GetTag() );
		}

		Event.Run( PBEvent.Player.Team.Changed, this, oldTeam );
	}

	[ServerCmd( "changeteam", Help = "Changes the caller's team" )]
	public static void ChangeTeamCommand( Team team )
	{
		Client client = ConsoleSystem.Caller;

		if ( client == null || client.Pawn is not Player player )
			return;

		if ( player.Team == team || player.TimeSinceTeamChanged <= 5f )
			return;

		if ( team == Team.None )
		{
			player.SetTeam( team );

			return;
		}

		int blueCount = Team.Blue.GetCount();
		int redCount = Team.Red.GetCount();
		int adjust = player.Team == Team.None ? 1 : 0;

		if ( team == Team.Blue && (blueCount < redCount + adjust) )
			player.SetTeam( team );
		else if ( team == Team.Red && (redCount < blueCount + adjust) )
			player.SetTeam( team );
	}
}
