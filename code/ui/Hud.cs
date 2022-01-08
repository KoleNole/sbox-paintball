using Sandbox;
using Sandbox.UI;

namespace PaintBall
{
	[Library]
	public partial class Hud : HudEntity<RootPanel>
	{
		[ClientRpc]
		public static void UpdateTeamScore( Team team, string text = "0" )
		{
			(RoundInfo.Instance.Middle.GetChild( (int)team ).GetChild( 0 ) as Label).Text = text;
		}

		public Hud()
		{
			if ( !IsClient )
				return;

			RootPanel.StyleSheet.Load( "/ui/Hud.scss" );

			RootPanel.AddChild<Ammo>();                  // 0
			RootPanel.AddChild<ChatBox>();               // 1
			RootPanel.AddChild<InventoryBar>();          // 2
			RootPanel.AddChild<KillFeed>();              // 3
			RootPanel.AddChild<RoundInfo>();             // 4
			RootPanel.AddChild<Scoreboard>();            // 5
			RootPanel.AddChild<TeamIndicator>();         // 6
			RootPanel.AddChild<TeamSelect>();            // 7
			RootPanel.AddChild<VoiceList>();             // 8
		}
	}
}
