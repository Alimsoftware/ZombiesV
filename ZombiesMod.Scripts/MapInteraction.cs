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
using ZombiesMod.PlayerManagement;
using ZombiesMod.Static;
using ZombiesMod.Wrappers;
using UI = GTA.UI;

namespace ZombiesMod.Scripts;

public class MapInteraction : Script
{
	private const int AmmoPerPart = 10;

	private readonly float _enemyRangeForSleeping = 50f;

	private readonly int _sleepHours = 8;

	private readonly NativeMenu _weaponsMenu = new NativeMenu(Lang.T("menu.weapons_crate"), Lang.T("menu.select_option"));

	private readonly NativeMenu _storageMenu;

	private readonly NativeMenu _myWeaponsMenu;

	private readonly NativeMenu _craftWeaponsMenu = new NativeMenu(Lang.T("menu.work_bench"), Lang.T("menu.select_option"));

	private readonly Dictionary<WeaponGroup, int> _requiredAmountDictionary;

	private static Ped PlayerPed => Database.PlayerPed;

	private static Player Player => Database.Player;

	public MapInteraction()
	{
		PlayerMap.Interacted += MapOnInteracted;
		MenuConrtoller.ObjectPool.Add(_weaponsMenu);
		MenuConrtoller.ObjectPool.Add(_craftWeaponsMenu);
		_storageMenu = new NativeMenu(_weaponsMenu.BannerText.Text, Lang.T("menu.store_weapons"));
		_weaponsMenu.AddSubMenu(_storageMenu, Lang.T("menu.store_weapons"));
		_myWeaponsMenu = new NativeMenu(_weaponsMenu.BannerText.Text, Lang.T("menu.give_weapons"));
		_weaponsMenu.AddSubMenu(_myWeaponsMenu, Lang.T("menu.give_weapons"));

		// AddSubMenu só cria o item de entrada no menu pai; o submenu continua por
		// desenhar e sem receber input até estar no ObjectPool, que é quem o percorre
		// e desenha. Sem estas duas linhas, «Armazenar» e «Dar» escondiam o pai e
		// mostravam um ecrã em branco.
		MenuConrtoller.ObjectPool.Add(_storageMenu);
		MenuConrtoller.ObjectPool.Add(_myWeaponsMenu);
		_enemyRangeForSleeping = ((Script)this).Settings.GetValue<float>("map_interaction", "enemy_range_for_sleeping", _enemyRangeForSleeping);
		_sleepHours = ((Script)this).Settings.GetValue<int>("map_interaction", "sleep_hours", _sleepHours);
		((Script)this).Settings.SetValue<float>("map_interaction", "enemy_range_for_sleeping", _enemyRangeForSleeping);
		((Script)this).Settings.SetValue<int>("map_interaction", "sleep_hours", _sleepHours);
		_requiredAmountDictionary = new Dictionary<WeaponGroup, int>
		{
			{
				WeaponGroup.Sniper,
				2
			},
			{
				WeaponGroup.Heavy,
				5
			},
			{
				WeaponGroup.MG,
				3
			},
			{
				WeaponGroup.PetrolCan,
				1
			}
		};
		((Script)this).Aborted += OnAborted;
	}

	private static void OnAborted(object sender, EventArgs eventArgs)
	{
		((Entity)PlayerPed).IsVisible = true;
		((Entity)PlayerPed).IsPositionFrozen = false;
		Player.CanControlCharacter = true;
		if (!((Entity)PlayerPed).IsDead)
		{
			UI.Screen.FadeIn(0);
		}
	}

	private void MapOnInteracted(MapProp mapProp, InventoryItemBase inventoryItem)
	{
		if (inventoryItem is BuildableInventoryItem buildableInventoryItem)
		{
			switch (buildableInventoryItem.Id)
			{
			case "Tent":
				Sleep(mapProp.Position);
				break;
			case "Weapons Crate":
				UseWeaponsCrate(mapProp);
				break;
			case "Work Bench":
				CraftAmmo();
				break;
			}
			if (buildableInventoryItem.IsDoor)
			{
				// Entity.FromHandle devolve null para um handle já apagado e o cast é
				// cego: se a porta tiver sido removida, isto rebentava com NRE.
				Prop prop = Entity.FromHandle(mapProp.Handle) as Prop;
				if (prop == null || !prop.Exists())
				{
					UI.Notification.Show(Lang.T("notify.door_missing"), false);
					return;
				}

				prop.SetStateOfDoor(!prop.GetDoorLockState(), DoorState.Closed);
			}
		}
	}

