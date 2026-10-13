using GTA.Math;
using GTA.Native;
using ZombiesMod.DataClasses;
using ZombiesMod.Static;

namespace ZombiesMod.Extensions;

public static class WorldExtended
{
	public static void SetParkedVehicleDensityMultiplierThisFrame(float multiplier)
	{
		Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, new InputArgument[1] { multiplier });
	}

	public static void SetVehicleDensityMultiplierThisFrame(float multiplier)
	{
		Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, new InputArgument[1] { multiplier });
	}

	public static void SetRandomVehicleDensityMultiplierThisFrame(float multiplier)
	{
		Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, new InputArgument[1] { multiplier });
	}

	public static void SetPedDensityThisMultiplierFrame(float multiplier)
	{
		Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, new InputArgument[1] { multiplier });
	}

	public static void SetScenarioPedDensityThisMultiplierFrame(float multiplier)
	{
		Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, new InputArgument[1] { multiplier });
	}

	public static void RemoveAllShockingEvents(bool toggle)
	{
		Function.Call(Hash.REMOVE_ALL_SHOCKING_EVENTS, new InputArgument[1] { toggle ? 1 : 0 });
	}

	public static void SetFrontendRadioActive(bool active)
	{
		Function.Call(Hash.SET_FRONTEND_RADIO_ACTIVE, new InputArgument[1] { active ? 1 : 0 });
	}

	public static void ClearCops(float radius = 9000f)
	{
		Vector3 playerPosition = Database.PlayerPosition;
		Function.Call(Hash.CLEAR_AREA_OF_COPS, new InputArgument[5]
		{
			playerPosition.X,
			playerPosition.Y,
			playerPosition.Z,
			radius,
			0
		});
	}

	public static void ClearAreaOfEverything(Vector3 position, float radius)
	{
		Function.Call(Hash.CLEAR_AREA_LEAVE_VEHICLE_HEALTH, new InputArgument[8]
		{
			position.X,
			position.Y,
			position.Z,
			radius,
			false,
			false,
			false,
			false
		});
	}

	public static ParticleEffect CreateParticleEffectAtCoord(Vector3 coord, string name)
	{
		Function.Call(Hash.USE_PARTICLE_FX_ASSET, new InputArgument[1] { "core" });
		int handle = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_AT_COORD, new InputArgument[12]
		{
			name,
			coord.X,
			coord.Y,
			coord.Z,
			0.0,
			0.0,
			0.0,
			1f,
			0,
			0,
			0,
			0
		});
		return new ParticleEffect(handle);
	}
}
