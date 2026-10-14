using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI;
using LemonUI.Menus;
using LemonUI.TimerBars;
using LemonUI.Scaleform;
using ZombiesMod.Extensions;
using ZombiesMod.Static;
using ZombiesMod.Wrappers;
using GTA.Input;
using UI = GTA.UI;

namespace ZombiesMod.PlayerManagement;

public class PlayerGroupManager : SafeScript
{
	private readonly NativeMenu _pedMenu;

	private Ped _selectedPed;

	private PedCollection _peds;

	private readonly Dictionary<Ped, PedTask> _pedTasks = new Dictionary<Ped, PedTask>();

	private bool _saving;

	public Ped PlayerPed => Database.PlayerPed;

	public Vector3 PlayerPosition
	{
		get
		{
			return Database.PlayerPosition;
		}
	}

	public static PlayerGroupManager Instance { get; private set; }

	public PlayerGroupManager()
	{
		Instance = this;
		_pedMenu = new NativeMenu(Lang.T("menu.guard_title"), Lang.T("menu.select_option"));
		MenuConrtoller.ObjectPool.Add(_pedMenu);
		_pedMenu.Closed += (object sender, EventArgs e) =>
		{
			_selectedPed = null;
		};
		NativeListItem<string> tasksItem = new NativeListItem<string>(Lang.T("opt.tasks"), Lang.T("help.ped_task_desc"), Enum.GetNames(typeof(PedTask)));
		tasksItem.Activated += (object sender, EventArgs e) =>
		{
			if (!((Entity)(object)_selectedPed == (Entity)null))
			{
				PedTask index = (PedTask)tasksItem.SelectedIndex;
				SetTask(_selectedPed, index);
			}
		};
		NativeItem val = new NativeItem(Lang.T("opt.apply_nearby"), Lang.T("help.apply_nearby_desc"));
		val.Activated += (object sender, EventArgs e) =>
		{
			PedTask task = (PedTask)tasksItem.SelectedIndex;
			List<Ped> list = ((IEnumerable<Ped>)PlayerPed.PedGroup).Where((Ped ped) =>
			{
				return ((Entity)ped).Position.VDist(PlayerPosition) < 50f;
			}).ToList();
			list.ForEach((Ped ped) =>
			{
				SetTask(ped, task);
			});
		};
		NativeItem val2 = new NativeItem(Lang.T("opt.give_weapon"), Lang.T("help.give_weapon_desc"));
		val2.Activated += (object sender, EventArgs e) =>
		{
			if (!((Entity)(object)_selectedPed == (Entity)null))
			{
				TradeWeapons(PlayerPed, _selectedPed);
			}
		};
		NativeItem val3 = new NativeItem(Lang.T("opt.take_weapon"), Lang.T("help.take_weapon_desc"));
		val3.Activated += (object sender, EventArgs e) =>
		{
			if (!((Entity)(object)_selectedPed == (Entity)null))
			{
				TradeWeapons(_selectedPed, PlayerPed);
			}
		};
		NativeListItem<string> globalTasks = new NativeListItem<string>(Lang.T("opt.guard_tasks"), Lang.T("help.guard_tasks_desc"), Enum.GetNames(typeof(PedTask)));
		globalTasks.Activated += (object sender, EventArgs e) =>
		{
			PedTask task = (PedTask)globalTasks.SelectedIndex;
			List<Ped> list = ((IEnumerable<Ped>)PlayerPed.PedGroup).ToList();
			list.ForEach((Ped ped) =>
			{
				SetTask(ped, task);
			});
		};
		_pedMenu.Add((NativeItem)(object)tasksItem);
		_pedMenu.Add(val);
		_pedMenu.Add(val2);
		_pedMenu.Add(val3);
		ModController.Instance.MainMenu.Add((NativeItem)(object)globalTasks);
		WrapEvents();
		((Script)this).Aborted += OnAborted;
	}

	private void SetTask(Ped ped, PedTask task)
	{
		if (task == (PedTask)(-1) || ped.IsPlayer)
		{
			return;
		}
		if (!_pedTasks.ContainsKey(ped))
		{
			_pedTasks.Add(ped, task);
		}
		else
		{
			_pedTasks[ped] = task;
		}
		ped.Task.ClearAll();
		switch (task)
		{
		case PedTask.Chill:
			Function.Call(Hash.TASK_USE_NEAREST_SCENARIO_TO_COORD, new InputArgument[6]
			{
				((Entity)ped).Handle,
				((Entity)ped).Position.X,
				((Entity)ped).Position.Y,
				((Entity)ped).Position.Z,
				100f,
				-1
			});
			break;
		case PedTask.Combat:
			ped.Task.FightAgainstHatedTargets(100f);
			break;
		case PedTask.Guard:
			ped.Task.GuardCurrentPosition();
			break;
		case PedTask.StandStill:
			ped.Task.StandStill(-1);
			break;
		case PedTask.Leave:
		{
			ped.LeaveGroup();
			_pedTasks.Remove(ped);
			Blip currentBlip = ((Entity)ped).AttachedBlip;
			if (currentBlip != null)
			{
				currentBlip.Delete();
			}
			((Entity)ped).MarkAsNoLongerNeeded();
			EntityEventWrapper.Dispose((Entity)(object)ped);
			break;
		}
		case PedTask.VehicleFollow:
		{
			Vehicle closestVehicle = World.GetClosestVehicle(((Entity)ped).Position, 100f);
			if ((Entity)(object)closestVehicle == (Entity)null)
			{
				UI.Notification.Show(Lang.T("notify.no_vehicle_near_ped"), true);
				return;
			}
			Function.Call(Hash.TASK_VEHICLE_FOLLOW, new InputArgument[6]
			{
				((Entity)ped).Handle,
				((Entity)closestVehicle).Handle,
				((Entity)PlayerPed).Handle,
				1074528293,
				262144,
				15
			});
			break;
		}
		}
		ped.BlockPermanentEvents = task == PedTask.Follow;
	}

