using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using ZombiesMod.Extensions;
using ZombiesMod.Static;

namespace ZombiesMod.Scripts;

public class AnimalSpawner : SafeScript, ISpawner
{
	public const int MinAnimalsPerSpawn = 3;

	public const int MaxAnimalsPerSpawn = 10;

	public const int RespawnDistance = 200;

	private const float HerdTriggerDistance = 170f;

	private const float MinHerdDistance = 70f;

	private const float HerdDespawnDistance = 260f;

	/// <summary>Distância mínima à estrada mais próxima para a célula contar como terreno selvagem.</summary>
	private const float WildDistance = 90f;

	private const int MaxHerdSpawnsPerTick = 2;

	private readonly bool _procedural = true;

	private sealed class Herd
	{
		public int CellX;
		public int CellY;
		public int ToSpawn;
		public PedHash Species;
		public Vector3 Center;
		public readonly List<Ped> Peds = new List<Ped>();
	}

	private readonly List<Herd> _herds = new List<Herd>();

	/// <summary>Células sem terreno selvagem (cidade), avaliadas uma vez por sessão.</summary>
	private readonly HashSet<long> _unsuitable = new HashSet<long>();

	private readonly PedHash[] _possibleAnimals;

	private readonly List<Blip> _spawnBlips;

	private Dictionary<Blip, List<Ped>> _spawnMap;

	public static AnimalSpawner Instance { get; private set; }

	public bool Spawn { get; set; }

	public AnimalSpawner()
	{
		_possibleAnimals = new PedHash[5]
		{
			PedHash.Deer,
			PedHash.Boar,
			PedHash.Coyote,
			PedHash.Cow,
			PedHash.Pig
		};
		_spawnBlips = new List<Blip>();
		_spawnMap = new Dictionary<Blip, List<Ped>>();
		Instance = this;
		_procedural = ((Script)this).Settings.GetValue<bool>("spawning", "procedural_spawning", true);
		if (_procedural)
		{
			Interval = 250;
		}
		WrapEvents();
		((Script)this).Aborted += OnAborted;
	}

	private void OnAborted(object sender, EventArgs e)
	{
		Clear();
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (Spawn)
		{
			if (_procedural)
			{
				TickHerds();
				return;
			}
			CreateBlips();
			int i = 0;
			for (int count = _spawnBlips.Count; i < count; i++)
			{
				Blip val = _spawnBlips[i];
				if (!_spawnMap.ContainsKey(val))
				{
					List<Ped> value = CreateAnimals(val);
					_spawnMap.Add(val, value);
					continue;
				}
				List<Ped> list = _spawnMap[val];
				for (int num = list.Count - 1; num >= 0; num--)
				{
					Ped val2 = list[num];
					if ((Entity)(object)val2 == (Entity)null)
					{
						list.Remove(null);
					}
					else if (((Entity)val2).IsDead || !((Entity)val2).Exists())
					{
						((Entity)val2).MarkAsNoLongerNeeded();
						list.Remove(val2);
					}
				}
			}
			_spawnMap = _spawnMap.Where((KeyValuePair<Blip, List<Ped>> keyValuePair) =>
			{
				if (keyValuePair.Value.Count != 0)
				{
					return true;
				}
				float num2 = keyValuePair.Key.Position.VDist(Database.PlayerPosition);
				return !(num2 > 200f);
			}).ToDictionary((KeyValuePair<Blip, List<Ped>> keyValuePair) => keyValuePair.Key, (KeyValuePair<Blip, List<Ped>> keyValuePair) => keyValuePair.Value);
		}
		else
		{
			Clear();
		}
	}

	private List<Ped> CreateAnimals(Blip blip)
	{
		List<Ped> list = new List<Ped>();
		int num = Database.Random.Next(3, 10);
		PedHash val = _possibleAnimals[Database.Random.Next(_possibleAnimals.Length)];
		for (int i = 0; i < num; i++)
		{
			Model val2 = (Model)(val);
			Vector3 position = blip.Position;
			Ped val3 = World.CreatePed(val2, position.Around(5f));
			if (!((Entity)(object)val3 == (Entity)null))
			{
				list.Add(val3);
				val3.Task.WanderAround();
				((Entity)val3).IsPersistent = true;
				Relationships.SetRelationshipBothWays((Relationship)5, Relationships.PlayerRelationship, val3.RelationshipGroup);
			}
		}
		return list;
	}

