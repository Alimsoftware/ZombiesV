using System;
using GTA;
using GTA.Native;

namespace ZombiesMod;

[Serializable]
public class WeaponInventoryItem : InventoryItemBase, IWeapon, ICraftable
{
	public int Ammo { get; set; }

	public WeaponHash Hash { get; set; }

	public CraftableItemComponent[] RequiredComponents { get; set; }

	public WeaponComponentHash[] Components { get; set; }

	public WeaponInventoryItem(int amount, int maxAmount, string id, string description, int ammo, WeaponHash weaponHash, WeaponComponentHash[] weaponComponents)
		: base(amount, maxAmount, id, description)
	{
		Ammo = ammo;
		Hash = weaponHash;
		Components = weaponComponents;
	}
}
