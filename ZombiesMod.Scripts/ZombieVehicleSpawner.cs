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

    // Distância a que uma horda da semente acorda; abaixo dos 120 m em que o ZombiePed apaga o que está longe.
    private const float HordeTriggerDistance = 110f;

    private readonly bool _procedural = true;

    // Orçamento por tick (100 ms): povoar aos poucos evita o soluço de criar dezenas de peds num frame.
    private const int MaxPedSpawnsPerTick = 2;

    private const int MaxVehicleSpawnsPerTick = 1;

    private const int MaxHordeSpawnsPerTick = 3;

    /// <summary>Fração de uma horda que tem de morrer às mãos do jogador para a célula ficar limpa.</summary>
    private const float HordeClearedRatio = 0.8f;

    private sealed class Horde
    {
        public int CellX;
        public int CellY;
        public int ToSpawn;
        public int Spawned;
        public Vector3 Center;
        public readonly List<Ped> Peds = new List<Ped>();
        public readonly HashSet<int> Killed = new HashSet<int>();
    }

    private readonly List<Horde> _hordes = new List<Horde>();

    private List<Ped> _peds = new List<Ped>();

    private List<Vehicle> _vehicles = new List<Vehicle>();

    private readonly VehicleClass[] _classes;

    public string[] InvalidZoneNames;

    public bool Spawn { get; set; }

    public SpawnBlocker SpawnBlocker { get; }

    private static Ped PlayerPed => Database.PlayerPed;

    private static Vector3 PlayerPosition
    {
        get { return Database.PlayerPosition; }
    }

    public static ZombieVehicleSpawner Instance { get; private set; }

    public ZombieVehicleSpawner()
    {
        _classes = new VehicleClass[3]
        {
            VehicleClass.OffRoad,
            VehicleClass.Muscle,
            VehicleClass.Sedans,
            //VehicleClass.Compacts,
            //VehicleClass.Utility
        };
        InvalidZoneNames = new string[8]
        {
            "Los Santos International Airport",
            "Fort Zancudo",
            "Bolingbroke Penitentiary",
            "Davis Quartz",
            "Palmer-Taylor Power Station",
            "RON Alternates Wind Farm",
            "Terminal",
            "Humane Labs and Research",
        };
        SpawnBlocker = new SpawnBlocker();
        Instance = this;

        _minZombies = Settings.GetValue("spawning", "min_spawned_zombies", _minZombies);
        _maxZombies = Settings.GetValue("spawning", "max_spawned_zombies", _maxZombies);
        _minVehicles = Settings.GetValue("spawning", "min_spawned_vehicles", _minVehicles);
        _maxVehicles = Settings.GetValue("spawning", "max_spawned_vehicles", _maxVehicles);
        _spawnDistance = Settings.GetValue("spawning", "spawn_distance", _spawnDistance);
        _minSpawnDistance = Settings.GetValue("spawning", "min_spawn_distance", _minSpawnDistance);
        _zombieHealth = Settings.GetValue("zombies", "zombie_health", _zombieHealth);
        _procedural = Settings.GetValue("spawning", "procedural_spawning", _procedural);

        Settings.SetValue("spawning", "min_spawned_zombies", _minZombies);
        Settings.SetValue("spawning", "max_spawned_zombies", _maxZombies);
        Settings.SetValue("spawning", "min_spawned_vehicles", _minVehicles);
        Settings.SetValue("spawning", "max_spawned_vehicles", _maxVehicles);
        Settings.SetValue("spawning", "spawn_distance", _spawnDistance);
        Settings.SetValue("spawning", "min_spawn_distance", _minSpawnDistance);
        Settings.SetValue("zombies", "zombie_health", _zombieHealth);
        Settings.SetValue("spawning", "procedural_spawning", _procedural);
        Settings.Save();
        WrapEvents();
        Aborted += (sender, args) =>
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
        int cap = _maxZombies;
        if (_procedural)
        {
            // A população máxima acompanha a densidade que a semente dá a este sítio.
            cap = Math.Max(2, (int)Math.Round(_maxZombies * WorldSeed.Density(PlayerPosition)));
            UpdateHordes();
            TriggerHordes();
        }
        if (_peds.Count >= cap)
        {
            return;
        }
        int need = Math.Min(cap - _peds.Count, MaxPedSpawnsPerTick);
        for (int i = 0; i < need; i++)
        {
            Vector3 position = PlayerPed.Position;
            Vector3 spawnPoint = position.Around(_spawnDistance);
            spawnPoint = World.GetNextPositionOnStreet(spawnPoint);
            if (!IsValidSpawn(spawnPoint))
            {
                break;
            }
            // Aceita o ponto com probabilidade proporcional à densidade local: zonas mortas ficam vazias.
            if (_procedural && Database.Random.NextDouble() > WorldSeed.Density(spawnPoint) / WorldNoise.MaxDensity)
            {
                continue;
            }
            Vector3 around = spawnPoint.Around(5f);
            if (around.IsOnScreen() || around.VDist(PlayerPosition) < _minSpawnDistance)
            {
                break;
            }
            Ped randomPed = World.CreateRandomPed(around);
            // Exists(): CreateRandomPed pode devolver um ped com handle 0 que != null deixa passar.
            if (randomPed != null && randomPed.Exists())
            {
                _peds.Add(ZombieCreator.InfectPed(randomPed, _zombieHealth));
            }
        }
    }

    /// <summary>
    /// Hordas da semente: nas células vizinhas que a semente marca com uma (e que o jogador
    /// ainda não limpou), a horda aparece quando o jogador chega a menos de HordeTriggerDistance.
    /// </summary>
    private void TriggerHordes()
    {
        Vector3 player = PlayerPosition;
        int cx = WorldNoise.Cell(player.X);
        int cy = WorldNoise.Cell(player.Y);
        for (int x = cx - 1; x <= cx + 1; x++)
        {
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                if (_hordes.Any(h => h.CellX == x && h.CellY == y)
                    || WorldSeed.IsCleared("h", x, y)
                    || !WorldSeed.TryGetHorde(x, y, out int size))
                {
                    continue;
                }
                float cellSize = WorldNoise.CellSize;
                Vector3 center = World.GetNextPositionOnStreet(new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, player.Z));
                float distance = center.VDist(player);
                if (distance > HordeTriggerDistance || distance < _minSpawnDistance || center.IsOnScreen()
                    || IsInvalidZone(center) || !IsValidSpawn(center))
                {
                    continue;
                }
                _hordes.Add(new Horde { CellX = x, CellY = y, ToSpawn = size, Center = center });
                ModLogger.Info("WorldSeed", "horda de " + size + " na célula " + x + "," + y + ".");
            }
        }
    }

    /// <summary>
    /// Faz nascer as hordas aos poucos e vigia-as. A célula só fica "limpa" (gravada no World.json)
    /// se o jogador matou a maioria; zumbis que se perderam ao longe não contam, e a horda volta.
    /// </summary>
    private void UpdateHordes()
    {
        for (int h = _hordes.Count - 1; h >= 0; h--)
        {
            Horde horde = _hordes[h];
            int batch = Math.Min(horde.ToSpawn, MaxHordeSpawnsPerTick);
            for (int i = 0; i < batch; i++)
            {
                horde.ToSpawn--;
                Ped ped = World.CreateRandomPed(horde.Center.Around(8f));
                if (ped == null || !ped.Exists())
                {
                    continue;
                }
                Ped zombie = ZombieCreator.InfectPed(ped, _zombieHealth);
                horde.Peds.Add(zombie);
                horde.Spawned++;
                _peds.Add(zombie);
            }
            horde.Peds.RemoveAll(p =>
            {
                if (!Exists(p))
                {
                    return true;
                }
                if (p.IsDead)
                {
                    horde.Killed.Add(p.Handle);
                }
                return false;
            });
            if (horde.ToSpawn > 0 || horde.Peds.Count > 0)
            {
                continue;
            }
            if (horde.Spawned > 0 && horde.Killed.Count >= (int)Math.Ceiling(horde.Spawned * HordeClearedRatio))
            {
                WorldSeed.MarkCleared("h", horde.CellX, horde.CellY);
                ModLogger.Info("WorldSeed", "horda da célula " + horde.CellX + "," + horde.CellY + " limpa.");
            }
            _hordes.RemoveAt(h);
        }
    }

    private void SpawnVehicles()
    {
        _vehicles = _vehicles.Where(Exists).ToList();
        if (_vehicles.Count >= _maxVehicles)
        {
            return;
        }
        int need = Math.Min(_maxVehicles - _vehicles.Count, MaxVehicleSpawnsPerTick);
        for (int i = 0; i < need; i++)
        {
            Vector3 position = PlayerPed.Position;
            Vector3 spawnPoint = position.Around(_spawnDistance);
            spawnPoint = World.GetNextPositionOnStreet(spawnPoint);
            if (IsInvalidZone(spawnPoint) || !IsValidSpawn(spawnPoint))
            {
                break;
            }
            Vector3 vehiclePosition = spawnPoint.Around(2.5f);
            if (
                vehiclePosition.IsOnScreen()
                || vehiclePosition.VDist(PlayerPosition) < _minSpawnDistance
            )
            {
                break;
            }
            Model randomVehicleModel = Database.GetRandomVehicleModel();

            // EXEMPLO — SafeExecutor.Run<T> à volta de um spawn.
            // World.CreateVehicle é um native: pode falhar (modelo ainda não carregado,
            // posicao invalida) e a ScriptDomain abortaria o script inteiro. Com Run<T>
            // fica registado e devolve null, pelo que o null-check de baixo continua a
            // valer exactamente como estava.
            Vehicle randomVehicle = SafeExecutor.Run(
                "ZombieVehicleSpawner.SpawnVehicle",
                () => World.CreateVehicle(randomVehicleModel, vehiclePosition),
                defaultValue: null
            );

            if (randomVehicle != null)
            {
                // EXEMPLO — SafeExecutor.Run (void) à volta de um bloco de natives.
                // Apanha só o estrago cosmético: se um native falhar a meio, o veiculo
                // fica pela metade mas o _vehicles.Add() de baixo continua a correr.
                SafeExecutor.Run(
                    "ZombieVehicleSpawner.DamageVehicle",
                    () =>
                    {
                        randomVehicle.EngineHealth = 0f;
                        randomVehicle.MarkAsNoLongerNeeded();
                        randomVehicle.DirtLevel = 14f;
                        SmashRandomWindow(randomVehicle);
                        if (Database.Random.NextDouble() < 0.5)
                        {
                            SmashRandomWindow(randomVehicle);
                        }
                        if (Database.Random.NextDouble() < 0.20000000298023224)
                        {
                            OpenRandomDoor(randomVehicle);
                        }
                        randomVehicle.Heading = Database.Random.Next(1, 360);
                    }
                );

                // Isto fica fora do SafeExecutor de proposito: e registo interno, nao um
                // native, e se nao correr o mod perde o controlo do veiculo para sempre.
                _vehicles.Add(randomVehicle);
            }
        }
    }

    private static void OpenRandomDoor(Vehicle veh)
    {
        VehicleDoor[] doors = veh.Doors.ToArray();

        var availableDoors = doors.Where(door => !door.IsOpen).ToArray();
        // Motos e alguns veículos não têm portas: Random.Next(0) lança
        // ArgumentOutOfRangeException e rebentava o spawner todo.
        if (availableDoors.Length == 0)
        {
            return;
        }

        VehicleDoor randomDoor = availableDoors[Database.Random.Next(availableDoors.Length)];
        randomDoor.Open(false, true);
    }

    private static void SmashRandomWindow(Vehicle vehicle)
    {
        // Criar uma lista para armazenar as janelas intactas
        List<VehicleWindow> intactWindows = new List<VehicleWindow>();

        // Iterar pelas janelas e verificar se estão intactas
        foreach (VehicleWindowIndex windowIndex in Enum.GetValues(typeof(VehicleWindowIndex)))
        {
            var window = vehicle.Windows[windowIndex];

            if (window.IsIntact) // Verifica se a janela está intacta
            {
                intactWindows.Add(window);
            }
        }

        // Se houver janelas intactas
        if (intactWindows.Count > 0)
        {
            // Seleciona uma janela intacta aleatória
            var randomWindow = intactWindows[Database.Random.Next(intactWindows.Count)];

            // Quebra a janela
            randomWindow.Smash();
        }
    }

    public bool IsInvalidZone(Vector3 spawn)
    {
        return InvalidZoneNames.Contains(World.GetZoneLocalizedName(spawn));
    }

    private static bool Exists(Entity arg)
    {
        return arg != null && arg.Exists();
    }

    private void ClearAll()
    {
        while (_peds.Count > 0)
        {
            Ped ped = _peds[0];
            ped.Delete();
            _peds.RemoveAt(0);
        }
        // while (_vehicles.Count > 0)
        // {
        //     Vehicle vehicle = _vehicles[0];
        //     vehicle.Delete();
        //     _vehicles.RemoveAt(0);
        // }
        foreach (var vehicle in _vehicles)
        {
            vehicle?.Delete();
        }
        _vehicles.Clear();
        _hordes.Clear();
    }

    public bool IsValidSpawn(Vector3 spawnPoint)
    {
        return SpawnBlocker.FindIndex(spawn => spawn.VDist(spawnPoint) < 150.0) <= -1;
    }
}
