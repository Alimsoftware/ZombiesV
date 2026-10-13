using GTA;
using GTA.Native;

namespace ZombiesMod;

public interface IWeapon
{
	int Ammo { get; set; }

	WeaponHash Hash { get; set; }

	WeaponComponentHash[] Components { get; set; }
}
