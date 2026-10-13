using System;
using GTA;
using GTA.Native;
using ZombiesMod.Extensions;
using ZombiesMod.Static;
using ZombiesMod.Zombies;
using ZombiesMod.Zombies.ZombieTypes;

namespace ZombiesMod.Scripts;

public static class ZombieCreator
{
	public static bool Runners { get; set; }

	public static ZombiePed InfectPed(Ped ped, int health, bool overrideAsFastZombie = false)
	{
		ped.CanPlayGestures = false;
		ped.SetCanPlayAmbientAnims(toggle: false);
		ped.SetCanEvasiveDive(toggle: false);
		ped.SetPathCanUseLadders(toggle: false);
		ped.SetPathCanClimb(toggle: false);
		ped.DisablePainAudio(toggle: true);
		ped.ApplyDamagePack(0f, 1f, DamagePack.BigHitByVehicle);
		ped.ApplyDamagePack(0f, 1f, DamagePack.ExplosionMed);
		ped.DiesOnLowHealth = false;
		ped.SetAlertness(Alertness.Nuetral);
		ped.SetCombatAttributes(ZombiesMod.Extensions.CombatAttributes.AlwaysFight, enabled: true);
		Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, new InputArgument[3]
		{
			((Entity)ped).Handle,
			0,
			0
		});
		ped.SetConfigFlag(281, true);
		ped.Task.WanderAround(((Entity)ped).Position, ZombiePed.WanderRadius);
		ped.AlwaysKeepTask = true;
		ped.BlockPermanentEvents = true;
		((Entity)ped).IsPersistent = false;
		Blip currentBlip = ((Entity)ped).AttachedBlip;
		if (currentBlip != null)
		{
			currentBlip.Delete();
		}
		((Entity)ped).IsPersistent = true;
		ped.RelationshipGroup = Relationships.InfectedRelationship;
		float num = 0.055f;
		if (IsNightFall())
		{
			num = 0.5f;
		}
		TimeSpan currentDayTime = World.CurrentTimeOfDay;
		if (currentDayTime.Hours >= 20 || currentDayTime.Hours <= 3)
		{
			num = 0.4f;
		}
		if (((Database.Random.NextDouble() < (double)num) | overrideAsFastZombie) && Runners)
		{
			return new Runner(((Entity)ped).Handle);
		}
		int health2 = (((Entity)ped).MaxHealth = health);
		((Entity)ped).Health = health2;
		return new Walker(((Entity)ped).Handle);
	}

	public static bool IsNightFall()
	{
		if (!Runners)
		{
			return false;
		}
		TimeSpan currentDayTime = World.CurrentTimeOfDay;
		return currentDayTime.Hours >= 20 || currentDayTime.Hours <= 3;
	}
}
