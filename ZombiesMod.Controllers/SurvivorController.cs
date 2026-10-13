using System;
using System.Collections.Generic;
using GTA;
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

	public bool Spawn { get; set; }

	public static SurvivorController Instance { get; private set; }

	public event OnCreatedSurvivorsEvent CreatedSurvivors;

	public SurvivorController()
	{
		Instance = this;
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
		Create();
		Destroy();
		_survivors.ForEach((Survivors s) =>
		{
			s.Update();
		});
	}

	private void Destroy()
	{
		if (!Spawn)
		{
			_survivors.ForEach((Survivors s) =>
			{
				_survivors.Remove(s);
				s.Abort();
			});
			_currentDelayTime = DateTime.UtcNow;
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
