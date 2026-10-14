using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI;
using LemonUI.Menus;
using LemonUI.TimerBars;
using LemonUI.Scaleform;
using ZombiesMod.DataClasses;
using ZombiesMod.Extensions;
using ZombiesMod.Static;
using GTA.Input;
using UI = GTA.UI;

namespace ZombiesMod.PlayerManagement;

public class PlayerInventory : SafeScript
{
	public delegate void OnUsedFoodEvent(FoodInventoryItem item, FoodType foodType);

	public delegate void OnUsedWeaponEvent(WeaponInventoryItem weapon);

	public delegate void OnUsedBuildableEvent(BuildableInventoryItem item, Prop newProp);

	public delegate void OnLootedEvent(Ped ped);

	public const float InteractDistance = 1.5f;

	private readonly NativeMenu _mainMenu = new NativeMenu(string.Empty, Lang.T("menu.inventory_subtitle"));

	private readonly List<Ped> _lootedPeds = new List<Ped>();

	private Inventory _inventory;

	private readonly Keys _inventoryKey = Keys.I;

	public Inventory Backpack => _inventory;

	public static PlayerInventory Instance { get; private set; }

	private static Ped PlayerPed => Database.PlayerPed;

	private static Vector3 PlayerPosition
	{
		get
		{
			return Database.PlayerPosition;
		}
	}

	public static event OnUsedFoodEvent FoodUsed;

	public static event OnUsedWeaponEvent WeaponUsed;

	public static event OnUsedBuildableEvent BuildableUsed;

	public static event OnLootedEvent LootedPed;

	public PlayerInventory()
	{
		_inventoryKey = ((Script)this).Settings.GetValue<Keys>("keys", "inventory_key", _inventoryKey);
		((Script)this).Settings.SetValue<Keys>("keys", "inventory_key", _inventoryKey);
		((Script)this).Settings.Save();
		_inventory = new Inventory(MenuType.Player);
		// Itens do jogador (ZombiesMod_CustomItems.json) antes das quantidades e dos menus.
		CustomItems.Apply(_inventory);
		SaveData.Apply(_inventory, Serializer.Deserialize<InventorySave>(Config.InventoryFilePath));
		_inventory.LoadMenus();
		Instance = this;
		MenuConrtoller.ObjectPool.Add(_mainMenu);
		// O LemonUI ja desenha um banner por omissao; o NativeUI pedia um UIResRectangle vazio.
		// No NativeUI era BindMenuToItem(submenu, itemExistente).
		// No LemonUI, AddSubMenu ja cria e adiciona o item que aponta pro submenu.
		NativeSubmenuItem inventoryItem = _mainMenu.AddSubMenu(_inventory.InventoryMenu);
		inventoryItem.Title = Lang.T("menu.inventory_tab");
		NativeSubmenuItem resourceItem = _mainMenu.AddSubMenu(_inventory.ResourceMenu);
		resourceItem.Title = Lang.T("menu.resources_tab");

		// Ordem dos argumentos trocou no LemonUI (compila igual, mas inverte os valores):
		//   NativeUI: new UIMenuCheckboxItem(titulo, checked, descricao)
		//   LemonUI : new NativeCheckboxItem(titulo, descricao, checked)
		NativeCheckboxItem editModeItem = new(Lang.T("opt.edit_mode"), Lang.T("opt.edit_mode_desc"), check: true);
		editModeItem.CheckboxChanged += (sender, _) => PlayerMap.Instance.EditMode = ((NativeCheckboxItem)sender).Checked;

		NativeItem mainMenuLink = new(Lang.T("opt.main_menu_link"), Lang.T("opt.main_menu_link_desc"));
		mainMenuLink.Activated += (_, _) =>
		{
			MenuConrtoller.ObjectPool.HideAll();
			ModController.Instance.MainMenu.Visible = true;
		};

		NativeCheckboxItem devModeItem = new(Lang.T("opt.dev_mode"), Lang.T("opt.dev_mode_desc"), _inventory.DeveloperMode);
		devModeItem.CheckboxChanged += (sender, _) =>
		{
			bool @checked = ((NativeCheckboxItem)sender).Checked;
			if (_inventory != null)
			{
				if (@checked)
				{
					string userInput = Game.GetUserInput(WindowTitle.EnterMessage20, "", 12);
					if (string.IsNullOrEmpty(userInput) || userInput.ToLower() != "michael")
					{
						devModeItem.Checked = false;
						UI.Notification.Show(Lang.T("notify.dev_mode_hint"), false);
						return;
					}
				}
				_inventory.DeveloperMode = @checked;
				if (!@checked)
				{
					_inventory.Items.ForEach((InventoryItemBase i) =>
					{
						i.Amount = 0;
					});
					_inventory.Resources.ForEach((InventoryItemBase i) =>
					{
						i.Amount = 0;
					});
					_inventory.RefreshMenu();
				}
				else
				{
					UI.Notification.Show(Lang.T("notify.dev_mode_enabled"), false);
				}
				Serializer.Serialize(Config.InventoryFilePath, SaveData.From(_inventory));
			}
		};

		_mainMenu.Add(editModeItem);
		_mainMenu.Add(devModeItem);
		_inventory.ItemUsed += InventoryOnItemUsed;
		_inventory.AddedItem += (InventoryItemBase item, int amount) =>
		{
			Serializer.Serialize(Config.InventoryFilePath, SaveData.From(_inventory));
		};
		WrapEvents();
		LootedPed += OnLootedPed;
	}

