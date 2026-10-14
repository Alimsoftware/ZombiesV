using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using ZombiesMod.PlayerManagement;

namespace ZombiesMod.Static;

// Formato dos saves: só números, texto e listas. O catálogo (receitas, menus, lambdas)
// vive no código e é recriado a cada arranque; o save só guarda o que o jogador tem.

public class V3Save
{
	public float X;
	public float Y;
	public float Z;
}

public class WeaponSave
{
	public int Ammo;
	public int Hash;
	public int[] Components = new int[0];
}

public class InventorySave
{
	public bool DeveloperMode;
	public Dictionary<string, int> Amounts = new Dictionary<string, int>();
}

public class MapPropSave
{
	public string Id;
	public string PropName;
	public int BlipSprite;
	public int BlipColor;
	public V3Save GroundOffset;
	public bool Interactable;
	public bool IsDoor;
	public bool CanBePickedUp;
	public bool IsSnappable;
	public V3Save Rotation;
	public V3Save Position;
	public List<WeaponSave> Weapons;
}

public class MapSave
{
	public List<MapPropSave> Props = new List<MapPropSave>();
}

public class VehicleSave
{
	public int Hash;
	public V3Save Rotation;
	public V3Save Position;
	public int PrimaryColor;
	public int SecondaryColor;
	public int Health;
	public float EngineHealth;
	public float Heading;
	public int[] NeonLights = new int[0];
	public int[] ModTypes = new int[0];
	public int[] ModIndexes = new int[0];
	public int[] ToggleMods = new int[0];
	public int WindowTint;
	public int WheelType;
	public int NeonColor;
	public int Livery;
	public bool Wheels1;
	public bool Wheels2;
}

public class PedSave
{
	public int Hash;
	public V3Save Rotation;
	public V3Save Position;
	public int Task;
	public List<WeaponSave> Weapons;
}

public static class SaveData
{
	// Tetos para um ficheiro adulterado não conseguir pedir milhares de entidades.
	private const int MaxProps = 5000;
	private const int MaxVehicles = 200;
	private const int MaxPeds = 200;
	private const int MaxWeapons = 64;
	private const int MaxInts = 256;
	private const int MaxText = 128;

	private static V3Save V(Vector3 v)
	{
		return new V3Save { X = v.X, Y = v.Y, Z = v.Z };
	}

	private static Vector3 V(V3Save v)
	{
		return v == null ? Vector3.Zero : new Vector3(v.X, v.Y, v.Z);
	}

	private static bool Text(string s)
	{
		return !string.IsNullOrWhiteSpace(s) && s.Length <= MaxText;
	}

	private static int[] Ints(int[] values)
	{
		return (values ?? new int[0]).Take(MaxInts).ToArray();
	}

	// ------------------------------------------------------------------ inventário

	public static InventorySave From(Inventory inventory)
	{
		InventorySave save = new InventorySave { DeveloperMode = inventory.DeveloperMode };
		foreach (InventoryItemBase item in inventory.Items.Concat(inventory.Resources))
		{
			save.Amounts[item.Id] = item.Amount;
		}
		return save;
	}

	/// <summary>Aplica as quantidades a um inventário já montado; ids desconhecidos são ignorados.</summary>
	public static void Apply(Inventory inventory, InventorySave save)
	{
		if (save == null || save.Amounts == null)
		{
			return;
		}
		inventory.DeveloperMode = save.DeveloperMode;
		foreach (InventoryItemBase item in inventory.Items.Concat(inventory.Resources))
		{
			if (item.Id != null && save.Amounts.TryGetValue(item.Id, out int amount))
			{
				item.Amount = Math.Max(0, Math.Min(amount, item.MaxAmount));
			}
		}
	}

	// ---------------------------------------------------------------------- armas

	private static List<WeaponSave> From(List<Weapon> weapons)
	{
		if (weapons == null)
		{
			return null;
		}
		return weapons.Select(w => new WeaponSave
		{
			Ammo = w.Ammo,
			Hash = unchecked((int)w.Hash),
			Components = (w.Components ?? new WeaponComponentHash[0]).Select(c => unchecked((int)c)).ToArray()
		}).ToList();
	}

	private static List<Weapon> ToWeapons(List<WeaponSave> saves)
	{
		if (saves == null)
		{
			return null;
		}
		return saves.Where(s => s != null).Take(MaxWeapons).Select(s => new Weapon(
			Math.Max(0, s.Ammo),
			unchecked((WeaponHash)s.Hash),
			Ints(s.Components).Select(c => unchecked((WeaponComponentHash)c)).ToArray())).ToList();
	}

	// ----------------------------------------------------------------------- mapa

