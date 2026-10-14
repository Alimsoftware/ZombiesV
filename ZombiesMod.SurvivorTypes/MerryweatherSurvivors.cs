using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI;
using LemonUI.Menus;
using LemonUI.TimerBars;
using LemonUI.Scaleform;
using ZombiesMod.DataClasses;
using ZombiesMod.Extensions;
using ZombiesMod.PlayerManagement;
using ZombiesMod.Static;
using ZombiesMod.Wrappers;
using GTA.Input;
using UI = GTA.UI;

namespace ZombiesMod.SurvivorTypes;

public class MerryweatherSurvivors : Survivors
{
	private enum DropType
	{
		Weapons,
		Loot
	}

	public const float InteractDistance = 2.3f;

	private const int BlipRadius = 145;

	private readonly int _timeOut;

	private DataClasses.ParticleEffect _particle;

	private readonly PedGroup _pedGroup = new PedGroup();

	private readonly List<Ped> _peds = new List<Ped>();

	private Blip _blip;

	private Prop _prop;

	private DropType _dropType;

	private bool _notify;

	private Vector3 _dropZone;

	private readonly TimerBarProgress _timerBar;

	private float _currentTime;

	private readonly PedHash[] _pedHashes;

	private readonly WeaponHash[] _weapons;

	public MerryweatherSurvivors(int timeout)
	{
		_pedHashes = new PedHash[3]
		{
			PedHash.Blackops01SMY,
			PedHash.Blackops01SMY,
			PedHash.Blackops03SMY
		};
		_weapons = new WeaponHash[5]
		{
			WeaponHash.AdvancedRifle,
			WeaponHash.AssaultRifle,
			WeaponHash.AssaultSMG,
			WeaponHash.AssaultShotgun,
			WeaponHash.BullpupRifle
		};
		_timerBar = new TimerBarProgress(Lang.T("hud.time_remaining"));
		_timeOut = timeout;
		_currentTime = _timeOut;
	}

	public override void Update()
	{
		if ((Entity)(object)_prop == (Entity)null)
		{
			return;
		}
		TryInteract((Entity)(object)_prop);
		UpdateTimer();
		if (CantSeeCrate())
		{
			return;
		}
		Blip val = ((Entity)_prop).AddBlip();
		if (!(val == (Blip)null))
		{
			val.Sprite = (BlipSprite)306;
			val.Color = (BlipColor)66;
			val.Name = Lang.T("msg.drop_crate");
			_blip.Delete();
			_peds.ForEach((Ped ped) =>
			{
				Blip val2 = ((Entity)ped).AddBlip();
				val2.Color = (BlipColor)66;
				val2.Name = Lang.T("msg.merryweather_guard");
			});
		}
	}

	private bool CantSeeCrate()
	{
		return !((Entity)_prop).IsOnScreen || ((Entity)_prop).IsOccluded || (((Entity)_prop).AttachedBlip?.Exists() == true) || ((Entity)_prop).Position.VDist(PlayerPosition) > 50f;
	}

	private void UpdateTimer()
	{
		if (PlayerPosition.VDist(_dropZone) < 145f)
		{
			if (!_notify)
			{
				UI.Notification.Show(Lang.T("notify.entering_hostile_zone"), false);
				_notify = true;
			}
			if (MenuConrtoller.BarPool.Contains((TimerBar)(object)_timerBar))
			{
				MenuConrtoller.BarPool.Remove((TimerBar)(object)_timerBar);
			}
			return;
		}
		if (!MenuConrtoller.BarPool.Contains((TimerBar)(object)_timerBar))
		{
			MenuConrtoller.BarPool.Add((TimerBar)(object)_timerBar);
		}
		_timerBar.Progress = Math.Max(0f, Math.Min(100f, _currentTime / (float)_timeOut * 100f));
		_currentTime -= Game.LastFrameTime;
		if (!(_currentTime > 0f))
		{
			Complete();
			UI.Notification.Show(Lang.T("notify.crate_recovery_failed"), false);
		}
	}

