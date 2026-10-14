using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using LemonUI;
using LemonUI.Menus;
using LemonUI.TimerBars;
using LemonUI.Scaleform;
using ZombiesMod.Static;
using GTA.Input;
using UI = GTA.UI;

namespace ZombiesMod.PlayerManagement;

public class PlayerStats : SafeScript
{
	public static bool UseStats = true;

	private readonly float _statDamageInterval = 5f;

	private readonly float _hungerReductionMultiplier = 0.00045f;

	private readonly float _thirstReductionMultiplier = 0.0007f;

	private readonly float _sprintReductionMultiplier = 0.05f;

	private readonly float _statSustainLength = 120f;

	private readonly List<StatDisplayItem> _statDisplay;

	private float _hungerDamageTimer;

	private float _hungerSustainTimer;

	private float _thirstDamageTimer;

	private float _thirstSustainTimer;

	private bool _removedDisplay;

	private static Ped PlayerPed => Database.PlayerPed;

	public PlayerStats()
	{
		PlayerInventory.FoodUsed += PlayerInventoryOnFoodUsed;
		_sprintReductionMultiplier = ((Script)this).Settings.GetValue<float>("stats", "sprint_reduction_multiplier", _sprintReductionMultiplier);
		_hungerReductionMultiplier = ((Script)this).Settings.GetValue<float>("stats", "hunger_reduction_multiplier", _hungerReductionMultiplier);
		_thirstReductionMultiplier = ((Script)this).Settings.GetValue<float>("stats", "thirst_reduction_multiplier", _thirstReductionMultiplier);
		_statDamageInterval = ((Script)this).Settings.GetValue<float>("stats", "stat_damage_interaval", _statDamageInterval);
		_statSustainLength = ((Script)this).Settings.GetValue<float>("stats", "stat_sustain_length", _statSustainLength);
		((Script)this).Settings.SetValue<bool>("stats", "use_stats", UseStats);
		((Script)this).Settings.SetValue<float>("stats", "sprint_reduction_multiplier", _sprintReductionMultiplier);
		((Script)this).Settings.SetValue<float>("stats", "hunger_reduction_multiplier", _hungerReductionMultiplier);
		((Script)this).Settings.SetValue<float>("stats", "thirst_reduction_multiplier", _thirstReductionMultiplier);
		((Script)this).Settings.SetValue<float>("stats", "stat_damage_interaval", _statDamageInterval);
		((Script)this).Settings.SetValue<float>("stats", "stat_sustain_length", _statSustainLength);
		((Script)this).Settings.Save();
		_statDisplay = new List<StatDisplayItem>();
		Stats stats = new Stats();
		foreach (Stat stat in stats.StatList)
		{
			StatDisplayItem statDisplayItem = new StatDisplayItem
			{
				Stat = stat,
				Bar = new TimerBarProgress(stat.Name.ToUpper())
				{
					ForegroundColor = Color.White,
					BackgroundColor = Color.Gray
				}
			};
			_statDisplay.Add(statDisplayItem);
			MenuConrtoller.BarPool.Add((TimerBar)(object)statDisplayItem.Bar);
		}
		WrapEvents();
		Interval = 10;
	}

	private void PlayerInventoryOnFoodUsed(FoodInventoryItem item, FoodType foodType)
	{
		switch (foodType)
		{
		case FoodType.Food:
			UpdateStat(item, "Fome", Lang.T("msg.hunger_satisfied"));
			break;
		case FoodType.Water:
			UpdateStat(item, "Sede", Lang.T("msg.thirst_satisfied"));
			break;
		case FoodType.SpecialFood:
			UpdateStat(item, "Fome", Lang.T("msg.hunger_satisfied"));
			UpdateStat(item, "Sede", Lang.T("msg.thirst_satisfied"), 0.15f);
			break;
		}
	}