	public static MapSave From(Map map)
	{
		MapSave save = new MapSave();
		foreach (MapProp p in map.Props)
		{
			save.Props.Add(new MapPropSave
			{
				Id = p.Id,
				PropName = p.PropName,
				BlipSprite = (int)p.BlipSprite,
				BlipColor = (int)p.BlipColor,
				GroundOffset = V(p.GroundOffset),
				Interactable = p.Interactable,
				IsDoor = p.IsDoor,
				CanBePickedUp = p.CanBePickedUp,
				IsSnappable = p.IsSnappable,
				Rotation = V(p.Rotation),
				Position = V(p.Position),
				Weapons = From(p.Weapons)
			});
		}
		return save;
	}

	public static Map ToMap(MapSave save)
	{
		Map map = new Map();
		if (save == null || save.Props == null)
		{
			return map;
		}
		foreach (MapPropSave p in save.Props.Where(p => p != null && Text(p.Id) && Text(p.PropName)).Take(MaxProps))
		{
			map.Props.Add(new MapProp(p.Id, p.PropName, (BlipSprite)p.BlipSprite, (BlipColor)p.BlipColor,
				V(p.GroundOffset), p.Interactable, p.IsDoor, p.CanBePickedUp, p.IsSnappable,
				V(p.Rotation), V(p.Position), 0, ToWeapons(p.Weapons)));
		}
		return map;
	}

	// ------------------------------------------------------------------ veículos

	public static List<VehicleSave> From(VehicleCollection vehicles)
	{
		return vehicles.Select(v => new VehicleSave
		{
			Hash = v.Hash,
			Rotation = V(v.Rotation),
			Position = V(v.Position),
			PrimaryColor = (int)v.PrimaryColor,
			SecondaryColor = (int)v.SecondaryColor,
			Health = v.Health,
			EngineHealth = v.EngineHealth,
			Heading = v.Heading,
			NeonLights = (v.NeonLights ?? new VehicleNeonLight[0]).Select(n => (int)n).ToArray(),
			ModTypes = (v.Mods ?? new List<Tuple<VehicleModType, int>>()).Select(m => (int)m.Item1).ToArray(),
			ModIndexes = (v.Mods ?? new List<Tuple<VehicleModType, int>>()).Select(m => m.Item2).ToArray(),
			ToggleMods = (v.ToggleMods ?? new VehicleToggleModType[0]).Select(t => (int)t).ToArray(),
			WindowTint = (int)v.WindowTint,
			WheelType = (int)v.WheelType,
			NeonColor = v.NeonColor.ToArgb(),
			Livery = v.Livery,
			Wheels1 = v.Wheels1,
			Wheels2 = v.Wheels2
		}).ToList();
	}

	public static VehicleCollection ToVehicles(List<VehicleSave> saves)
	{
		VehicleCollection result = new VehicleCollection();
		if (saves == null)
		{
			return result;
		}
		foreach (VehicleSave s in saves.Where(s => s != null).Take(MaxVehicles))
		{
			int[] types = Ints(s.ModTypes);
			int[] indexes = Ints(s.ModIndexes);
			List<Tuple<VehicleModType, int>> mods = new List<Tuple<VehicleModType, int>>();
			for (int i = 0; i < Math.Min(types.Length, indexes.Length); i++)
			{
				mods.Add(new Tuple<VehicleModType, int>((VehicleModType)types[i], indexes[i]));
			}
			result.Add(new VehicleData(0, s.Hash, V(s.Rotation), V(s.Position),
				(VehicleColor)s.PrimaryColor, (VehicleColor)s.SecondaryColor, s.Health, s.EngineHealth, s.Heading,
				Ints(s.NeonLights).Select(n => (VehicleNeonLight)n).ToArray(), mods,
				Ints(s.ToggleMods).Select(t => (VehicleToggleModType)t).ToArray(),
				(VehicleWindowTint)s.WindowTint, (VehicleWheelType)s.WheelType, Color.FromArgb(s.NeonColor),
				s.Livery, s.Wheels1, s.Wheels2));
		}
		return result;
	}

	// ------------------------------------------------------------------- guardas

	public static List<PedSave> From(PedCollection peds)
	{
		return peds.Select(p => new PedSave
		{
			Hash = p.Hash,
			Rotation = V(p.Rotation),
			Position = V(p.Position),
			Task = (int)p.Task,
			Weapons = From(p.Weapons)
		}).ToList();
	}

	public static PedCollection ToPeds(List<PedSave> saves)
	{
		PedCollection result = new PedCollection();
		if (saves == null)
		{
			return result;
		}
		foreach (PedSave s in saves.Where(s => s != null).Take(MaxPeds))
		{
			result.Add(new PedData(0, s.Hash, V(s.Rotation), V(s.Position), (PedTask)s.Task,
				ToWeapons(s.Weapons) ?? new List<Weapon>()));
		}
		return result;
	}
}
