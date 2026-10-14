using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using ZombiesMod.Static;
using ZombiesMod.Wrappers;
using UI = GTA.UI;

namespace ZombiesMod.PlayerManagement;

public class PlayerVehicles : Script
{
	private VehicleCollection _vehicleCollection;

	private readonly List<Vehicle> _vehicles = new List<Vehicle>();

	public static PlayerVehicles Instance { get; private set; }

	public PlayerVehicles()
	{
		Instance = this;
		((Script)this).Aborted += OnAborted;
	}

	private void OnAborted(object sender, EventArgs eventArgs)
	{
		// Pode abortar antes de Deserialize ter corrido — sem isto, NRE a desligar.
		if (_vehicleCollection == null)
		{
			return;
		}

		_vehicleCollection.ToList().ForEach((VehicleData vehicle) =>
		{
			vehicle.Delete();
		});
	}

	public void Deserialize()
	{
		if (_vehicleCollection != null)
		{
			return;
		}
		VehicleCollection vehicleCollection;
		try
		{
			vehicleCollection = SaveData.ToVehicles(Serializer.Deserialize<List<VehicleSave>>(Config.VehicleFilePath));
		}
		catch (Exception ex)
		{
			// Ficheiro corrompido ou gravado por outra versão do mod. Sem isto a
			// excepção subia até ao manipulador da tecla e o jogador perdia a garagem
			// inteira sem uma única mensagem.
			ModLogger.Error("PlayerVehicles.Deserialize", ex);
			UI.Notification.Show(Lang.T("notify.garage_file_corrupt"), false);
			vehicleCollection = new VehicleCollection();
		}

		if (vehicleCollection == null)
		{
			vehicleCollection = new VehicleCollection();
		}
		_vehicleCollection = vehicleCollection;
		_vehicleCollection.ListChanged += (VehicleCollection sender) =>
		{
			Serialize();
		};
		foreach (VehicleData item in _vehicleCollection)
		{
			Vehicle val = World.CreateVehicle((Model)(item.Hash), item.Position);
			if ((Entity)(object)val == (Entity)null)
			{
				// continue e não break: um modelo inválido só custa esse veículo. Com
				// break, um único hash errado descarregava TODOS os restantes da sessão.
				UI.Notification.Show(Lang.T("notify.vehicle_load_failed"), false);
				continue;
			}
			val.Mods.PrimaryColor = item.PrimaryColor;
			val.Mods.SecondaryColor = item.SecondaryColor;
			((Entity)val).Health = item.Health;
			val.EngineHealth = item.EngineHealth;
			((Entity)val).Rotation = item.Rotation;
			item.Handle = ((Entity)val).Handle;
			AddKit(val, item);
			AddBlipToVehicle(val);
			_vehicles.Add(val);
			((Entity)val).IsPersistent = true;
			EntityEventWrapper entityEventWrapper = new EntityEventWrapper((Entity)(object)val);
			entityEventWrapper.Died += WrapperOnDied;
		}
	}

	private static void AddKit(Vehicle vehicle, VehicleData data)
	{
		if (data != null && !((Entity)(object)vehicle == (Entity)null))
		{
			vehicle.Mods.InstallModKit();
			data.NeonLights?.ToList().ForEach((VehicleNeonLight h) =>
			{
				vehicle.Mods.SetNeonLightsOn(h, true);
			});
			data.Mods?.ForEach((Tuple<VehicleModType, int> m) =>
			{
				vehicle.Mods[m.Item1].Index = m.Item2;
				vehicle.Mods[m.Item1].Variation = true;
			});
			data.ToggleMods?.ToList().ForEach((VehicleToggleModType h) =>
			{
				vehicle.Mods[h].IsInstalled = true;
			});
			vehicle.Mods.WindowTint = data.WindowTint;
			vehicle.Mods.WheelType = data.WheelType;
			vehicle.Mods.NeonLightsColor = data.NeonColor;
			Function.Call(Hash.SET_VEHICLE_LIVERY, new InputArgument[2]
			{
				((Entity)vehicle).Handle,
				data.Livery
			});
		}
	}

	public void Serialize(bool notify = false)
	{
		if (_vehicleCollection != null)
		{
			UpdateVehicleData();
			Serializer.Serialize(Config.VehicleFilePath, SaveData.From(_vehicleCollection));
			if (notify)
			{
				UI.Notification.Show((_vehicleCollection.Count <= 0) ? Lang.T("notify.no_vehicles") : Lang.T("notify.vehicles_saved"), false);
			}
		}
	}

	private void UpdateVehicleData()
	{
		if (_vehicleCollection.Count <= 0)
		{
			return;
		}
		_vehicleCollection.ToList().ForEach((VehicleData v) =>
		{
			Vehicle val = _vehicles.Find((Vehicle i) => ((Entity)i).Handle == v.Handle);
			if (!((Entity)(object)val == (Entity)null))
			{
				UpdateDataSpecific(v, val);
			}
		});
	}