	private void OnLootedPed(Ped ped)
	{
		if (ped.IsHuman)
		{
			PickupLoot(ped);
		}
		else
		{
			AnimalLoot(ped);
		}
	}

	private void AnimalLoot(Ped ped)
	{
		if (!PlayerPed.Weapons.HasWeapon(WeaponHash.Knife))
		{
			UI.Notification.Show(Lang.T("notify.need_knife"), false);
		}
		else if (_inventory.AddItem(ItemFromName("Raw Meat"), 2, ItemType.Resource))
		{
			PlayerPed.Weapons.Select(WeaponHash.Knife, true);
			UI.Notification.Show(Lang.T("notify.animal_looted"), false);
			PlayerPed.Task.PlayAnimation("amb@world_human_gardener_plant@male@base", "base", 8f, 3000, (AnimationFlags)0);
			_lootedPeds.Add(ped);
		}
	}

	public void PickupLoot(Ped ped, ItemType type = ItemType.Resource, int amountPerItemMin = 1, int amountPerItemMax = 3, float successChance = 0.2f)
	{
		List<InventoryItemBase> list = ((type == ItemType.Resource) ? _inventory.Resources : _inventory.Items);
		if (list.All((InventoryItemBase r) => r.Amount == r.MaxAmount))
		{
			UI.Notification.Show(Lang.T("notify.resources_full", type), false);
			return;
		}
		int amount = 0;
		list.ForEach((InventoryItemBase i) =>
		{
			if (!(i.Id == "Cooked Meat"))
			{
				Random random = Database.Random;
				if (!(random.NextDouble() > (double)successChance))
				{
					int num = Database.Random.Next(amountPerItemMin, amountPerItemMax);
					if (i.Amount + num > i.MaxAmount)
					{
						num = i.MaxAmount - i.Amount;
					}
					_inventory.AddItem(i, num, type);
					amount += num;
				}
			}
		});
		UI.Notification.Show((amount > 0) ? Lang.T("msg.gathered", type, amount) : Lang.T("msg.nothing_found"), true);
		PlayerPed.Task.PlayAnimation("pickup_object", "pickup_low");
		if (!((Entity)(object)ped == (Entity)null))
		{
			_lootedPeds.Add(ped);
		}
	}

