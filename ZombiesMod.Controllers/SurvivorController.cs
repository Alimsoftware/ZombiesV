using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using ZombiesMod.Extensions;
using ZombiesMod.DataClasses;
using ZombiesMod.Static;
using ZombiesMod.SurvivorTypes;

namespace ZombiesMod.Controllers;

public class SurvivorController : SafeScript, ISpawner
{
	public delegate void OnCreatedSurvivorsEvent();

	private readonly List<Survivors> _survivors = new List<Survivors>();

	private readonly int _survivorInterval = 30;

	private readonly float _survivorSpawnChance = 0.7f;

	private readonly int _merryweatherTimeout = 120;

	private DateTime _currentDelayTime;

	private const float EncounterTriggerDistance = 220f;

	private const float EncounterMinDistance = 80f;

	private readonly bool _procedural = true;

	private DateTime _nextScan;

	/// <summary>Célula (x, y) de cada encontro procedural ativo.</summary>
	private readonly Dictionary<Survivors, int[]> _cells = new Dictionary<Survivors, int[]>();

	/// <summary>Células cujo encontro não conseguiu nascer nesta sessão (evita tentar a cada segundo).</summary>
	private readonly HashSet<long> _failed = new HashSet<long>();

	public bool Spawn { get; set; }

	public static SurvivorController Instance { get; private set; }

	public event OnCreatedSurvivorsEvent CreatedSurvivors;

	public SurvivorController()
	{
		Instance = this;
		_procedural = ((Script)this).Settings.GetValue<bool>("spawning", "procedural_spawning", true);
		_survivorInterval = ((Script)this).Settings.GetValue<int>("survivors", "survivor_interval", _survivorInterval);
		_survivorSpawnChance = ((Script)this).Settings.GetValue<float>("survivors", "survivor_spawn_chance", _survivorSpawnChance);
		_merryweatherTimeout = ((Script)this).Settings.GetValue<int>("survivors", "merryweather_timeout", _merryweatherTimeout);
		((Script)this).Settings.SetValue<int>("survivors", "survivor_interval", _survivorInterval);
		((Script)this).Settings.SetValue<float>("survivors", "survivor_spawn_chance", _survivorSpawnChance);
		((Script)this).Settings.SetValue<int>("survivors", "merryweather_timeout", _merryweatherTimeout);
		((Script)this).Settings.Save();
		WrapEvents();
		((Script)this).Aborted += (object sender, EventArgs args) =>
		{
			_survivors.ForEach((Survivors s) =>
			{
				s.Abort();
			});
		};
		CreatedSurvivors += OnCreatedSurvivors;
	}

	private void OnCreatedSurvivors()
	{
		bool flag = Database.Random.NextDouble() <= (double)_survivorSpawnChance;
		EventTypes[] array = (EventTypes[])Enum.GetValues(typeof(EventTypes));
		EventTypes eventTypes = array[Database.Random.Next(array.Length)];
		Survivors survivors = null;
		switch (eventTypes)
		{
		case EventTypes.Friendly:
		{
			FriendlySurvivors survivors4 = new FriendlySurvivors();
			survivors = TryCreateEvent(survivors4);
			break;
		}
		case EventTypes.Hostile:
			if (Database.Random.NextDouble() <= 0.20000000298023224)
			{
				HostileSurvivors survivors3 = new HostileSurvivors();
				survivors = TryCreateEvent(survivors3);
			}
			break;
		case EventTypes.Merryweather:
		{
			MerryweatherSurvivors survivors2 = new MerryweatherSurvivors(_merryweatherTimeout);
			survivors = TryCreateEvent(survivors2);
			break;
		}
		}
		if (survivors != null)
		{
			_survivors.Add(survivors);
			survivors.SpawnEntities();
			survivors.Completed += (Survivors survivors5) =>
			{
				SetDelayTime();
				survivors5.CleanUp();
				_survivors.Remove(survivors5);
			};
		}
	}

	private Survivors TryCreateEvent(Survivors survivors)
	{
		Survivors result = null;
		if (_survivors.FindIndex((Survivors s) => s.GetType() == survivors.GetType()) <= -1)
		{
			result = survivors;
		}
		return result;
	}