	private void OnAborted(object sender, EventArgs eventArgs)
	{
		PedGroup currentPedGroup = PlayerPed.PedGroup;
		List<Ped> list = ((IEnumerable<Ped>)currentPedGroup).Where((Ped ped) => !ped.IsPlayer).ToList();
		currentPedGroup.Dispose();
		while (list.Count > 0)
		{
			Ped val = list[0];
			((Entity)val).Delete();
			list.RemoveAt(0);
		}
	}

	private void OnTick(object sender, EventArgs eventArgs)
	{
		if (PlayerPed.IsInVehicle() || MenuConrtoller.ObjectPool.AreAnyVisible || PlayerPed.PedGroup.MemberCount <= 0)
		{
			return;
		}
		Ped[] nearbyPeds = World.GetNearbyPeds(PlayerPed, 1.5f);
		Ped closest = World.GetClosest<Ped>(PlayerPosition, nearbyPeds);
		if (!((Entity)(object)closest == (Entity)null) && !closest.IsInVehicle() && !(closest.PedGroup != PlayerPed.PedGroup))
		{
			Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)51);
			UiExtended.DisplayHelpTextThisFrame(Lang.T("help.configure_ped"));
			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)51))
			{
				_selectedPed = closest;
				_pedMenu.Visible = !_pedMenu.Visible;
			}
		}
	}

	public void Deserialize()
	{
		if (_peds != null)
		{
			return;
		}
		PedCollection pedCollection = SaveData.ToPeds(Serializer.Deserialize<List<PedSave>>(Config.GuardsFilePath));
		_peds = pedCollection;
		_peds.ListChanged += (PedCollection count) =>
		{
			if (!_saving)
			{
				Serializer.Serialize(Config.GuardsFilePath, SaveData.From(_peds));
			}
		};
		_peds.ToList().ForEach((PedData data) =>
		{
			Ped ped = World.CreatePed((Model)(data.Hash), data.Position);
			if (!((Entity)(object)ped == (Entity)null))
			{
				((Entity)ped).Rotation = data.Rotation;
				ped.Recruit(PlayerPed);
				data.Weapons.ForEach((Weapon w) =>
				{
					ped.Weapons.Give(w.Hash, w.Ammo, true, true);
				});
				data.Handle = ((Entity)ped).Handle;
				SetTask(ped, data.Task);
			}
		});
	}

	public void SavePeds()
	{
		if (_peds == null)
		{
			Deserialize();
		}
		List<Ped> list = PlayerPed.PedGroup.ToList(false);
		if (list.Count <= 0)
		{
			UI.Notification.Show(Lang.T("notify.no_bodyguards"), false);
			return;
		}
		List<PedData> pedDatas = _peds.ToList();
		List<PedData> list2 = list.ConvertAll((Ped ped) =>
		{
			PedData data = pedDatas.Find((PedData pedData) => pedData.Handle == ((Entity)ped).Handle);
			return UpdatePedData(ped, data);
		}).ToList();
		_saving = true;
		try
		{
			list2.ForEach((PedData data) =>
			{
				if (!_peds.Contains(data))
				{
					_peds.Add(data);
				}
			});
		}
		finally
		{
			_saving = false;
		}
		Serializer.Serialize(Config.GuardsFilePath, SaveData.From(_peds));
		UI.Notification.Show(Lang.T("notify.guards_saved"), false);
	}

	private PedData UpdatePedData(Ped ped, PedData data)
	{
		PedTask task = (_pedTasks.ContainsKey(ped) ? _pedTasks[ped] : ((PedTask)(-1)));
		IEnumerable<WeaponHash> source = ((WeaponHash[])Enum.GetValues(typeof(WeaponHash))).Where((WeaponHash hash) =>
		{
			return ped.Weapons.HasWeapon(hash);
		});
		WeaponComponentHash[] componentHashes = (WeaponComponentHash[])Enum.GetValues(typeof(WeaponComponentHash));
		List<Weapon> weapons = source.ToList().ConvertAll((WeaponHash hash) =>
		{
			GTA.Weapon weapon = ped.Weapons[hash];
			WeaponComponentHash[] components = componentHashes.Where((WeaponComponentHash h) =>
			{
				return weapon.Components[h].Active;
			}).ToArray();
			return new Weapon(weapon.Ammo, weapon.Hash, components);
		}).ToList();
		switch (data == null)
		{
		case true:
		{
			int handle = ((Entity)ped).Handle;
			Model model = ((Entity)ped).Model;
			data = new PedData(handle, model.Hash, ((Entity)ped).Rotation, ((Entity)ped).Position, task, weapons);
			break;
		}
		case false:
			data.Position = ((Entity)ped).Position;
			data.Rotation = ((Entity)ped).Rotation;
			data.Task = task;
			data.Weapons = weapons;
			break;
		}
		return data;
	}

	private static void TradeWeapons(Ped trader, Ped reviever)
	{
		if (trader.Weapons.Current == trader.Weapons[WeaponHash.Unarmed])
		{
			return;
		}
		GTA.Weapon current = trader.Weapons.Current;
		if (!reviever.Weapons.HasWeapon(current.Hash))
		{
			if (!reviever.IsPlayer)
			{
				reviever.Weapons.Drop();
			}
			GTA.Weapon val = reviever.Weapons.Give(current.Hash, 0, true, true);
			val.Ammo = current.Ammo;
			val.InfiniteAmmo = false;
			trader.Weapons.Remove(current);
		}
	}
}