	private void OnKeyUp(object sender, KeyEventArgs keyEventArgs)
	{
		if (!MenuConrtoller.ObjectPool.AreAnyVisible && keyEventArgs.KeyCode == _inventoryKey)
		{
			_mainMenu.Visible = !_mainMenu.Visible;
		}
	}

	private void InventoryOnItemUsed(InventoryItemBase item, ItemType type)
	{
		if (item == null || type == ItemType.Resource)
		{
			return;
		}
		if (item.GetType() == typeof(FoodInventoryItem))
		{
			FoodInventoryItem foodInventoryItem = (FoodInventoryItem)item;
			PlayerPed.Task.PlayAnimation(foodInventoryItem.AnimationDict, foodInventoryItem.AnimationName, 8f, foodInventoryItem.AnimationDuration, foodInventoryItem.AnimationFlags);
			FoodUsed?.Invoke(foodInventoryItem, foodInventoryItem.FoodType);
		}
		else if (item.GetType() == typeof(WeaponInventoryItem))
		{
			WeaponInventoryItem weaponInventoryItem = (WeaponInventoryItem)item;
			PlayerPed.Weapons.Give(weaponInventoryItem.Hash, weaponInventoryItem.Ammo, true, true);
			WeaponUsed?.Invoke(weaponInventoryItem);
		}
		else if (item.GetType() == typeof(BuildableInventoryItem) || item.GetType() == typeof(WeaponStorageInventoryItem))
		{
			if (PlayerPed.IsInVehicle())
			{
				UI.Notification.Show(Lang.T("notify.build_in_vehicle"), false);
				return;
			}
			PlaceBuildable((BuildableInventoryItem)item, type);
			return;
		}
		else if (item.GetType() == typeof(UsableInventoryItem))
		{
			UsableInventoryItem usableInventoryItem = (UsableInventoryItem)item;
			UsableItemEvent[] itemEvents = usableInventoryItem.ItemEvents;
			UsableItemEvent[] array = itemEvents;
			foreach (UsableItemEvent usableItemEvent in array)
			{
				switch (usableItemEvent.Event)
				{
				case ItemEvent.GiveArmor:
				{
					int num2 = (usableItemEvent.EventArgument as int?) ?? 0;
					Ped playerPed2 = PlayerPed;
					((Entity)playerPed2).Health = ((Entity)playerPed2).Health + num2;
					break;
				}
				case ItemEvent.GiveHealth:
				{
					int num = (usableItemEvent.EventArgument as int?) ?? 0;
					Ped playerPed = PlayerPed;
					playerPed.Armor += num;
					break;
				}
				}
			}
		}
		else if (item.GetType() == typeof(CraftableInventoryItem))
		{
			CraftableInventoryItem craftableInventoryItem = (CraftableInventoryItem)item;
			if (!craftableInventoryItem.Validation())
			{
				return;
			}
		}
		_inventory.AddItem(item, -1, type);
	}

	/// <summary>No modo edicao os construiveis sao ilimitados e gratis.</summary>
	public static bool IsFreeBuild(InventoryItemBase item)
	{
		return item is BuildableInventoryItem && PlayerMap.Instance != null && PlayerMap.Instance.EditMode;
	}

