using System;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using ZombiesMod.Extensions;
using ZombiesMod.Static;

namespace ZombiesMod.Controllers;

public class WorldController : SafeScript
{
	private bool _reset;

	public static bool Configure { get; set; }

	public static bool StopPedsFromSpawning { get; set; }

	public Vector3 PlayerPosition
	{
		get
		{
			return Database.PlayerPosition;
		}
	}

	public WorldController()
	{
		WrapEvents();
		((Script)this).Aborted += OnAborted;
	}

	private static void OnAborted(object sender, EventArgs e)
	{
		Reset();
	}

	private static void Reset()
	{
		Function.Call(Hash.CAN_CREATE_RANDOM_COPS, new InputArgument[1] { true });
		Function.Call(Hash.SET_RANDOM_BOATS, new InputArgument[1] { true });
		Function.Call(Hash.SET_RANDOM_TRAINS, new InputArgument[1] { true });
		Function.Call(Hash.SET_GARBAGE_TRUCKS, new InputArgument[1] { true });
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (Configure)
		{
			WorldExtended.ClearCops(10000f);
			WorldExtended.SetScenarioPedDensityThisMultiplierFrame(0f);
			WorldExtended.SetVehicleDensityMultiplierThisFrame(0f);
			WorldExtended.SetRandomVehicleDensityMultiplierThisFrame(0f);
			WorldExtended.SetParkedVehicleDensityMultiplierThisFrame(0f);
			WorldExtended.SetPedDensityThisMultiplierFrame(0f);
			WorldExtended.SetScenarioPedDensityThisMultiplierFrame(0f);
			Game.MaxWantedLevel = 0;
			Vehicle[] array = (from v in World.GetAllVehicles()
				where !((Entity)v).IsPersistent
				select v).ToArray();
			Vehicle[] array2 = Array.FindAll(array, (Vehicle v) =>
			{
				return (int)v.ClassType == 16;
			});
			Vehicle[] array3 = Array.FindAll(array, (Vehicle v) =>
			{
				return (int)v.ClassType == 21;
			});
			Vehicle[] array4 = Array.FindAll(array, (Vehicle v) =>
			{
				Ped driver = v.Driver;
				return driver != null && ((Entity)driver).Exists() && !driver.IsPlayer;
			});
			Array.ForEach(array4, (Vehicle vehicle) =>
			{
				((Entity)vehicle).Delete();
			});
			Array.ForEach(array2, (Vehicle plane) =>
			{
				Ped driver = plane.Driver;
				if (driver != null && ((Entity)driver).Exists() && !driver.IsPlayer && !((Entity)driver).IsDead)
				{
					driver.Kill();
				}
			});
			Array.ForEach(array3, (Vehicle t) =>
			{
				Function.Call(Hash.SET_TRAIN_SPEED, new InputArgument[2]
				{
					((Entity)t).Handle,
					0f
				});
			});
			ScriptExtended.TerminateScriptByName("re_prison");
			ScriptExtended.TerminateScriptByName("am_prison");
			ScriptExtended.TerminateScriptByName("gb_biker_free_prisoner");
			ScriptExtended.TerminateScriptByName("re_prisonvanbreak");
			ScriptExtended.TerminateScriptByName("am_vehicle_spawn");
			ScriptExtended.TerminateScriptByName("am_taxi");
			ScriptExtended.TerminateScriptByName("audiotest");
			ScriptExtended.TerminateScriptByName("freemode");
			ScriptExtended.TerminateScriptByName("re_prisonerlift");
			ScriptExtended.TerminateScriptByName("am_prison");
			ScriptExtended.TerminateScriptByName("re_lossantosintl");
			ScriptExtended.TerminateScriptByName("re_armybase");
			ScriptExtended.TerminateScriptByName("restrictedareas");
			ScriptExtended.TerminateScriptByName("stripclub");
			ScriptExtended.TerminateScriptByName("re_gangfight");
			ScriptExtended.TerminateScriptByName("re_gang_intimidation");
			ScriptExtended.TerminateScriptByName("spawn_activities");
			ScriptExtended.TerminateScriptByName("am_vehiclespawn");
			ScriptExtended.TerminateScriptByName("traffick_air");
			ScriptExtended.TerminateScriptByName("traffick_ground");
			ScriptExtended.TerminateScriptByName("emergencycall");
			ScriptExtended.TerminateScriptByName("emergencycalllauncher");
			ScriptExtended.TerminateScriptByName("clothes_shop_sp");
			ScriptExtended.TerminateScriptByName("gb_rob_shop");
			ScriptExtended.TerminateScriptByName("gunclub_shop");
			ScriptExtended.TerminateScriptByName("hairdo_shop_sp");
			ScriptExtended.TerminateScriptByName("re_shoprobbery");
			ScriptExtended.TerminateScriptByName("shop_controller");
			ScriptExtended.TerminateScriptByName("re_crashrescue");
			ScriptExtended.TerminateScriptByName("re_rescuehostage");
			ScriptExtended.TerminateScriptByName("fm_mission_controller");
			ScriptExtended.TerminateScriptByName("player_scene_m_shopping");
			ScriptExtended.TerminateScriptByName("shoprobberies");
			ScriptExtended.TerminateScriptByName("re_atmrobbery");
			ScriptExtended.TerminateScriptByName("ob_vend1");
			ScriptExtended.TerminateScriptByName("ob_vend2");
			Function.Call(Hash.STOP_ALARM, new InputArgument[2]
			{
				"PRISON_ALARMS",
				0
			});
			Function.Call(Hash.CLEAR_AMBIENT_ZONE_STATE, new InputArgument[3]
			{
				"AZ_COUNTRYSIDE_PRISON_01_ANNOUNCER_GENERAL",
				0,
				0
			});
			Function.Call(Hash.CLEAR_AMBIENT_ZONE_STATE, new InputArgument[3]
			{
				"AZ_COUNTRYSIDE_PRISON_01_ANNOUNCER_WARNING",
				0,
				0
			});
			int num = Function.Call<int>(Hash.GET_HASH_KEY, new InputArgument[1] { "prop_gate_prison_01" });
			Function.Call(Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, new InputArgument[7]
			{
				num,
				1845f,
				2605f,
				45f,
				false,
				0,
				0
			});
			int num2 = Function.Call<int>(Hash.GET_HASH_KEY, new InputArgument[1] { "prop_gate_prison_01" });
			Function.Call(Hash.SET_LOCKED_UNSTREAMED_IN_DOOR_OF_TYPE, new InputArgument[7]
			{
				num2,
				1819.27f,
				2608.53f,
				44.61f,
				false,
				0,
				0
			});
			if (_reset)
			{
				Function.Call(Hash.CAN_CREATE_RANDOM_COPS, new InputArgument[1] { false });
				Function.Call(Hash.SET_RANDOM_BOATS, new InputArgument[1] { false });
				Function.Call(Hash.SET_RANDOM_TRAINS, new InputArgument[1] { false });
				Function.Call(Hash.SET_GARBAGE_TRUCKS, new InputArgument[1] { false });
				Function.Call(Hash.SET_DISTANT_CARS_ENABLED, new InputArgument[1] { false });
				_reset = false;
			}
		}
		else if (!_reset)
		{
			Reset();
			_reset = true;
		}
	}
}