	public override void SpawnEntities()
	{
		Vector3 spawnPoint = GetSpawnPoint();
		if (!IsValidSpawn(spawnPoint))
		{
			return;
		}
		DropType[] array = (DropType[])Enum.GetValues(typeof(DropType));
		DropType dropType = array[Database.Random.Next(array.Length)];
		_dropType = dropType;
		string text = ((_dropType == DropType.Weapons) ? "prop_mil_crate_01" : "ex_prop_crate_closed_bc");
		_prop = World.CreateProp((Model)(text), spawnPoint, Vector3.Zero, false, false);
		if ((Entity)(object)_prop == (Entity)null)
		{
			Complete();
			return;
		}
		Vector3 position = ((Entity)_prop).Position;
		_blip = World.CreateBlip(position.Around(45f), 145f);
		_blip.Color = (BlipColor)66;
		_blip.Alpha = 150;
		_dropZone = _blip.Position;
		position = ((Entity)_prop).Position;
		Vector3 coord = position.Around(5f);
		_particle = WorldExtended.CreateParticleEffectAtCoord(coord, "exp_grd_flare");
		_particle.Color = Color.LightGoldenrodYellow;
		int num = Database.Random.Next(3, 6);
		for (int i = 0; i <= num; i++)
		{
			Vector3 val = spawnPoint.Around(10f);
			PedHash val2 = _pedHashes[Database.Random.Next(_pedHashes.Length)];
			Ped val3 = World.CreatePed((Model)(val2), val);
			if (!((Entity)(object)val3 == (Entity)null))
			{
				if (i > 0)
				{
					val3.Weapons.Give(_weapons[Database.Random.Next(_weapons.Length)], 45, true, true);
				}
				else
				{
					val3.Weapons.Give((WeaponHash)100416529, 15, true, true);
				}
				val3.Accuracy = 100;
				val3.Task.GuardCurrentPosition();
				val3.RelationshipGroup = Relationships.MilitiaRelationship;
				_pedGroup.Add(val3, i == 0);
				_peds.Add(val3);
				EntityEventWrapper entityEventWrapper = new EntityEventWrapper((Entity)(object)val3);
				entityEventWrapper.Died += PedWrapperOnDied;
			}
		}
		Model val4 = (Model)("mesa3");
		position = ((Entity)_prop).Position;
		World.CreateVehicle(val4, World.GetNextPositionOnStreet(position.Around(25f)));
		UI.Notification.Show(
			(_dropType == DropType.Loot)
				? Lang.T("notify.merryweather_drop_supplies")
				: Lang.T("notify.merryweather_drop_weapons"),
			false);
	}

	private void PedWrapperOnDied(EntityEventWrapper sender, Entity entity)
	{
		_peds.Remove((Ped)(object)((entity is Ped) ? entity : null));
		entity.MarkAsNoLongerNeeded();
		Blip currentBlip = entity.AttachedBlip;
		if (currentBlip != null)
		{
			currentBlip.Delete();
		}
		sender.Dispose();
	}

	private void TryInteract(Entity prop)
	{
		if (prop == (Entity)null || prop.Position.VDist(PlayerPosition) >= 2.3f)
		{
			return;
		}
		UiExtended.DisplayHelpTextThisFrame(Lang.T("help.loot_crate"));
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)51);
		if (!Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)51))
		{
			return;
		}
		prop.Delete();
		switch (_dropType)
		{
		case DropType.Loot:
		{
			int num3 = Database.Random.Next(1, 3);
			PlayerInventory.Instance.PickupLoot(null, ItemType.Item, num3, num3, 0.4f);
			break;
		}
		case DropType.Weapons:
		{
			int num = Database.Random.Next(3, 5);
			int num2 = 0;
			for (int i = 0; i <= num; i++)
			{
				WeaponHash[] array = ((WeaponHash[])Enum.GetValues(typeof(WeaponHash))).Where(IsGoodHash).ToArray();
				if (array.Length != 0)
				{
					WeaponHash val = array[Database.Random.Next(array.Length)];
					PlayerPed.Weapons.Give(val, Database.Random.Next(20, 45), false, true);
					num2++;
				}
			}
			UI.Notification.Show(Lang.T("notify.weapons_found", num2), false);
			break;
		}
		}
		Complete();
	}

	public override void CleanUp()
	{
		_particle.Delete();
		_peds?.ForEach((Ped ped) =>
		{
			Blip currentBlip = ((Entity)ped).AttachedBlip;
			if (currentBlip != null)
			{
				currentBlip.Delete();
			}
			ped.AlwaysKeepTask = true;
			((Entity)ped).IsPersistent = false;
		});
		Blip blip = _blip;
		if (blip != null)
		{
			blip.Delete();
		}
		if (MenuConrtoller.BarPool.Contains((TimerBar)(object)_timerBar))
		{
			MenuConrtoller.BarPool.Remove((TimerBar)(object)_timerBar);
		}
	}

	public override void Abort()
	{
		_particle?.Delete();
		Prop prop = _prop;
		if (prop != null)
		{
			((Entity)prop).Delete();
		}
		_peds?.ForEach((Ped ped) =>
		{
			Blip currentBlip = ((Entity)ped).AttachedBlip;
			if (currentBlip != null)
			{
				currentBlip.Delete();
			}
			((Entity)ped).Delete();
		});
		Blip blip = _blip;
		if (blip != null)
		{
			blip.Delete();
		}
		if (MenuConrtoller.BarPool.Contains((TimerBar)(object)_timerBar))
		{
			MenuConrtoller.BarPool.Remove((TimerBar)(object)_timerBar);
		}
	}

	private bool IsGoodHash(WeaponHash hash)
	{
		return (int)hash != -1569615261 && (int)hash != -1600701090 && (int)hash != 600439132 && (int)hash != 126349499 && !PlayerPed.Weapons.HasWeapon(hash);
	}
}