	/// <summary>
	/// Coloca o construivel e, enquanto houver estoque (ou for gratis), abre o proximo preview
	/// em sequencia. Cancelar (voltar) encerra. Bloqueia o script: so chamar da thread do script.
	/// </summary>
	private void PlaceBuildable(BuildableInventoryItem item, ItemType type)
	{
		while (true)
		{
			ItemPreview preview = new ItemPreview();
			preview.StartPreview(item.PropName, item.GroundOffset, item.IsDoor, item.IsSnappable);
			try
			{
				while (!preview.PreviewComplete)
				{
					Script.Yield();
				}
			}
			catch (ThreadAbortException)
			{
				// Script descarregado ou menu fechado: nao derruba o script.
				ModLogger.Error("PlaceBuildable", "Thread aborted while waiting for buildable preview");
				return;
			}
			catch (Exception ex)
			{
				ModLogger.Error("PlaceBuildable", ex.ToString());
				return;
			}

			Prop result = preview.GetResult();
			if (result == null)
			{
				return;
			}
			AddBlipToProp(item, item.Id, result);
			BuildableUsed?.Invoke(item, result);
			if (item.Amount > 0)
			{
				_inventory.AddItem(item, -1, type);
			}
			if ((item.Amount <= 0 && !IsFreeBuild(item)) || PlayerPed.IsInVehicle() || Database.PlayerIsDead)
			{
				return;
			}
			// Um frame de folga: o clique que colocou nao pode colocar o proximo.
			Script.Yield();
		}
	}

	private void OnTick(object sender, EventArgs eventArgs)
	{
		_inventory.ProcessKeys();
		GetWater();
		LootDeadPeds();
	}

	private void GetWater()
	{
		if (PlayerPed.IsInVehicle() || PlayerPed.IsSwimming || !((Entity)PlayerPed).IsInWater || ((Entity)(object)PlayerPed).IsPlayingAnim("pickup_object", "pickup_low"))
		{
			return;
		}
		InventoryItemBase inventoryItemBase = _inventory.Resources.Find((InventoryItemBase i) => i.Id == "Bottle");
		if (inventoryItemBase != null && inventoryItemBase.Amount > 0)
		{
			Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)23);
			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)23))
			{
				PlayerPed.Task.PlayAnimation("pickup_object", "pickup_low");
				InventoryItemBase item = ItemFromName("Dirty Water");
				AddItem(item, 1, ItemType.Resource);
				AddItem(inventoryItemBase, -1, ItemType.Resource);
				UI.Notification.Show(Lang.T("notify.resources_removed"), true);
				UI.Notification.Show(Lang.T("notify.resources_added"), true);
			}
		}
	}

	private void LootDeadPeds()
	{
		if (PlayerPed.IsInVehicle())
		{
			return;
		}
		Ped closest = World.GetClosest<Ped>(PlayerPosition, World.GetNearbyPeds(PlayerPed, 1.5f));
		if (!((Entity)(object)closest == (Entity)null) && ((Entity)closest).IsDead && !_lootedPeds.Contains(closest))
		{
			UiExtended.DisplayHelpTextThisFrame(Lang.T("help.loot_ped"));
			Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)51);
			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)51))
			{
				LootedPed?.Invoke(closest);
			}
		}
	}

	private void Controller()
	{
	}

	public bool AddItem(InventoryItemBase item, int amount, ItemType type)
	{
		return item != null && _inventory.AddItem(item, amount, type);
	}

	public bool PickupItem(InventoryItemBase item, ItemType type)
	{
		return item != null && _inventory.AddItem(item, 1, type);
	}

	public InventoryItemBase ItemFromName(string id)
	{
		if (_inventory?.Items == null)
		{
			return null;
		}
		if (_inventory?.Resources == null)
		{
			return null;
		}
		InventoryItemBase[] array = _inventory.Items.Concat(_inventory.Resources).ToArray();
		return Array.Find(array, (InventoryItemBase i) => i.Id == id);
	}

	private static void AddBlipToProp(IProp item, string name, Entity entity)
	{
		if ((int)item.BlipSprite != 1)
		{
			Blip val = entity.AddBlip();
			val.Sprite = item.BlipSprite;
			val.Color = item.BlipColor;
			val.Name = name;
		}
	}

	public bool HasItem(InventoryItemBase item, ItemType itemType)
	{
		if (item == null)
		{
			return false;
		}
		return itemType switch
		{
			ItemType.Item => _inventory.Items.Contains(item) && item.Amount > 0, 
			ItemType.Resource => _inventory.Resources.Contains(item) && item.Amount > 0, 
			_ => false, 
		};
	}
}
