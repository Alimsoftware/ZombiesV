using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using ZombiesMod.DataClasses;
using ZombiesMod.Extensions;
using ZombiesMod.Static;

namespace ZombiesMod.Scripts;

public class ZombieVehicleSpawner : SafeScript, ISpawner
{
	public const int SpawnBlockingDistance = 150;

	private readonly int _maxVehicles = 10;

	private readonly int _maxZombies = 30;

	private readonly int _minVehicles = 1;

	private readonly int _minZombies = 7;

	private readonly int _spawnDistance = 75;

	private readonly int _minSpawnDistance = 50;

	private readonly int _zombieHealth = 750;

	private bool _nightFall;

	private List<Ped> _peds = new List<Ped>();

	private List<Vehicle> _vehicles = new List<Vehicle>();

	private readonly VehicleClass[] _classes;

	public string[] InvalidZoneNames;

	public bool Spawn { get; set; }

	public SpawnBlocker SpawnBlocker { get; }

	private static Ped PlayerPed => Database.PlayerPed;

	private static Vector3 PlayerPosition
	{
		get
		{
			return Database.PlayerPosition;
		}
	}

	public static ZombieVehicleSpawner Instance { get; private set; }

	public ZombieVehicleSpawner()
	{
		_classes = new VehicleClass[3]
		{
			VehicleClass.OffRoad,
			VehicleClass.Muscle,
			VehicleClass.Sedans
		};
		InvalidZoneNames = new string[8] { "Los Santos International Airport", "Fort Zancudo", "Bolingbroke Penitentiary", "Davis Quartz", "Palmer-Taylor Power Station", "RON Alternates Wind Farm", "Terminal", "Humane Labs and Research" };
		SpawnBlocker = new SpawnBlocker();
		Instance = this;
		_minZombies = ((Script)this).Settings.GetValue<int>("spawning", "min_spawned_zombies", _minZombies);
		_maxZombies = ((Script)this).Settings.GetValue<int>("spawning", "max_spawned_zombies", _maxZombies);
		_minVehicles = ((Script)this).Settings.GetValue<int>("spawning", "min_spawned_vehicles", _minVehicles);
		_maxVehicles = ((Script)this).Settings.GetValue<int>("spawning", "max_spawned_vehicles", _maxVehicles);
		_spawnDistance = ((Script)this).Settings.GetValue<int>("spawning", "spawn_distance", _spawnDistance);
		_minSpawnDistance = ((Script)this).Settings.GetValue<int>("spawning", "min_spawn_distance", _minSpawnDistance);
		_zombieHealth = ((Script)this).Settings.GetValue<int>("zombies", "zombie_health", _zombieHealth);
		((Script)this).Settings.SetValue<int>("spawning", "min_spawned_zombies", _minZombies);
		((Script)this).Settings.SetValue<int>("spawning", "max_spawned_zombies", _maxZombies);
		((Script)this).Settings.SetValue<int>("spawning", "min_spawned_vehicles", _minVehicles);
		((Script)this).Settings.SetValue<int>("spawning", "max_spawned_vehicles", _maxVehicles);
		((Script)this).Settings.SetValue<int>("spawning", "spawn_distance", _spawnDistance);
		((Script)this).Settings.SetValue<int>("spawning", "min_spawn_distance", _minSpawnDistance);
		((Script)this).Settings.SetValue<int>("zombies", "zombie_health", _zombieHealth);
		((Script)this).Settings.Save();
		WrapEvents();
		((Script)this).Aborted += (object sender, EventArgs args) =>
		{
			ClearAll();
		};
		Interval = 100;
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (Spawn)
		{
			if (!MenuConrtoller.ObjectPool.AreAnyVisible)
			{
				if (ZombieCreator.IsNightFall() && !_nightFall)
				{
					UiExtended.DisplayHelpTextThisFrame(Lang.T("help.nightfall_warning"));
					_nightFall = true;
				}
				else if (!ZombieCreator.IsNightFall())
				{
					_nightFall = false;
				}
			}
			SpawnVehicles();
			SpawnPeds();
		}
		else
		{
			ClearAll();
		}
	}

	private void SpawnPeds()
	{
		_peds = _peds.Where(Exists).ToList();
		if (_peds.Count >= _maxZombies)
		{
			return;
		}
		for (int i = 0; i < _maxZombies - _peds.Count; i++)
		{
			Vector3 position = ((Entity)PlayerPed).Position;
			Vector3 val = position.Around((float)_spawnDistance);
			val = World.GetNextPositionOnStreet(val);
			if (!IsValidSpawn(val))
			{
				break;
			}
			Vector3 val2 = val.Around(5f);
			if (val2.IsOnScreen() || val2.VDist(PlayerPosition) < (float)_minSpawnDistance)
			{
				break;
			}
			Ped val3 = World.CreateRandomPed(val2);
			if (!((Entity)(object)val3 == (Entity)null))
			{
				_peds.Add(ZombieCreator.InfectPed(val3, _zombieHealth));
			}
		}
	}

