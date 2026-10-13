using System.Collections.Generic;
using GTA;
using GTA.Math;
using ZombiesMod.DataClasses;
using ZombiesMod.Extensions;
using ZombiesMod.Static;
using ZombiesMod.Wrappers;
using UI = GTA.UI;

namespace ZombiesMod.SurvivorTypes;

public class HostileSurvivors : Survivors
{
	private readonly PedGroup _group = new PedGroup();

	private readonly List<Ped> _peds = new List<Ped>();

	private Vehicle _vehicle;

	public override void Update()
	{
		if (_peds.Count <= 0)
		{
			Complete();
		}
	}

	public override void SpawnEntities()
	{
		Vector3 spawnPoint = GetSpawnPoint();
		if (!IsValidSpawn(spawnPoint))
		{
			return;
		}
		Vehicle val = World.CreateVehicle(Database.GetRandomVehicleModel(), spawnPoint, (float)Database.Random.Next(1, 360));
		if ((Entity)(object)val == (Entity)null)
		{
			Complete();
			return;
		}
		_vehicle = val;
		Blip val2 = ((Entity)_vehicle).AddBlip();
		val2.Name = Lang.T("hud.enemy_vehicle");
		val2.Sprite = (BlipSprite)225;
		val2.Color = (BlipColor)1;
		EntityEventWrapper entityEventWrapper = new EntityEventWrapper((Entity)(object)_vehicle);
		entityEventWrapper.Died += VehicleWrapperOnDied;
		entityEventWrapper.Updated += VehicleWrapperOnUpdated;
		for (int i = 0; i < val.PassengerCapacity + 1; i++)
		{
			if (_group.MemberCount < 6 && val.IsSeatFree((VehicleSeat)(-2)))
			{
				Ped val3 = val.CreateRandomPedOnSeat((VehicleSeat)((i == 0) ? (-1) : (-2)));
				if (!((Entity)(object)val3 == (Entity)null))
				{
					val3.Weapons.Give(Database.WeaponHashes[Database.Random.Next(Database.WeaponHashes.Length)], 25, true, true);
					val3.SetCombatAttributes(ZombiesMod.Extensions.CombatAttributes.AlwaysFight, enabled: true);
					val3.SetAlertness(Alertness.FullyAlert);
					val3.RelationshipGroup = Relationships.HostileRelationship;
					_group.Add(val3, i == 0);
					Blip val4 = ((Entity)val3).AddBlip();
					val4.Name = Lang.T("msg.blip_enemy");
					_peds.Add(val3);
					EntityEventWrapper entityEventWrapper2 = new EntityEventWrapper((Entity)(object)val3);
					entityEventWrapper2.Died += PedWrapperOnDied;
					entityEventWrapper2.Updated += PedWrapperOnUpdated;
					entityEventWrapper2.Disposed += PedWrapperOnDisposed;
				}
			}
		}
		UI.Notification.Show(Lang.T("notify.hostiles_nearby"), false);
	}

	private void PedWrapperOnDisposed(EntityEventWrapper sender, Entity entity)
	{
		if (_peds.Contains((Ped)(object)((entity is Ped) ? entity : null)))
		{
			_peds.Remove((Ped)(object)((entity is Ped) ? entity : null));
		}
	}

	private void VehicleWrapperOnUpdated(EntityEventWrapper sender, Entity entity)
	{
		if (!(entity == (Entity)null))
		{
			Blip blip = entity.AttachedBlip;
			if (blip != null)
			{
				Ped driver = (_vehicle == null) ? null : _vehicle.Driver;
				blip.Alpha = (driver != null && ((Entity)driver).Exists()) ? 255 : 0;
			}
		}
	}

	private void VehicleWrapperOnDied(EntityEventWrapper sender, Entity entity)
	{
		Blip currentBlip = entity.AttachedBlip;
		if (currentBlip != null)
		{
			currentBlip.Delete();
		}
		sender.Dispose();
		((Entity)_vehicle).MarkAsNoLongerNeeded();
		_vehicle = null;
	}

	private void PedWrapperOnUpdated(EntityEventWrapper sender, Entity entity)
	{
		Ped val = (Ped)(object)((entity is Ped) ? entity : null);
		if (!((Entity)(object)val == (Entity)null))
		{
			Vehicle currentVehicle = val.CurrentVehicle;
			if ((Entity)(object)((currentVehicle != null) ? currentVehicle.Driver : null) == (Entity)(object)val && !val.IsInCombat)
			{
				val.Task.DriveTo(val.CurrentVehicle, PlayerPosition, 25f, 75f);
			}
			if (((Entity)val).Position.VDist(PlayerPosition) > Survivors.DeleteRange)
			{
				((Entity)val).Delete();
			}
			Blip blip = ((Entity)val).AttachedBlip;
			if (blip != null && blip.Exists())
			{
				blip.Alpha = ((!val.IsInVehicle()) ? 255 : 0);
			}
		}
	}

	private void PedWrapperOnDied(EntityEventWrapper sender, Entity entity)
	{
		Blip currentBlip = entity.AttachedBlip;
		if (currentBlip != null)
		{
			currentBlip.Delete();
		}
		_peds.Remove((Ped)(object)((entity is Ped) ? entity : null));
	}

	public override void CleanUp()
	{
		Vehicle vehicle = _vehicle;
		if (vehicle != null)
		{
			Blip currentBlip = ((Entity)vehicle).AttachedBlip;
			if (currentBlip != null)
			{
				currentBlip.Delete();
			}
		}
		EntityEventWrapper.Dispose((Entity)(object)_vehicle);
	}

	public override void Abort()
	{
		Vehicle vehicle = _vehicle;
		if (vehicle != null)
		{
			((Entity)vehicle).Delete();
		}
		while (_peds.Count > 0)
		{
			Ped val = _peds[0];
			if (val != null)
			{
				((Entity)val).Delete();
			}
			_peds.RemoveAt(0);
		}
	}

	public HostileSurvivors()
	{
	}
}