	private static void UpdateDataSpecific(VehicleData vehicleData, Vehicle vehicle)
	{
		vehicleData.Position = ((Entity)vehicle).Position;
		vehicleData.Rotation = ((Entity)vehicle).Rotation;
		vehicleData.Health = ((Entity)vehicle).Health;
		vehicleData.EngineHealth = vehicle.EngineHealth;
		vehicleData.PrimaryColor = vehicle.Mods.PrimaryColor;
		vehicleData.SecondaryColor = vehicle.Mods.SecondaryColor;
	}

	public void SaveVehicle(Vehicle vehicle)
	{
		if (_vehicleCollection == null)
		{
			Deserialize();
		}
		VehicleData vehicleData = _vehicleCollection.ToList().Find((VehicleData v) => v.Handle == ((Entity)vehicle).Handle);
		if (vehicleData != null)
		{
			UpdateDataSpecific(vehicleData, vehicle);
			Serialize(notify: true);
			return;
		}
		VehicleNeonLight[] source = (VehicleNeonLight[])Enum.GetValues(typeof(VehicleNeonLight));
		source = ((IEnumerable<VehicleNeonLight>)source).Where((Func<VehicleNeonLight, bool>)vehicle.Mods.IsNeonLightsOn).ToArray();
		VehicleModType[] source2 = (VehicleModType[])Enum.GetValues(typeof(VehicleModType));
		List<Tuple<VehicleModType, int>> mods = new List<Tuple<VehicleModType, int>>();
		source2.ToList().ForEach((VehicleModType h) =>
		{
			int mod = vehicle.Mods[h].Index;
			if (mod != -1)
			{
				mods.Add(new Tuple<VehicleModType, int>(h, mod));
			}
		});
		VehicleToggleModType[] source3 = (VehicleToggleModType[])Enum.GetValues(typeof(VehicleToggleModType));
		source3 = ((IEnumerable<VehicleToggleModType>)source3).Where((Func<VehicleToggleModType, bool>)(t => vehicle.Mods[t].IsInstalled)).ToArray();
		int handle = ((Entity)vehicle).Handle;
		Model model = ((Entity)vehicle).Model;
		VehicleData item = new VehicleData(handle, model.Hash, ((Entity)vehicle).Rotation, ((Entity)vehicle).Position, vehicle.Mods.PrimaryColor, vehicle.Mods.SecondaryColor, ((Entity)vehicle).Health, vehicle.EngineHealth, ((Entity)vehicle).Heading, source, mods, source3, vehicle.Mods.WindowTint, vehicle.Mods.WheelType, vehicle.Mods.NeonLightsColor, Function.Call<int>(Hash.GET_VEHICLE_LIVERY, new InputArgument[1] { ((Entity)vehicle).Handle }), Function.Call<bool>(Hash.GET_VEHICLE_MOD_VARIATION, new InputArgument[2]
		{
			((Entity)vehicle).Handle,
			23
		}), Function.Call<bool>(Hash.GET_VEHICLE_MOD_VARIATION, new InputArgument[2]
		{
			((Entity)vehicle).Handle,
			24
		}));
		_vehicleCollection.Add(item);
		_vehicles.Add(vehicle);
		((Entity)vehicle).IsPersistent = true;
		EntityEventWrapper entityEventWrapper = new EntityEventWrapper((Entity)(object)vehicle);
		entityEventWrapper.Died += WrapperOnDied;
		AddBlipToVehicle(vehicle);
	}

	private static void AddBlipToVehicle(Vehicle vehicle)
	{
		Blip val = ((Entity)vehicle).AddBlip();
		val.Sprite = GetSprite(vehicle);
		val.Color = (BlipColor)58;
		val.Name = vehicle.DisplayName;
		val.Scale = 0.85f;
	}

	private static BlipSprite GetSprite(Vehicle vehicle)
	{
		return (BlipSprite)(((int)vehicle.ClassType == 8) ? 226 : (((int)vehicle.ClassType == 14) ? 410 : (((int)vehicle.ClassType == 15) ? 64 : (((int)vehicle.ClassType == 16) ? 423 : 225))));
	}

	private void WrapperOnDied(EntityEventWrapper sender, Entity entity)
	{
		UI.Notification.Show(Lang.T("notify.vehicle_destroyed"), false);
		_vehicleCollection.Remove(_vehicleCollection.ToList().Find((VehicleData v) => v.Handle == entity.Handle));
		Blip currentBlip = entity.AttachedBlip;
		if (currentBlip != null)
		{
			currentBlip.Delete();
		}
		sender.Dispose();
	}
}
