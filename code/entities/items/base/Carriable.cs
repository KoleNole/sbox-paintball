﻿using Paintball.UI;
using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System.ComponentModel;

namespace Paintball;

public enum SlotType : byte
{
	Primary = 0,
	Secondary = 1,
	Melee = 2,
	Utility = 3,
	Deployable = 4,
}

[Library( "carri" ), AutoGenerate]
public partial class CarriableInfo : Asset
{
	public static Dictionary<string, CarriableInfo> All { get; set; } = new();
	public Model CachedViewModel { get; set; }
	public Model CachedWorldModel { get; set; }
	public LibraryAttribute LibraryAttribute { get; set; }
	public string Title { get; set; }

	[Property, Category( "Important" )] public bool Buyable { get; set; }
	[Property, Category( "Important" )] public Team ExclusiveFor { get; set; }
	[Property, Category( "Important" )] public int HoldType { get; set; }
	[Property, Category( "Important" )] public string LibraryName { get; set; } 
	[Property, Category( "Important" )] public SlotType Slot { get; set; }
	[Property, Category( "UI" ), ResourceType( "png" )] public string Icon { get; set; } = "";
	[Property, Category( "Models" ), ResourceType( "vmdl" )] public string ViewModel { get; set; } = "";
	[Property, Category( "Models" ), ResourceType( "vmdl" )] public string WorldModel { get; set; } = "";
	[Property, Category( "Stats" )] public float DeployTime { get; set; } = 0.6f;
	[Property, Category( "Stats" )] public float MovementSpeedMultiplier { get; set; } = 1f;
	[Property, Category( "Stats" )] public int Price { get; set; }

	protected override void PostLoad()
	{
		base.PostLoad();

		if ( string.IsNullOrEmpty( LibraryName ) )
			return;

		var attribute = Library.GetAttribute( LibraryName );

		if ( attribute == null )
			return;

		All[LibraryName] = this;

		CachedViewModel = Model.Load( ViewModel );
		CachedWorldModel = Model.Load( WorldModel );
		LibraryAttribute = attribute;
		Title = attribute.Title;
	}
}

[Hammer.Skip]
public abstract partial class Carriable : BaseCarriable, IUse, ILook
{
	[Net, Predicted] public TimeSince TimeSinceDeployed { get; protected set; }
	public virtual string CrosshairClass => "standard";
	public virtual bool Droppable => true;
	public CarriableInfo Info { get; set; }
	public Panel LookPanel { get; set; }
	public Player PreviousOwner { get; private set; }
	public TimeSince TimeSinceDropped { get; private set; }
	public new Player Owner
	{
		get => base.Owner as Player;
		set => base.Owner = value;
	}

	public Carriable() { }

	public override void Spawn()
	{
		base.Spawn();

		CollisionGroup = CollisionGroup.Weapon; // so players touch it as a trigger but not as a solid
		SetInteractsAs( CollisionLayer.Debris ); // so player movement doesn't walk into it

		if ( string.IsNullOrEmpty( ClassInfo?.Name ) )
		{
			Log.Error( this + " doesn't have a Library name!" );

			return;
		}

		Info = CarriableInfo.All[ClassInfo.Name];
		SetModel( Info.WorldModel );
	}

	public override void ClientSpawn()
	{
		base.ClientSpawn();

		if ( !string.IsNullOrEmpty( ClassInfo.Name ) )
			Info = CarriableInfo.All[ClassInfo?.Name];

		if ( Local.Pawn is not Player player )
			return;

		if ( !IsLocalPawn && IsActiveChild() )
		{
			CreateViewModel();
			CreateHudElements();

			if ( player.Alive() || player.CurrentPlayer != Owner )
				ViewModelEntity.EnableDrawing = false;
		}
	}

	public override void ActiveStart( Entity entity )
	{
		base.ActiveStart( entity );

		TimeSinceDeployed = 0;

		if ( IsServer )
			OnActiveStartClient( To.Everyone );
	}