	private void UpdateStat(IFood item, string name, string notify, float valueOverride = 0f)
	{
		StatDisplayItem statDisplayItem = _statDisplay.Find((StatDisplayItem displayItem) => displayItem.Stat.Name == name);
		statDisplayItem.Stat.Value += ((valueOverride <= 0f) ? item.RestorationAmount : valueOverride);
		statDisplayItem.Stat.Sustained = true;
		UI.Notification.Show(notify, true);
		if (statDisplayItem.Stat.Value > statDisplayItem.Stat.MaxVal)
		{
			statDisplayItem.Stat.Value = statDisplayItem.Stat.MaxVal;
		}
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (Database.PlayerIsDead)
		{
			foreach (StatDisplayItem item in _statDisplay)
			{
				item.Stat.Value = item.Stat.MaxVal;
			}
			return;
		}
		if (!UseStats)
		{
			if (_removedDisplay)
			{
				return;
			}
			foreach (StatDisplayItem item2 in _statDisplay)
			{
				MenuConrtoller.BarPool.Remove((TimerBar)(object)item2.Bar);
			}
			_removedDisplay = true;
			return;
		}
		if (_removedDisplay)
		{
			foreach (StatDisplayItem item3 in _statDisplay)
			{
				MenuConrtoller.BarPool.Add((TimerBar)(object)item3.Bar);
			}
			_removedDisplay = false;
		}
		int i = 0;
		for (int count = _statDisplay.Count; i < count; i++)
		{
			StatDisplayItem statDisplayItem = _statDisplay[i];
			Stat stat = statDisplayItem.Stat;
			// LemonUI: Progress é 0..100 (e lança fora disso); o valor do stat é 0..MaxVal.
			statDisplayItem.Bar.Progress = Math.Max(0f, Math.Min(100f, stat.Value / stat.MaxVal * 100f));
			HandleReductionStat(stat, "Fome", Lang.T("msg.hunger_critical"), _hungerReductionMultiplier, ref _hungerDamageTimer, ref _hungerSustainTimer);
			HandleReductionStat(stat, "Sede", Lang.T("msg.thirst_critical"), _thirstReductionMultiplier, ref _thirstDamageTimer, ref _thirstSustainTimer);
			HandleStamina(stat);
		}
	}

	private void HandleStamina(Stat stat)
	{
		if (stat.Name != "Vigor")
		{
			return;
		}
		if (stat.Sustained)
		{
			if (Database.PlayerIsSprinting)
			{
				if (stat.Value > 0f)
				{
					stat.Value -= Game.LastFrameTime * _sprintReductionMultiplier;
					return;
				}
				stat.Sustained = false;
				stat.Value = 0f;
			}
			else if (stat.Value < stat.MaxVal)
			{
				stat.Value += Game.LastFrameTime * (_sprintReductionMultiplier * 10f);
			}
			else
			{
				stat.Value = stat.MaxVal;
			}
		}
		else
		{
			Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)21);
			stat.Value += Game.LastFrameTime * _sprintReductionMultiplier;
			if (stat.Value >= stat.MaxVal * 0.3f)
			{
				stat.Sustained = true;
			}
		}
	}

	private void HandleReductionStat(Stat stat, string targetName, string notification, float reductionMultiplier, ref float damageTimer, ref float sustainTimer)
	{
		if (stat.Name != targetName)
		{
			return;
		}
		if (!stat.Sustained)
		{
			if (stat.Value > 0f)
			{
				stat.Value -= Game.LastFrameTime * reductionMultiplier;
				damageTimer = _statDamageInterval;
				return;
			}
			UI.Notification.Show(notification, false);
			damageTimer += Game.LastFrameTime;
			if (damageTimer >= _statDamageInterval)
			{
				PlayerPed.ApplyDamage(Database.Random.Next(3, 15));
				damageTimer = 0f;
			}
			stat.Value = 0f;
		}
		else
		{
			damageTimer = _statDamageInterval;
			sustainTimer += Game.LastFrameTime;
			if (!(sustainTimer < _statSustainLength))
			{
				sustainTimer = 0f;
				stat.Sustained = false;
			}
		}
	}
}