	private void CraftAmmo()
	{
		_craftWeaponsMenu.Clear();
		WeaponGroup[] source = (WeaponGroup[])Enum.GetValues(typeof(WeaponGroup));
		source = source.Where((WeaponGroup w) =>
		{
			return (int)w != 1595662460 && (int)w != -1609580060 && (int)w != -728555052 && (int)w != -942583726;
		}).ToArray();
		List<WeaponGroup> list = source.ToList();
		list.Add((WeaponGroup)970310034);
		source = list.ToArray();
		WeaponGroup[] array = source;
		for (int num = 0; num < array.Length; num++)
		{
			WeaponGroup weaponGroup = array[num];
			NativeItem val = new NativeItem(((int)weaponGroup == 970310034) ? Lang.T("menu.assault_rifle") : ((object)weaponGroup/*cast due to constrained. prefix*/).ToString(), Lang.T("menu.ammo_for", weaponGroup));
			val.LeftBadgeSet = ZombiesMod.Static.MenuBadges.Ammo;
			int required = GetRequiredPartsForWeaponGroup(weaponGroup);
			val.Description = Lang.T("menu.parts_needed", required);
			_craftWeaponsMenu.Add(val);
			val.Activated += (object sender, EventArgs e) =>
			{
				InventoryItemBase inventoryItemBase = PlayerInventory.Instance.ItemFromName("Weapon Parts");
				if (inventoryItemBase != null)
				{
					if (inventoryItemBase.Amount >= required)
					{
						WeaponHash[] array2 = (WeaponHash[])Enum.GetValues(typeof(WeaponHash));
						WeaponHash val2 = Array.Find(array2, (WeaponHash h) =>
						{
							return PlayerPed.Weapons.HasWeapon(h) && PlayerPed.Weapons[h].Group == weaponGroup;
						});
						GTA.Weapon val3 = PlayerPed.Weapons[val2];
						if (val3 != null)
						{
							int num2 = 10 * required;
							if (val3.Ammo + num2 <= val3.MaxAmmo)
							{
								PlayerPed.Weapons.Select(val3);
								if (val3.Ammo + num2 > val3.MaxAmmo)
								{
									val3.Ammo = val3.MaxAmmo;
								}
								else
								{
									val3.Ammo += num2;
								}
								PlayerInventory.Instance.AddItem(inventoryItemBase, -required, ItemType.Resource);
							}
						}
					}
					else
					{
						UI.Notification.Show(Lang.T("notify.not_enough_parts"), false);
					}
				}
			};
		}
		_craftWeaponsMenu.Visible = !_craftWeaponsMenu.Visible;
	}

	private int GetRequiredPartsForWeaponGroup(WeaponGroup group)
	{
		return (!_requiredAmountDictionary.ContainsKey(group)) ? 1 : _requiredAmountDictionary[group];
	}

	private void UseWeaponsCrate(MapProp prop)
	{
		if (prop?.Weapons == null)
		{
			return;
		}
		void OnWeaponsSubmenuShown(object sender, EventArgs e)
		{
			NativeMenu newMenu = (NativeMenu)sender;
			if (newMenu == _storageMenu)
			{
				TradeOffWeapons(prop, prop.Weapons, _storageMenu, giveToPlayer: true);
			}
			else if (newMenu == _myWeaponsMenu)
			{
				List<Weapon> playerWeapons = new List<Weapon>();
				WeaponHash[] source = (WeaponHash[])Enum.GetValues(typeof(WeaponHash));
				WeaponComponentHash[] weaponComponents = (WeaponComponentHash[])Enum.GetValues(typeof(WeaponComponentHash));
				source.ToList().ForEach((WeaponHash hash) =>
				{
					if ((int)hash != -1569615261 && PlayerPed.Weapons.HasWeapon(hash))
					{
						GTA.Weapon val = PlayerPed.Weapons[hash];
						WeaponComponentHash[] components = weaponComponents.Where((WeaponComponentHash c) =>
						{
							return PlayerPed.Weapons[hash].Components[c].Active;
						}).ToArray();
						Weapon item = new Weapon(val.Ammo, hash, components);
						playerWeapons.Add(item);
					}
				});
				TradeOffWeapons(prop, playerWeapons, _myWeaponsMenu, giveToPlayer: false);
			}
		}
		_storageMenu.Shown += OnWeaponsSubmenuShown;
		_myWeaponsMenu.Shown += OnWeaponsSubmenuShown;
		_weaponsMenu.Visible = !_weaponsMenu.Visible;
	}

