﻿using Sandbox;

namespace Paintball;

[Item( SlotType.Secondary )]
[Buyable( Price = 100 )]
[Library( "pb_pistol", Title = "Pistol", Icon = "ui/weapons/pistol.png", Spawnable = true )]
[Hammer.EditorModel( "weapons/rust_pistol/rust_pistol.vmdl" )]
public partial class Pistol : ProjectileWeapon<BouncyProjectile>
{
	public override int ClipSize => 10;
	public override ItemConfig Config { get; set; }
	public override float MovementSpeedMultiplier => 0.9f;
	public override float PrimaryRate => 15f;
	public override float ProjectileGravity => 10f;
	public override float ProjectileSpeed => 1500f;
	public override float ReloadTime => 2.0f;
	public override string ViewModelPath => "weapons/rust_pistol/v_rust_pistol.vmdl";

	public override void Spawn()
	{
		base.Spawn();

		AmmoClip = ClipSize;
		ReserveAmmo = 2 * ClipSize;

		SetModel( "weapons/rust_pistol/rust_pistol.vmdl" );
	}

	public override void SimulateAnimator( PawnAnimator anim )
	{
		anim.SetParam( "holdtype", 1 );
		anim.SetParam( "aimat_weight", 1.0f );
		anim.SetParam( "holdtype_handedness", 0 );
	}
}
