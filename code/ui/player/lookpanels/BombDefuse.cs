﻿using Sandbox;
using Sandbox.UI;

namespace Paintball.UI;

public class BombDefuse : Panel
{
	public InputHint InputHint;
	public ProgressBar ProgressBar;

	public BombDefuse()
	{
		StyleSheet.Load( "/ui/player/lookpanels/BombDefuse.scss" );

		InputHint = AddChild<InputHint>();
		InputHint.SetButton( InputButton.Use );
		InputHint.Context.Text = "Hold to defuse";

		AddChild( new ProgressBar( () =>
		{
			var bomb = (Game.Current.State as GameplayState).Bomb;
			return (float)bomb.TimeSinceStartedBeingDefused.Relative / 5f;
		} ) );

		ProgressBar = this.LastChild() as ProgressBar;
	}
}
