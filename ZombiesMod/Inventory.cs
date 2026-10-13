using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI;
using LemonUI.Menus;
using LemonUI.TimerBars;
using LemonUI.Scaleform;
using ZombiesMod.Static;
using GTA.Input;
using UI = GTA.UI;

namespace ZombiesMod;

[Serializable]
public class Inventory
{
	public delegate void CraftedItemEvent(InventoryItemBase item);

	public delegate void ItemUsedEvent(InventoryItemBase item, ItemType type);

	public delegate void AddedItemEvent(InventoryItemBase item, int newAmount);

	public const float InteractDist = 2.5f;

	public List<InventoryItemBase> Items = new List<InventoryItemBase>();

	public List<InventoryItemBase> Resources = new List<InventoryItemBase>();

	[NonSerialized]
	public readonly MenuType MenuType;

	[NonSerialized]
	public NativeMenu InventoryMenu;

	[NonSerialized]
	public NativeMenu ResourceMenu;

	private static Vector3 PlayerPosition
	{
		get
		{
			return Database.PlayerPosition;
		}
	}

	private static Ped PlayerPed => Database.PlayerPed;

	public bool DeveloperMode { get; set; }

	[field: NonSerialized]
	public event CraftedItemEvent TryCraft;

	[field: NonSerialized]
	public event ItemUsedEvent ItemUsed;

	[field: NonSerialized]
	public event AddedItemEvent AddedItem;

	public Inventory(MenuType menuType, bool ignoreContainers = false)
	{
		MenuType = menuType;
		AddDefaultCatalog();
		LoadMenus();
		if (!ignoreContainers)
		{
			AddWeaponsCrate();
		}
	}

	/// <summary>
	/// Garante que este inventário tem todos os recursos e itens embutidos do mod.
	///
	/// Serve os saves anteriores a uma versão que tenha acrescentado itens: adiciona só
	/// os que faltam, sem duplicar e sem apagar o que o jogador já tem. Num inventário
	/// novo é inócuo (o construtor já criou tudo).
	/// </summary>
	public void EnsureDefaultCatalog()
	{
		AddDefaultCatalog();
		AddWeaponsCrate();
		RemapComponentResources();
	}

