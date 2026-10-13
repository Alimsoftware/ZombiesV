using System;
using GTA;
using GTA.Native;

namespace ZombiesMod;

[Serializable]
public class Weapon : IWeapon
{
	public int Ammo { get; set; }

	public WeaponHash Hash { get; set; }

	public WeaponComponentHash[] Components { get; set; }

	public Weapon(int ammo, WeaponHash hash, WeaponComponentHash[] components)
	{
		Ammo = ammo;
		Hash = hash;
		Components = components;
	}
}