	private static void TradeOffWeapons(MapProp item, List<Weapon> weapons, NativeMenu currentMenu, bool giveToPlayer)
	{
		// No NativeUI o sender do evento era o menu; no LemonUI e o item,
		// entao o menu e capturado pelo escopo em vez de lido do sender.
		NativeItem backItem = new(Lang.T("opt.back_action"));
		backItem.Activated += (_, _) => currentMenu.Back();

		currentMenu.Clear();
		currentMenu.Add(backItem);

		Action notify = () => PlayerMap.Instance.NotifyListChanged();

		weapons.ForEach(weapon =>
		{
			NativeItem weaponItem = new($"{weapon.Hash}");
			currentMenu.Add(weaponItem);

			// No NativeUI era currentMenu.RemoveItemAt(currentMenu.CurrentSelection).
			// O LemonUI nao tem RemoveItemAt; remove pelo proprio item, que e o
			// selecionado no momento em que ele e ativado.
			weaponItem.Activated += (_, _) =>
			{
				currentMenu.Remove(weaponItem);
				currentMenu.SelectedIndex = 0;
				if (giveToPlayer)
				{
					PlayerPed.Weapons.Give(weapon.Hash, 0, true, true);
					PlayerPed.Weapons[weapon.Hash].Ammo = weapon.Ammo;
					weapon.Components.ToList().ForEach((WeaponComponentHash component) =>
					{
						PlayerPed.Weapons[weapon.Hash].Components[component].Active = true;
					});
					item.Weapons.Remove(weapon);
				}
				else
				{
					PlayerPed.Weapons.Remove(weapon.Hash);
					item.Weapons.Add(weapon);
				}
				notify();
			};
		});
		currentMenu.SelectedIndex = 0;
	}

	private void Sleep(Vector3 position)
	{
		Ped[] array = World.GetNearbyPeds(position, _enemyRangeForSleeping).Where(IsEnemy).ToArray();
		if (!array.Any())
		{
			TimeSpan currentDayTime = World.CurrentTimeOfDay + new TimeSpan(0, _sleepHours, 0, 0);
			((Entity)PlayerPed).IsVisible = false;
			Player.CanControlCharacter = false;
			((Entity)PlayerPed).IsPositionFrozen = true;
			UI.Screen.FadeOut(2000);
			Script.Wait(2000);
			World.CurrentTimeOfDay = currentDayTime;
			((Entity)PlayerPed).IsVisible = true;
			Player.CanControlCharacter = true;
			((Entity)PlayerPed).IsPositionFrozen = false;
			PlayerPed.ClearBloodDamage();
			Weather[] source = (Weather[])Enum.GetValues(typeof(Weather));
			source = source.Where((Weather w) =>
			{
				return (int)w != 11 && (int)w != 13 && (int)w != 10 && (int)w != 12 && (int)w != -1;
			}).ToArray();
			Weather weather = source[Database.Random.Next(source.Length)];
			World.Weather = weather;
			Script.Wait(2000);
			UI.Screen.FadeIn(2000);
		}
		else
		{
			UI.Notification.Show(Lang.T("notify.enemies_nearby"), false);
			UI.Notification.Show(Lang.T("notify.marking_on_map"), false);
			Array.ForEach(array, AddBlip);
		}
	}

	private static void AddBlip(Ped ped)
	{
		Blip existing = ((Entity)ped).AttachedBlip;
		if (existing != null && existing.Exists())
		{
			return;
		}
		Blip val = ((Entity)ped).AddBlip();
		val.Name = Lang.T("hud.enemy_blip");
		EntityEventWrapper entityEventWrapper = new EntityEventWrapper((Entity)(object)ped);
		entityEventWrapper.Died += (EntityEventWrapper sender, Entity entity) =>
		{
			Blip currentBlip = entity.AttachedBlip;
			if (currentBlip != null)
			{
				currentBlip.Delete();
			}
			sender.Dispose();
		};
		entityEventWrapper.Aborted += (EntityEventWrapper sender, Entity entity) =>
		{
			Blip currentBlip = entity.AttachedBlip;
			if (currentBlip != null)
			{
				currentBlip.Delete();
			}
		};
	}

	private static bool IsEnemy(Ped ped)
	{
		return (ped.IsHuman && !((Entity)ped).IsDead && (int)ped.GetRelationshipWithPed(PlayerPed) == 5) || ped.IsInCombatAgainst(PlayerPed);
	}
}