	private void AddDefaultCatalog()
	{
		InventoryItemBase inventoryItemBase = new InventoryItemBase(0, 20, "Alcohol", Lang.T("item.alcohol.desc"));
		InventoryItemBase inventoryItemBase2 = new InventoryItemBase(0, 25, "Battery", Lang.T("item.battery.desc"));
		InventoryItemBase inventoryItemBase3 = new InventoryItemBase(0, 25, "Binding", Lang.T("item.binding.desc"));
		InventoryItemBase inventoryItemBase4 = new InventoryItemBase(0, 10, "Bottle", Lang.T("item.bottle.desc"));
		InventoryItemBase inventoryItemBase5 = new InventoryItemBase(0, 25, "Cloth", Lang.T("item.cloth.desc"));
		InventoryItemBase inventoryItemBase6 = new InventoryItemBase(0, 25, "Dirty Water", Lang.T("item.dirty_water.desc"));
		InventoryItemBase inventoryItemBase7 = new InventoryItemBase(0, 25, "Metal", Lang.T("item.metal.desc"));
		InventoryItemBase inventoryItemBase8 = new InventoryItemBase(0, 25, "Wood", Lang.T("item.wood.desc"));
		InventoryItemBase inventoryItemBase9 = new InventoryItemBase(0, 25, "Plastic", Lang.T("item.plastic.desc"));
		InventoryItemBase inventoryItemBase10 = new InventoryItemBase(0, 15, "Raw Meat", Lang.T("item.raw_meat.desc"));
		InventoryItemBase inventoryItemBase11 = new InventoryItemBase(0, 10, "Matches", Lang.T("item.matches.desc"));
		InventoryItemBase inventoryItemBase12 = new InventoryItemBase(25, 25, "Weapon Parts", Lang.T("item.weapon_parts.desc"));
		InventoryItemBase inventoryItemBase13 = new InventoryItemBase(0, 25, "Vehicle Parts", Lang.T("item.vehicle_parts.desc"));
		UsableInventoryItem usableInventoryItem = new UsableInventoryItem(0, 10, "Bandage", Lang.T("item.bandage.desc"), new UsableItemEvent[2]
		{
			new UsableItemEvent(ItemEvent.GiveHealth, 25),
			new UsableItemEvent(ItemEvent.GiveArmor, 15)
		})
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase3, 1),
				new CraftableItemComponent(inventoryItemBase, 2),
				new CraftableItemComponent(inventoryItemBase5, 2)
			}
		};
		CraftableInventoryItem craftableInventoryItem = new CraftableInventoryItem(0, 5, "Suppressor", Lang.T("item.suppressor.desc"), () =>
		{
			GTA.Weapon current = PlayerPed.Weapons.Current;
			if ((int)current.Hash == -1569615261)
			{
				UI.Notification.Show(Lang.T("notify.no_weapon_selected"), false);
				return false;
			}
			WeaponComponentHash[] array3 = new WeaponComponentHash[5]
			{
				WeaponComponentHash.AtArSupp,
				WeaponComponentHash.AtArSupp02,
				WeaponComponentHash.AtPiSupp,
				WeaponComponentHash.AtPiSupp02,
				WeaponComponentHash.AtSrSupp
			};
			foreach (WeaponComponentHash val in array3)
			{
				if (Function.Call<bool>(Hash.DOES_WEAPON_TAKE_WEAPON_COMPONENT, new InputArgument[2]
				{
					(uint)(int)current.Hash,
					(uint)(int)val
				}) && !current.Components[val].Active)
				{
					current.Components[val].Active = true;
					return true;
				}
			}
			UI.Notification.Show(Lang.T("notify.cannot_equip_now"), false);
			return false;
		})
		{
			RequiredComponents = new CraftableItemComponent[2]
			{
				new CraftableItemComponent(inventoryItemBase12, 2),
				new CraftableItemComponent(inventoryItemBase3, 1)
			}
		};
		BuildableInventoryItem buildableInventoryItem = new BuildableInventoryItem(0, 5, "Sand Block", Lang.T("item.sand_block.desc"), "prop_mb_sandblock_02", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: false, isDoor: false, canBePickedUp: true, isSnappable: true)
		{
			RequiredComponents = new CraftableItemComponent[4]
			{
				new CraftableItemComponent(inventoryItemBase7, 3),
				new CraftableItemComponent(inventoryItemBase3, 2),
				new CraftableItemComponent(inventoryItemBase5, 1),
				new CraftableItemComponent(inventoryItemBase8, 2)
			}
		};
		BuildableInventoryItem buildableInventoryItem2 = new BuildableInventoryItem(0, 2, "Work Bench", Lang.T("item.work_bench.desc"), "prop_tool_bench02", (BlipSprite)110, (BlipColor)66, Vector3.Zero, interactable: true, isDoor: false, canBePickedUp: true)
		{
			RequiredComponents = new CraftableItemComponent[4]
			{
				new CraftableItemComponent(inventoryItemBase7, 15),
				new CraftableItemComponent(inventoryItemBase8, 5),
				new CraftableItemComponent(inventoryItemBase9, 5),
				new CraftableItemComponent(inventoryItemBase3, 5)
			}
		};
		BuildableInventoryItem buildableInventoryItem3 = new BuildableInventoryItem(0, 3, "Gate", Lang.T("item.gate.desc"), "prop_gate_prison_01", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: true, isDoor: true, canBePickedUp: true, isSnappable: true)
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase7, 5),
				new CraftableItemComponent(inventoryItemBase3, 3),
				new CraftableItemComponent(inventoryItemBase2, 1)
			}
		};
		WeaponInventoryItem weaponInventoryItem = new WeaponInventoryItem(0, 25, "Molotov", Lang.T("item.molotov.desc"), 1, (WeaponHash)615608432, null)
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase, 1),
				new CraftableItemComponent(inventoryItemBase5, 1),
				new CraftableItemComponent(inventoryItemBase4, 1)
			}
		};
		WeaponInventoryItem weaponInventoryItem2 = new WeaponInventoryItem(0, 1, "Knife", Lang.T("item.knife.desc"), 1, WeaponHash.Knife, null)
		{
			RequiredComponents = new CraftableItemComponent[2]
			{
				new CraftableItemComponent(inventoryItemBase7, 3),
				new CraftableItemComponent(inventoryItemBase3, 1)
			}
		};
		WeaponInventoryItem weaponInventoryItem3 = new WeaponInventoryItem(0, 5, "Flashlight", Lang.T("item.flashlight.desc"), 1, WeaponHash.Flashlight, null)
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase2, 4),
				new CraftableItemComponent(inventoryItemBase9, 4),
				new CraftableItemComponent(inventoryItemBase3, 4)
			}
		};
		FoodInventoryItem foodInventoryItem = new FoodInventoryItem(0, 15, "Cooked Meat", Lang.T("item.cooked_meat.desc"), "mp_player_inteat@burger", "mp_player_int_eat_burger", (AnimationFlags)16, -1, FoodType.Food, 0.25f)
		{
			RequiredComponents = new CraftableItemComponent[2]
			{
				new CraftableItemComponent(inventoryItemBase10, 1),
				new CraftableItemComponent(inventoryItemBase, 2)
			},
			NearbyResource = NearbyResource.CampFire
		};
		FoodInventoryItem foodInventoryItem2 = new FoodInventoryItem(0, 15, "Packaged Food", Lang.T("item.packaged_food.desc"), "mp_player_inteat@pnq", "loop", (AnimationFlags)16, -1, FoodType.SpecialFood, 0.3f);
		FoodInventoryItem foodInventoryItem3 = new FoodInventoryItem(0, 15, "Clean Water", Lang.T("item.clean_water.desc"), "mp_player_intdrink", "loop_bottle", (AnimationFlags)16, -1, FoodType.Water, 0.35f)
		{
			RequiredComponents = new CraftableItemComponent[1]
			{
				new CraftableItemComponent(inventoryItemBase6, 1)
			},
			NearbyResource = NearbyResource.CampFire
		};
		BuildableInventoryItem buildableInventoryItem4 = new BuildableInventoryItem(1, 5, "Tent", Lang.T("item.tent.desc"), "prop_skid_tent_01", (BlipSprite)411, (BlipColor)0, Vector3.Zero, interactable: true, isDoor: false, canBePickedUp: true)
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase8, 3),
				new CraftableItemComponent(inventoryItemBase5, 4),
				new CraftableItemComponent(inventoryItemBase3, 3)
			}
		};
		BuildableInventoryItem buildableInventoryItem5 = new BuildableInventoryItem(1, 5, "Camp Fire", Lang.T("item.camp_fire.desc"), "prop_beach_fire", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: false, isDoor: false, canBePickedUp: true)
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase8, 3),
				new CraftableItemComponent(inventoryItemBase, 1),
				new CraftableItemComponent(inventoryItemBase11, 1)
			}
		};
		BuildableInventoryItem buildableInventoryItem6 = new BuildableInventoryItem(0, 15, "Wall", Lang.T("item.wall.desc"), "prop_fncconstruc_01d", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: false, isDoor: false, canBePickedUp: true, isSnappable: true)
		{
			RequiredComponents = new CraftableItemComponent[2]
			{
				new CraftableItemComponent(inventoryItemBase8, 4),
				new CraftableItemComponent(inventoryItemBase3, 3)
			}
		};
		BuildableInventoryItem buildableInventoryItem7 = new BuildableInventoryItem(0, 10, "Barrier", Lang.T("item.barrier.desc"), "prop_fncwood_16b", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: false, isDoor: false, canBePickedUp: true, isSnappable: true)
		{
			RequiredComponents = new CraftableItemComponent[2]
			{
				new CraftableItemComponent(inventoryItemBase8, 5),
				new CraftableItemComponent(inventoryItemBase3, 2)
			}
		};
		BuildableInventoryItem buildableInventoryItem8 = new BuildableInventoryItem(0, 5, "Door", Lang.T("item.door.desc"), "ex_p_mp_door_office_door01", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: true, isDoor: true, canBePickedUp: true, isSnappable: true)
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase8, 3),
				new CraftableItemComponent(inventoryItemBase3, 1),
				new CraftableItemComponent(inventoryItemBase7, 1)
			}
		};
		BuildableInventoryItem buildableInventoryItem9 = new BuildableInventoryItem(0, 20, "Wood Fence", Lang.T("item.wood_fence.desc"), "prop_fncwood_02b", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: false, isDoor: false, canBePickedUp: true, isSnappable: true)
		{
			RequiredComponents = new CraftableItemComponent[2]
			{
				new CraftableItemComponent(inventoryItemBase8, 4),
				new CraftableItemComponent(inventoryItemBase3, 2)
			}
		};
		BuildableInventoryItem buildableInventoryItem10 = new BuildableInventoryItem(0, 10, "Metal Barricade", Lang.T("item.metal_barricade.desc"), "prop_sec_barier_01a", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: false, isDoor: false, canBePickedUp: true, isSnappable: true)
		{
			RequiredComponents = new CraftableItemComponent[2]
			{
				new CraftableItemComponent(inventoryItemBase7, 4),
				new CraftableItemComponent(inventoryItemBase5, 2)
			}
		};
		BuildableInventoryItem buildableInventoryItem11 = new BuildableInventoryItem(0, 5, "Metal Gate", Lang.T("item.metal_gate.desc"), "prop_sec_gate_01b", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: true, isDoor: true, canBePickedUp: true, isSnappable: true)
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase7, 6),
				new CraftableItemComponent(inventoryItemBase3, 3),
				new CraftableItemComponent(inventoryItemBase2, 1)
			}
		};
		BuildableInventoryItem buildableInventoryItem12 = new BuildableInventoryItem(0, 5, "Wooden Crate", Lang.T("item.wooden_crate.desc"), "ba_prop_battle_crate_closed_bc", (BlipSprite)1, (BlipColor)0, Vector3.Zero, interactable: false, isDoor: false, canBePickedUp: true, isSnappable: false)
		{
			RequiredComponents = new CraftableItemComponent[2]
			{
				new CraftableItemComponent(inventoryItemBase8, 5),
				new CraftableItemComponent(inventoryItemBase3, 2)
			}
		};
		CraftableInventoryItem craftableInventoryItem2 = new CraftableInventoryItem(0, 10, "Vehicle Repair Kit", Lang.T("item.vehicle_repair_kit.desc"), () =>
		{
			UI.Notification.Show(Lang.T("notify.repair_vehicle_only"), false);
			return false;
		})
		{
			RequiredComponents = new CraftableItemComponent[3]
			{
				new CraftableItemComponent(inventoryItemBase13, 5),
				new CraftableItemComponent(inventoryItemBase7, 5),
				new CraftableItemComponent(inventoryItemBase3, 2)
			}
		};
		AddMissing(Items, new InventoryItemBase[21]
		{
			usableInventoryItem, weaponInventoryItem, weaponInventoryItem2, weaponInventoryItem3, foodInventoryItem, foodInventoryItem2, foodInventoryItem3, buildableInventoryItem4, buildableInventoryItem5, buildableInventoryItem6,
			buildableInventoryItem7, buildableInventoryItem8, craftableInventoryItem, buildableInventoryItem3, buildableInventoryItem, buildableInventoryItem2, craftableInventoryItem2, buildableInventoryItem9, buildableInventoryItem10, buildableInventoryItem11,
			buildableInventoryItem12
		});
		AddMissing(Resources, new InventoryItemBase[13]
		{
			inventoryItemBase3, inventoryItemBase, inventoryItemBase5, inventoryItemBase4, inventoryItemBase7, inventoryItemBase8, inventoryItemBase2, inventoryItemBase9, inventoryItemBase10, inventoryItemBase6,
			inventoryItemBase11, inventoryItemBase12, inventoryItemBase13
		});
		Items.Sort((InventoryItemBase c1, InventoryItemBase c2) => string.Compare(c1.Id, c2.Id, StringComparison.Ordinal));
		Resources.Sort((InventoryItemBase c1, InventoryItemBase c2) => string.Compare(c1.Id, c2.Id, StringComparison.Ordinal));
	}

	private void AddWeaponsCrate()
	{
		if (FindItem("Weapons Crate") != null)
		{
			return;
		}

		InventoryItemBase metal = FindResource("Metal");
		InventoryItemBase wood = FindResource("Wood");
		InventoryItemBase plastic = FindResource("Plastic");
		InventoryItemBase binding = FindResource("Binding");
		if (metal == null || wood == null || plastic == null || binding == null)
		{
			return;
		}

		Items.Add(new WeaponStorageInventoryItem(0, 1, "Weapons Crate", Lang.T("item.weapons_crate.desc"), "hei_prop_carrier_crate_01a", (BlipSprite)150, (BlipColor)0, Vector3.Zero, interactable: true, isDoor: false, canBePickedUp: true, new List<Weapon>())
		{
			RequiredComponents = new CraftableItemComponent[4]
			{
				new CraftableItemComponent(metal, 15),
				new CraftableItemComponent(wood, 15),
				new CraftableItemComponent(plastic, 15),
				new CraftableItemComponent(binding, 10)
			}
		});
	}

	/// <summary>
	/// Acrescenta só os candidatos cujo Id ainda não exista no destino. É isto que
	/// permite reconstruir o catálogo embutido sobre um save antigo sem duplicar itens.
	/// </summary>
	private static void AddMissing(List<InventoryItemBase> target, InventoryItemBase[] candidates)
	{
		foreach (InventoryItemBase candidate in candidates)
		{
			if (target.Find(existing => string.Equals(existing.Id, candidate.Id, StringComparison.OrdinalIgnoreCase)) == null)
			{
				target.Add(candidate);
			}
		}
	}

	/// <summary>
	/// Reaponta cada receita para a instância canónica do recurso. Necessário quando um
	/// item novo entra num save antigo: a receita foi montada com recursos recém-criados,
	/// mas a construção tem de consumir os recursos que o inventário já usa.
	/// </summary>
	private void RemapComponentResources()
	{
		foreach (InventoryItemBase item in Items)
		{
			if (!(item is ICraftable craftable) || craftable.RequiredComponents == null)
			{
				continue;
			}

			foreach (CraftableItemComponent component in craftable.RequiredComponents)
			{
				if (component?.Resource == null)
				{
					continue;
				}

				InventoryItemBase canonical = FindResource(component.Resource.Id);
				if (canonical != null)
				{
					component.Resource = canonical;
				}
			}
		}
	}

	private InventoryItemBase FindItem(string id)
	{
		return Items.Find(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	private InventoryItemBase FindResource(string id)
	{
		return Resources.Find(resource => string.Equals(resource.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	public void LoadMenus()
	{
		InventoryMenu = new NativeMenu(Lang.T("menu.inventory_title"), Lang.T("menu.select_item"));
		ResourceMenu = new NativeMenu(Lang.T("menu.resources_title"), Lang.T("menu.select_item"));
		AddItemsToMenu(InventoryMenu, Items, ItemType.Item);
		AddItemsToMenu(ResourceMenu, Resources, ItemType.Resource);
		MenuConrtoller.ObjectPool.Add(InventoryMenu);
		MenuConrtoller.ObjectPool.Add(ResourceMenu);
		RefreshMenu();
		InventoryMenu.SelectedIndex = 0;
		ResourceMenu.SelectedIndex = 0;
		if (MenuType == MenuType.Player)
		{
			InventoryMenu.Buttons.Add(new InstructionalButton(Lang.T("opt.show_blueprint"), (Control)23));
			InventoryMenu.Buttons.Add(new InstructionalButton(Lang.T("opt.craft_item"), (Control)26));
		}
	}

	public void RefreshMenu()
	{
		UpdateMenuSpecific(InventoryMenu, Items, MenuType == MenuType.Player);
		UpdateMenuSpecific(ResourceMenu, Resources, leftBadges: false);
	}

	public void ProcessKeys()
	{
		if (MenuType != MenuType.Player || !InventoryMenu.Visible)
		{
			return;
		}
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)23);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)75);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)26);
		if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)23))
		{
			ICraftable selectedInventoryItem = GetSelectedInventoryItem<ICraftable>();
			if (selectedInventoryItem == null)
			{
				return;
			}
			StringBuilder str = new StringBuilder(Lang.T("inv.blueprint_header"));
			if (selectedInventoryItem.RequiredComponents != null && (selectedInventoryItem.RequiredComponents.Any() || DeveloperMode))
			{
				Array.ForEach(selectedInventoryItem.RequiredComponents, (CraftableItemComponent i) =>
				{
					str.Append(Lang.T("inv.item_requirements", (i.Resource.Amount >= i.RequiredAmount) ? "~g~" : "~r~", i.Resource.Amount, i.RequiredAmount, i.Resource.Id));
				});
				UI.Notification.Show(str.ToString(), false);
			}
		}
		else if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)26))
		{
			InventoryItemBase selectedInventoryItem2 = GetSelectedInventoryItem<InventoryItemBase>();
			ICraftable craftable = selectedInventoryItem2 as ICraftable;
			if (selectedInventoryItem2 == null)
			{
				throw new NullReferenceException("item");
			}
			if (craftable != null)
			{
				Craft(selectedInventoryItem2, craftable);
			}
		}
	}

	public bool AddItem(InventoryItemBase item, int amount, ItemType type)
	{
		if (item == null)
		{
			// Um Id errado devolve null aqui; em vez de rebentar o script, loga e recusa.
			ModLogger.Warn(
				"Inventory.AddItem",
				"item nulo (amount=" + amount + ") — provável Id inexistente passado a ItemFromName.");
			return false;
		}

		if (!DeveloperMode)
		{
			if (item.Amount + amount < 0)
			{
				return false;
			}
			if (item.Amount + amount > item.MaxAmount)
			{
				UI.Notification.Show(Lang.T("notify.no_space", Lang.ItemName(item.Id)), true);
				return false;
			}
		}
		item.Amount += amount;
		switch (type)
		{
		case ItemType.Item:
			UpdateMenuSpecific(InventoryMenu, Items, MenuType == MenuType.Player);
			break;
		case ItemType.Resource:
			UpdateMenuSpecific(ResourceMenu, Resources, leftBadges: false);
			break;
		}
		RefreshMenu();
		AddedItem?.Invoke(item, item.Amount);
		return true;
	}

	private void Craft(InventoryItemBase item, ICraftable craftable)
	{
		if (MenuType != MenuType.Player || item == null || craftable == null || (!DeveloperMode && (!CanCraftItem(craftable) || item.Amount >= item.MaxAmount)))
		{
			return;
		}
		FoodInventoryItem foodInventoryItem = item as FoodInventoryItem;
		if (!DeveloperMode)
		{
			NearbyResource? nearbyResource = foodInventoryItem?.NearbyResource;
			NearbyResource? nearbyResource2 = nearbyResource;
			NearbyResource? nearbyResource3 = nearbyResource2;
			if (nearbyResource3.HasValue)
			{
				NearbyResource valueOrDefault = nearbyResource3.GetValueOrDefault();
				if (valueOrDefault == NearbyResource.CampFire)
				{
					Prop[] nearbyProps = World.GetNearbyProps(PlayerPosition, 2.5f, (Model)("prop_beach_fire"));
					if (!nearbyProps.Any())
					{
						UI.Notification.Show(Lang.T("notify.no_campfire_nearby"), false);
						return;
					}
				}
			}
		}
		AddItem(item, 1, ItemType.Item);

		// FoodInventoryItem implementa ICraftable com RequiredComponents a null. O
		// teste de CanCraftItem não basta em DeveloperMode, onde a receita é saltada.
		if (craftable.RequiredComponents != null)
		{
			Array.ForEach(craftable.RequiredComponents, (CraftableItemComponent c) =>
			{
				AddItem(c.Resource, -c.RequiredAmount, ItemType.Resource);
			});
		}

		TryCraft?.Invoke(item);
	}

	private void AddItemsToMenu(NativeMenu menu, List<InventoryItemBase> items, ItemType type)
	{
		items.ForEach((InventoryItemBase i) =>
		{
			i.CreateMenuItem();
			menu.Add(i.MenuItem);
			i.MenuItem.Activated += (object sender, EventArgs e) =>
			{
				if (i is WeaponInventoryItem { Amount: >0 } weaponInventoryItem && PlayerPed.Weapons.HasWeapon(weaponInventoryItem.Hash))
				{
					UI.Notification.Show(Lang.T("notify.already_has_weapon"), false);
				}
				else if (i.Amount > 0)
				{
					ItemUsed?.Invoke(i, type);
				}
			};
		});
	}

	private void UpdateMenuSpecific(NativeMenu menu, List<InventoryItemBase> collection, bool leftBadges)
	{
		menu.Items.ForEach((NativeItem menuItem) =>
		{
			InventoryItemBase itemFromMenuItem = GetItemFromMenuItem<InventoryItemBase>(collection, menuItem);
			if (itemFromMenuItem != null)
			{
				if ((CanCraftItem(itemFromMenuItem as ICraftable) || DeveloperMode) & leftBadges)
				{
					menuItem.LeftBadgeSet = MenuBadges.Tick;
				}
				else if (leftBadges)
				{
					menuItem.LeftBadgeSet = MenuBadges.Lock;
				}
				menuItem.AltTitle = $"{itemFromMenuItem.Amount}/{itemFromMenuItem.MaxAmount}";
			}
		});
	}

	private bool CanCraftItem(ICraftable craftable)
	{
		if (craftable?.RequiredComponents == null)
		{
			return false;
		}
		CraftableItemComponent[] requiredComponents = craftable.RequiredComponents;
		foreach (CraftableItemComponent craftableItemComponent in requiredComponents)
		{
			InventoryItemBase resource = craftableItemComponent.Resource;
			InventoryItemBase owned = Resources.Find((InventoryItemBase inventoryItemBase) => resource == inventoryItemBase);

			// Um recurso que nem está registado tem de travar a receita. A condição
			// anterior era «Contains && Amount insuficiente», por isso um recurso
			// ausente passava a verificação e dava para criar sem pagar — o AddItem(-n)
			// falhava em silêncio logo a seguir.
			if (owned == null || owned.Amount < craftableItemComponent.RequiredAmount)
			{
				return false;
			}
		}
		return true;
	}

	private T GetSelectedInventoryItem<T>() where T : class
	{
		NativeItem menuItem = InventoryMenu.Items[InventoryMenu.SelectedIndex];
		return GetItemFromMenuItem<T>(Items, menuItem);
	}

	private static T GetItemFromMenuItem<T>(List<InventoryItemBase> collection, NativeItem menuItem) where T : class
	{
		return collection.Find((InventoryItemBase i) => i.MenuItem == menuItem) as T;
	}
}
