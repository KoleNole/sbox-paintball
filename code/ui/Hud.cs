using Sandbox;
using Sandbox.UI;

namespace PaintBall
{
	[Library]
	public partial class Hud : HudEntity<RootPanel>
	{
		// Ugly ass code. Use static instances?

		[ClientRpc]
		public static void AddKillFeed( string left, string right, string method, Team teamLeft, Team teamRight, long lsteamid, long rsteamid )
		{
			KillFeed.Instance.AddEntry( left, right, method, teamLeft, teamRight, lsteamid, rsteamid );
		}

		[ClientRpc]
		public static void UpdateCrosshairMessage( string text = "" )
		{
			(Local.Hud
				.GetChild( 2 )?
				.GetChild( 0 ) as Label)?
				.SetText( text );
		}

		[ClientRpc]
		public static void UpdateTeamScore( Team team, string text = "0" )
		{
			(GameInfo.Instance.Mid.GetChild( (int)team ).GetChild( 0 ) as Label).Text = text;
		}

		[ClientRpc]
		public static void OnTeamChanged( Client client, Team newTeam )
		{
			Scoreboard.Instance.UpdateEntry( client, newTeam );
		}

		public Hud()
		{
			if ( !IsClient )
				return;

			RootPanel.StyleSheet.Load( "/ui/Hud.scss" );

			RootPanel.AddChild<Ammo>();          // 0
			RootPanel.AddChild<ChatBox>();       // 1
			RootPanel.AddChild<Crosshair>();     // 2
			RootPanel.AddChild<GameInfo>();      // 3
			RootPanel.AddChild<InventoryBar>();  // 4
			RootPanel.AddChild<KillFeed>();      // 5
			RootPanel.AddChild<Scoreboard>();    // 6
			RootPanel.AddChild<TeamIndicator>(); // 7
		}
	}
}