	private void OnTick(object sender, EventArgs eventArgs)
	{
		if (_procedural && Spawn)
		{
			ProcessEncounters();
		}
		else
		{
			Create();
		}
		Destroy();
		// Cópia: Update() pode completar o evento e o handler mexe na lista.
		foreach (Survivors s in _survivors.ToArray())
		{
			s.Update();
		}
	}

	private void Destroy()
	{
		if (!Spawn)
		{
			foreach (Survivors s in _survivors.ToArray())
			{
				_survivors.Remove(s);
				_cells.Remove(s);
				s.Abort();
			}
			_currentDelayTime = DateTime.UtcNow;
		}
	}

	/// <summary>
	/// Encontros da semente: acampamentos, hostis e patrulhas nas células que a semente marca.
	/// Nascem quando o jogador se aproxima; ficam resolvidos (gravados no World.json) quando o
	/// evento termina, e não voltam.
	/// </summary>
	private void ProcessEncounters()
	{
		DateTime now = DateTime.UtcNow;
		if (now < _nextScan)
		{
			return;
		}
		_nextScan = now.AddSeconds(1);
		Vector3 player = Database.PlayerPosition;
		int cx = WorldNoise.Cell(player.X);
		int cy = WorldNoise.Cell(player.Y);
		float cellSize = WorldNoise.CellSize;
		for (int x = cx - 1; x <= cx + 1; x++)
		{
			for (int y = cy - 1; y <= cy + 1; y++)
			{
				CellContent content = WorldSeed.Content(x, y);
				Type type = content == CellContent.Friendly ? typeof(FriendlySurvivors)
					: content == CellContent.Hostile ? typeof(HostileSurvivors)
					: content == CellContent.Merryweather ? typeof(MerryweatherSurvivors)
					: null;
				long key = ((long)x << 32) ^ (uint)y;
				if (type == null || _failed.Contains(key) || WorldSeed.IsCleared("e", x, y)
					|| _cells.Values.Any((int[] c) => c[0] == x && c[1] == y)
					|| _survivors.Any((Survivors s) => s.GetType() == type))
				{
					continue;
				}
				float dx = (x + 0.5f) * cellSize - player.X;
				float dy = (y + 0.5f) * cellSize - player.Y;
				if (dx * dx + dy * dy > EncounterTriggerDistance * EncounterTriggerDistance)
				{
					continue;
				}
				Vector3 center = World.GetNextPositionOnStreet(new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, player.Z));
				float distance = center.VDist(player);
				if (distance > EncounterTriggerDistance || distance < EncounterMinDistance || center.IsOnScreen())
				{
					continue;
				}
				Survivors survivors = content == CellContent.Friendly ? new FriendlySurvivors()
					: content == CellContent.Hostile ? (Survivors)new HostileSurvivors()
					: new MerryweatherSurvivors(_merryweatherTimeout);
				survivors.Origin = center;
				bool spawning = true;
				bool failed = false;
				int cellX = x;
				int cellY = y;
				survivors.Completed += (Survivors done) =>
				{
					if (spawning)
					{
						// Terminou ainda a nascer: ponto inválido, nada foi resolvido.
						failed = true;
						return;
					}
					done.CleanUp();
					_survivors.Remove(done);
					_cells.Remove(done);
					WorldSeed.MarkCleared("e", cellX, cellY);
				};
				survivors.SpawnEntities();
				spawning = false;
				if (failed)
				{
					survivors.Abort();
					_failed.Add(key);
					continue;
				}
				_survivors.Add(survivors);
				_cells[survivors] = new int[2] { x, y };
			}
		}
	}

	private void Create()
	{
		if (Spawn)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (!(utcNow <= _currentDelayTime))
			{
				CreatedSurvivors?.Invoke();
				SetDelayTime();
			}
		}
	}

	private void SetDelayTime()
	{
		_currentDelayTime = DateTime.UtcNow + new TimeSpan(0, 0, _survivorInterval);
	}
}
