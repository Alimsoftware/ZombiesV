using System;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using ZombiesMod.Extensions;

namespace ZombiesMod.Static;

public static class Database
{
	public static Random Random = new Random();

	public static string[] VehicleModels = new string[20]
	{
		"fusilade", "stratum", "police", "buffalo", "superd", "emperor", "picador", "dubsta", "blista", "fugitive",
		"zion", "tornado", "regina", "police2", "pigalle", "asterope", "fq2", "glendale", "schafter2", "ambulance"
	};

	public static WeaponHash[] WeaponHashes;

	public static Vector3[] AnimalSpawns;

	public static Vector3[] Shops247Locations;

	public static Ped PlayerPed => Player.Character;

	public static Player Player => Game.Player;

	public static Vehicle PlayerCurrentVehicle => PlayerPed.CurrentVehicle;

	public static PedGroup PlayerGroup => PlayerPed.PedGroup;

	public static bool PlayerIsDead => ((Entity)PlayerPed).IsDead;

	public static bool PlayerInVehicle => PlayerPed.IsInVehicle();

	public static bool PlayerIsSprinting => PlayerPed.IsSprinting;

	public static int PlayerHealth
	{
		get
		{
			return ((Entity)PlayerPed).Health;
		}
		set
		{
			((Entity)PlayerPed).Health = value;
		}
	}

	public static int PlayerMaxHealth => ((Entity)PlayerPed).MaxHealth;

	public static Vector3 PlayerPosition
	{
		get
		{
			return ((Entity)PlayerPed).Position;
		}
	}

	public static Model GetRandomVehicleModel()
	{
		Model val = new Model(VehicleModels[Random.Next(VehicleModels.Length)]);
		return val.Request(1500) ? val : (Model)((string)null);
	}

	static Database()
	{
		WeaponHashes = new WeaponHash[6]
		{
			WeaponHash.AdvancedRifle,
			WeaponHash.Pistol,
			WeaponHash.CarbineRifle,
			WeaponHash.GolfClub,
			WeaponHash.Bat,
			WeaponHash.MicroSMG
		};
		AnimalSpawns = new Vector3[6]
		{
			new Vector3(-2333.765f, 1274.093f, 326.2806f),
			new Vector3(-2583.969f, 489.153f, 218.0715f),
			new Vector3(717.0663f, 5062.837f, 360.6411f),
			new Vector3(-1536.53f, 3634.83f, 248.3539f),
			new Vector3(2516.562f, -1684.065f, 35.24468f),
			new Vector3(2825.585f, -1469.839f, 11.25044f)
		};
		Shops247Locations = new Vector3[9]
		{
			new Vector3(-3041.777f, 588.7258f, 7.908933f),
			new Vector3(-3243.759f, 1005.157f, 12.83071f),
			new Vector3(1732.932f, 6414.323f, 35.03724f),
			new Vector3(1963.272f, 3743.574f, 32.34375f),
			new Vector3(2678.908f, 3284.251f, 55.24114f),
			new Vector3(544.7951f, 2669.228f, 42.1565f),
			new Vector3(2557.156f, 384.4772f, 108.623f),
			new Vector3(377.7599f, 326.8445f, 103.5664f),
			new Vector3(29.1841f, -1346.031f, 29.49703f)
		};
	}
}
