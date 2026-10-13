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

	private void Clear()
	{
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