	private void CreateBlips()
	{
		if (_spawnBlips.Count < Database.AnimalSpawns.Length)
		{
			int i = 0;
			for (int num = Database.AnimalSpawns.Length; i < num; i++)
			{
				Vector3 val = Database.AnimalSpawns[i];
				Blip val2 = World.CreateBlip(val);
				val2.Sprite = (BlipSprite)141;
				val2.Name = "Animais";
				_spawnBlips.Add(val2);
			}
		}
	}

	/// <summary>
	/// Manadas da semente: nas células marcadas, longe de estradas, a manada aparece aos poucos
	/// quando o jogador se aproxima e é largada quando ele se afasta.
	/// </summary>
	private void TickHerds()
	{
		Vector3 player = Database.PlayerPosition;
		for (int h = _herds.Count - 1; h >= 0; h--)
		{
			Herd herd = _herds[h];
			int batch = Math.Min(herd.ToSpawn, MaxHerdSpawnsPerTick);
			for (int i = 0; i < batch; i++)
			{
				herd.ToSpawn--;
				Ped animal = World.CreatePed((Model)(herd.Species), herd.Center.Around(5f));
				if (animal == null)
				{
					continue;
				}
				animal.Task.WanderAround();
				((Entity)animal).IsPersistent = true;
				Relationships.SetRelationshipBothWays((Relationship)5, Relationships.PlayerRelationship, animal.RelationshipGroup);
				herd.Peds.Add(animal);
			}
			herd.Peds.RemoveAll((Ped p) =>
			{
				if (p == null || !((Entity)p).Exists())
				{
					return true;
				}
				if (((Entity)p).IsDead)
				{
					((Entity)p).MarkAsNoLongerNeeded();
					return true;
				}
				return false;
			});
			bool gone = herd.ToSpawn == 0 && herd.Peds.Count == 0;
			if (!gone && herd.Center.VDist(player) <= HerdDespawnDistance)
			{
				continue;
			}
			// Largados, o jogo recolhe-os longe da vista, sem apagões à frente do jogador.
			foreach (Ped p in herd.Peds)
			{
				((Entity)p).IsPersistent = false;
				((Entity)p).MarkAsNoLongerNeeded();
			}
			_herds.RemoveAt(h);
		}

		int cx = WorldNoise.Cell(player.X);
		int cy = WorldNoise.Cell(player.Y);
		float cellSize = WorldNoise.CellSize;
		for (int x = cx - 1; x <= cx + 1; x++)
		{
			for (int y = cy - 1; y <= cy + 1; y++)
			{
				long key = ((long)x << 32) ^ (uint)y;
				if (_unsuitable.Contains(key) || _herds.Any((Herd e) => e.CellX == x && e.CellY == y)
					|| !WorldSeed.TryGetHerd(x, y, out int species, out int size))
				{
					continue;
				}
				float dx = (x + 0.5f) * cellSize - player.X;
				float dy = (y + 0.5f) * cellSize - player.Y;
				if (dx * dx + dy * dy > HerdTriggerDistance * HerdTriggerDistance)
				{
					continue;
				}
				Vector3 raw = new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, player.Z);
				if (World.GetNextPositionOnStreet(raw).VDist(raw) < WildDistance)
				{
					_unsuitable.Add(key);
					continue;
				}
				Vector3 ground = World.GetSafeCoordForPed(raw, false);
				if (ground == Vector3.Zero || ground.IsOnScreen() || ground.VDist(player) < MinHerdDistance)
				{
					continue;
				}
				_herds.Add(new Herd
				{
					CellX = x,
					CellY = y,
					ToSpawn = size,
					Species = _possibleAnimals[species % _possibleAnimals.Length],
					Center = ground
				});
			}
		}
	}

	private void ClearHerds()
	{
		foreach (Herd herd in _herds)
		{
			foreach (Ped p in herd.Peds)
			{
				((Entity)p).Delete();
			}
		}
		_herds.Clear();
	}

	private void Clear()
	{
		ClearHerds();
		if (_spawnBlips.Count > 0)
		{
			foreach (Blip spawnBlip in _spawnBlips)
			{
				if (_spawnMap.ContainsKey(spawnBlip))
				{
					List<Ped> list = _spawnMap[spawnBlip];
					foreach (Ped item in list)
					{
						((Entity)item).Delete();
					}
				}
				spawnBlip.Delete();
			}
		}
		_spawnMap.Clear();
	}
}