	public override void ActiveEnd( Entity ent, bool dropped )
	{
		base.ActiveEnd( ent, dropped );

		if ( IsServer )
			OnActiveEndClient( To.Everyone );
	}

	public override void Simulate( Client owner )
	{
		if ( TimeSinceDeployed < Info.DeployTime )
			return;

		base.Simulate( owner );
	}

	public override void SimulateAnimator( PawnAnimator anim )
	{
		anim.SetAnimParameter( "holdtype", Info.HoldType );
	}

	public override void CreateViewModel()
	{
		Host.AssertClient();

		if ( string.IsNullOrEmpty( Info.ViewModel ) )
			return;

		ViewModelEntity = new ViewModel
		{
			Position = Position,
			Owner = Owner,
			EnableViewmodelRendering = true,
			FieldOfView = 80
		};

		ViewModelEntity.SetModel( Info.ViewModel );
	}

	public override void CreateHudElements()
	{
		if ( Local.Hud == null )
			return;

		CrosshairPanel = new Crosshair
		{
			Parent = Local.Hud,
			TargetWeapon = this
		};

		CrosshairPanel.AddClass( CrosshairClass );
	}

	public override bool CanCarry( Entity carrier )
	{
		if ( Owner != null || carrier is not Player player )
			return false;

		if ( Info.ExclusiveFor != Team.None && player.Team != Info.ExclusiveFor )
			return false;

		if ( !player.Inventory.HasFreeSlot( Info.Slot ) )
			return false;

		return base.CanCarry( carrier );
	}

	public override void OnCarryStart( Entity carrier )
	{
		base.OnCarryStart( carrier );

		PreviousOwner = Owner;
		Owner.Inventory.SlotCapacity[(int)Info.Slot]--;
	}

	public override void OnCarryDrop( Entity dropper )
	{
		base.OnCarryDrop( dropper );

		TimeSinceDropped = 0f;

		if ( PreviousOwner.IsValid() )
			PreviousOwner.Inventory.SlotCapacity[(int)Info.Slot]++;

		OnActiveEndClient( To.Everyone );
	}

	/// <summary>
	/// Gets called on the server and client
	/// </summary>
	public virtual void Reset()
	{
		if ( IsServer )
		{
			using ( Prediction.Off() )
			{
				TimeSinceDeployed = 0;
				TimeSinceDropped = 0;
			}

			return;
		}

		ViewModelEntity?.SetAnimParameter( "deploy", true );
	}

	public bool IsActiveChild()
	{
		return Owner?.ActiveChild == this;
	}

	#region rpc
	[ClientRpc]
	protected void OnActiveStartClient()
	{
		if ( IsLocalPawn || ViewModelEntity != null )
			return;

		CreateViewModel();

		if ( (Local.Pawn as Player).CurrentPlayer != Owner )
			ViewModelEntity.EnableDrawing = false;

		ViewModelEntity?.SetAnimParameter( "deploy", true );
	}

	[ClientRpc]
	protected void OnActiveEndClient()
	{
		if ( IsLocalPawn )
			return;

		DestroyHudElements();
		DestroyViewModel();
	}
	#endregion

	[Event.Entity.PostCleanup]
	protected void PostCleanup()
	{
		Reset();
	}

	bool IUse.OnUse( Entity user )
	{
		if ( IsServer && user is Player player )
			player.Inventory.Swap( this );

		return false;
	}

	bool IUse.IsUsable( Entity user )
	{
		if ( Owner != null || user is not Player player )
			return false;

		if ( Info.ExclusiveFor != Team.None && player.Team != Info.ExclusiveFor )
			return false;

		return true;
	}

	bool ILook.IsLookable( Entity viewer )
	{
		return (this as IUse).IsUsable( viewer );
	}

	void ILook.StartLook( Entity viewer )
	{
		if ( viewer != Local.Pawn )
			return;

		LookPanel = Local.Hud.AddChild<WeaponLookAt>();
		(LookPanel as WeaponLookAt).Icon.SetTexture( Info.Icon );
	}

	void ILook.EndLook( Entity viewer )
	{
		LookPanel?.Delete();
	}
}