	private void SpawnVehicles()
	{
		_vehicles = _vehicles.Where(Exists).ToList();
		if (_vehicles.Count >= _maxVehicles)
		{
			return;
		}
		for (int i = 0; i < _maxVehicles - _vehicles.Count; i++)
		{
			Vector3 position = ((Entity)PlayerPed).Position;
			Vector3 val = position.Around((float)_spawnDistance);
			val = World.GetNextPositionOnStreet(val);
			if (IsInvalidZone(val) || !IsValidSpawn(val))
			{
				break;
			}
			Vector3 val2 = val.Around(2.5f);
			if (val2.IsOnScreen() || val2.VDist(PlayerPosition) < (float)_minSpawnDistance)
			{
				break;
			}
			Model randomVehicleModel = Database.GetRandomVehicleModel();

			// EXEMPLO — SafeExecutor.Run<T> à volta de um spawn.
			// World.CreateVehicle é um native: pode falhar (modelo ainda não carregado,
			// posicao invalida) e a ScriptDomain abortaria o script inteiro. Com Run<T>
			// fica registado e devolve null, pelo que o null-check de baixo continua a
			// valer exactamente como estava.
			Vehicle val3 = SafeExecutor.Run("ZombieVehicleSpawner.SpawnVehicle",
				() => World.CreateVehicle(randomVehicleModel, val2), defaultValue: null);

			if (!((Entity)(object)val3 == (Entity)null))
			{
				// EXEMPLO — SafeExecutor.Run (void) à volta de um bloco de natives.
				// Apanha só o estrago cosmético: se um native falhar a meio, o veiculo
				// fica pela metade mas o _vehicles.Add() de baixo continua a correr.
				SafeExecutor.Run("ZombieVehicleSpawner.DamageVehicle", () =>
				{
					val3.EngineHealth = 0f;
					((Entity)val3).MarkAsNoLongerNeeded();
					val3.DirtLevel = 14f;
					SmashRandomWindow(val3);
					if (Database.Random.NextDouble() < 0.5)
					{
						SmashRandomWindow(val3);
					}
					if (Database.Random.NextDouble() < 0.20000000298023224)
					{
						OpenRandomDoor(val3);
					}
					((Entity)val3).Heading = Database.Random.Next(1, 360);
				});

				// Isto fica fora do SafeExecutor de proposito: e registo interno, nao um
				// native, e se nao correr o mod perde o controlo do veiculo para sempre.
				_vehicles.Add(val3);
			}
		}
	}

	private static void OpenRandomDoor(Vehicle veh)
	{
		VehicleDoor[] doors = veh.Doors.ToArray();

		// Motos e alguns veículos não têm portas: Random.Next(0) lança
		// ArgumentOutOfRangeException e rebentava o spawner todo.
		if (doors.Length == 0)
		{
			return;
		}

		VehicleDoor val = doors[Database.Random.Next(doors.Length)];
		val.Open(false, true);
	}

	private static void SmashRandomWindow(Vehicle veh)
	{
		VehicleWindowIndex[] source =
		{
			VehicleWindowIndex.FrontLeftWindow,
			VehicleWindowIndex.FrontRightWindow,
			VehicleWindowIndex.BackLeftWindow,
			VehicleWindowIndex.BackRightWindow
		};
		source = source.Where((VehicleWindowIndex v) =>
		{
			return Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, new InputArgument[2]
			{
				((Entity)veh).Handle,
				(int)v
			});
		}).ToArray();

		// Se todas as janelas já estiverem estilhaçadas, o array vem vazio e o
		// Random.Next(0) rebenta.
		if (source.Length == 0)
		{
			return;
		}

		VehicleWindowIndex val = source[Database.Random.Next(source.Length)];
		veh.Windows[val].Smash();
	}

	public bool IsInvalidZone(Vector3 spawn)
	{
		return Array.Find(InvalidZoneNames, (string z) =>
		{
			return z == World.GetZoneLocalizedName(spawn);
		}) != null;
	}

	private static bool Exists(Entity arg)
	{
		return arg != (Entity)null && arg.Exists();
	}

	private void ClearAll()
	{
		while (_peds.Count > 0)
		{
			Ped val = _peds[0];
			((Entity)val).Delete();
			_peds.RemoveAt(0);
		}
		while (_vehicles.Count > 0)
		{
			Vehicle val2 = _vehicles[0];
			((Entity)val2).Delete();
			_vehicles.RemoveAt(0);
		}
	}

	public bool IsValidSpawn(Vector3 spawnPoint)
	{
		int num = SpawnBlocker.FindIndex((Vector3 spawn) =>
		{
			return spawn.VDist(spawnPoint) < 150f;
		});
		return num <= -1;
	}
}
