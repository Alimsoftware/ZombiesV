using System;
using GTA;
using GTA.Math;
using GTA.Native;
using ZombiesMod.Extensions;
using ZombiesMod.PlayerManagement;
using ZombiesMod.Static;
using GTA.Input;
using UI = GTA.UI;

namespace ZombiesMod.Scripts;

public class VehicleRepair : SafeScript
{
	private Vehicle _selectedVehicle;

	private InventoryItemBase _item;

	private readonly int _repairTimeMs = 7500;

	private static Ped PlayerPed => Database.PlayerPed;

	public VehicleRepair()
	{
		_repairTimeMs = ((Script)this).Settings.GetValue<int>("interaction", "vehicle_repair_time_ms", _repairTimeMs);
		((Script)this).Settings.SetValue<int>("interaction", "vehicle_repair_time_ms", _repairTimeMs);
		((Script)this).Settings.Save();
		WrapEvents();
		((Script)this).Aborted += OnAborted;
	}

	private static void OnAborted(object sender, EventArgs e)
	{
		PlayerPed.Task.ClearAll();
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (Database.PlayerInVehicle)
		{
			return;
		}
		Vehicle closestVehicle = World.GetClosestVehicle(Database.PlayerPosition, 20f);
		if (_item == null)
		{
			_item = PlayerInventory.Instance.ItemFromName("Vehicle Repair Kit");
		}
		if ((Entity)(object)_selectedVehicle != (Entity)null)
		{
			Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)24);
			UiExtended.DisplayHelpTextThisFrame(Lang.T("help.repair_cancel"));
			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)24))
			{
				PlayerPed.Task.ClearAllImmediately();
				_selectedVehicle.Doors[VehicleDoorIndex.Hood].Close(false);
				_selectedVehicle = null;
			}
			else if (PlayerPed.TaskSequenceProgress == -1)
			{
				_selectedVehicle.EngineHealth = 1000f;
				_selectedVehicle.Doors[VehicleDoorIndex.Hood].Close(false);
				_selectedVehicle = null;
				PlayerInventory.Instance.AddItem(_item, -1, ItemType.Item);
				UI.Notification.Show(Lang.T("notify.repair_kit_used"), false);
			}
		}
		else
		{
			if (!((Entity)(object)closestVehicle != (Entity)null))
			{
				return;
			}
			Model model = ((Entity)closestVehicle).Model;
			if (!model.IsCar || !(closestVehicle.EngineHealth < 1000f) || MenuConrtoller.ObjectPool.AreAnyVisible || ((Entity)closestVehicle).IsUpsideDown || !((Entity)closestVehicle).Bones.Contains("engine"))
			{
				return;
			}
			Vector3 boneCoord = ((Entity)closestVehicle).Bones["engine"].Position;
			if (boneCoord == Vector3.Zero || !((Entity)PlayerPed).IsInRange(boneCoord, 1.5f))
			{
				return;
			}
			if (!PlayerInventory.Instance.HasItem(_item, ItemType.Item))
			{
				UiExtended.DisplayHelpTextThisFrame(Lang.T("help.repair_kit_needed"));
				return;
			}
			Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)51);
			UiExtended.DisplayHelpTextThisFrame(Lang.T("help.repair_engine"));
			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)51))
			{
				closestVehicle.Doors[VehicleDoorIndex.Hood].Open(false, false);
				PlayerPed.Weapons.Select(WeaponHash.Unarmed, true);
				Vector3 val = boneCoord + ((Entity)closestVehicle).ForwardVector;
				Vector3 val2 = ((Entity)closestVehicle).Position - Database.PlayerPosition;
				float num = val2.ToHeading();
				TaskSequence val3 = new TaskSequence();
				val3.AddTask.ClearAllImmediately();
				val3.AddTask.GoTo(val, 1500);
				val3.AddTask.AchieveHeading(num, 2000);
				val3.AddTask.PlayAnimation("mp_intro_seq@", "mp_mech_fix", 8f, -8f, _repairTimeMs, (AnimationFlags)1, 1f);
				val3.AddTask.ClearAll();
				val3.Close();
				PlayerPed.Task.PerformSequence(val3);
				val3.Dispose();
				_selectedVehicle = closestVehicle;
			}
		}
	}
}
