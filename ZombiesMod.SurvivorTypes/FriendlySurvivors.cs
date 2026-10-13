using System.Collections.Generic;
using GTA;
using GTA.Math;
using ZombiesMod.DataClasses;
using ZombiesMod.Extensions;
using ZombiesMod.Static;
using ZombiesMod.Wrappers;
using UI = GTA.UI;

namespace ZombiesMod.SurvivorTypes;

public class FriendlySurvivors : Survivors
{
	private readonly List<Ped> _peds = new List<Ped>();

	private readonly PedGroup _pedGroup = new PedGroup();

	public static FriendlySurvivors Instance { get; private set; }

	public FriendlySurvivors()
	{
		Instance = this;
	}

	public void RemovePed(Ped item)
	{
		if (_peds.Contains(item))
		{
			_peds.Remove(item);
			item.LeaveGroup();
			Blip currentBlip = ((Entity)item).AttachedBlip;
			if (currentBlip != null)
			{
				currentBlip.Delete();
			}
			EntityEventWrapper.Dispose((Entity)(object)item);
		}
	}

	public override void Update()
	{
		if (_peds.Count <= 0)
		{
			Complete();
		}
	}

	public override void SpawnEntities()
	{
		int num = Database.Random.Next(3, 6);
		Vector3 spawnPoint = GetSpawnPoint();
		if (!IsValidSpawn(spawnPoint))
		{
			return;
		}
		for (int i = 0; i <= num; i++)
		{
			Ped val = World.CreateRandomPed(spawnPoint.Around(5f));
			if (!((Entity)(object)val == (Entity)null))
			{
				Blip val2 = ((Entity)val).AddBlip();
				val2.Color = (BlipColor)3;
				val2.Name = Lang.T("msg.blip_ally");
				val.RelationshipGroup = Relationships.FriendlyRelationship;
				val.Task.FightAgainstHatedTargets(9000f);
				val.SetAlertness(Alertness.FullyAlert);
				val.SetCombatAttributes(ZombiesMod.Extensions.CombatAttributes.AlwaysFight, enabled: true);
				val.Weapons.Give(Database.WeaponHashes[Database.Random.Next(Database.WeaponHashes.Length)], 25, true, true);
				_pedGroup.Add(val, i == 0);
				_pedGroup.Formation = (Formation)0;
				_peds.Add(val);
				EntityEventWrapper entityEventWrapper = new EntityEventWrapper((Entity)(object)val);
				entityEventWrapper.Died += EventWrapperOnDied;
				entityEventWrapper.Disposed += EventWrapperOnDisposed;
			}
		}
		UI.Notification.Show(Lang.T("notify.friendly_nearby"), true);
	}

	private void EventWrapperOnDisposed(EntityEventWrapper sender, Entity entity)
	{
		if (_peds.Contains((Ped)(object)((entity is Ped) ? entity : null)))
		{
			_peds.Remove((Ped)(object)((entity is Ped) ? entity : null));
		}
	}

	private void EventWrapperOnDied(EntityEventWrapper sender, Entity entity)
	{
		_peds.Remove((Ped)(object)((entity is Ped) ? entity : null));
		Blip currentBlip = entity.AttachedBlip;
		if (currentBlip != null)
		{
			currentBlip.Delete();
		}
		entity.MarkAsNoLongerNeeded();
		sender.Dispose();
	}

	public override void CleanUp()
	{
		_peds.ForEach((Ped ped) =>
		{
			Blip currentBlip = ((Entity)ped).AttachedBlip;
			if (currentBlip != null)
			{
				currentBlip.Delete();
			}
			((Entity)ped).MarkAsNoLongerNeeded();
			EntityEventWrapper.Dispose((Entity)(object)ped);
		});
	}

	public override void Abort()
	{
		while (_peds.Count > 0)
		{
			Ped val = _peds[0];
			((Entity)val).Delete();
			_peds.RemoveAt(0);
		}
	}
}
