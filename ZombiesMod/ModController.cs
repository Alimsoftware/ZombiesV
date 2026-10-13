using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using LemonUI;
using LemonUI.Menus;
using ZombiesMod.Controllers;
using ZombiesMod.Extensions;
using ZombiesMod.PlayerManagement;
using ZombiesMod.Scripts;
using ZombiesMod.Static;
using ZombiesMod.Zombies;
using UI = GTA.UI;

namespace ZombiesMod;

public class ModController : SafeScript
{
	private Keys _menuKey = Keys.F10;

	public NativeMenu MainMenu { get; private set; }

	public static ModController Instance { get; private set; }

	public ModController()
	{
		// EXEMPLO — handler global da AppDomain (requisito 4 do SafeExecutor).
		// O SafeScript ja o instala no construtor da base; chamar aqui e redundante de
		// proposito, para o handler ficar garantido mesmo que algum script deixe um dia
		// de herdar de SafeScript. A linha de Info serve para confirmar em jogo que o
		// logger esta a escrever no sitio certo.
		SafeExecutor.InstallGlobalHandler();
		ModLogger.Info("ModController", "A carregar. Log em " + ModLogger.LogPath);

		Instance = this;
		Config.Check();
		Relationships.SetRelationships();
		LoadSave();
		ConfigureMenu();
		WrapEvents();
	}

	private void OnKeyUp(object sender, KeyEventArgs e)
	{
		// LemonUI: ObjectPool.AreAnyVisible substitui o MenuPool.AreAnyVisible.
		if (!MenuConrtoller.ObjectPool.AreAnyVisible && e.KeyCode == _menuKey)
		{
			MainMenu.Visible = !MainMenu.Visible;
		}
	}

	private void LoadSave()
	{
		_menuKey = Settings.GetValue<Keys>("keys", "zombies_menu_key", _menuKey);
		ZombiePed.ZombieDamage = Settings.GetValue<int>("zombies", "zombie_damage", ZombiePed.ZombieDamage);
		Settings.SetValue<Keys>("keys", "zombies_menu_key", _menuKey);
		Settings.SetValue<int>("zombies", "zombie_damage", ZombiePed.ZombieDamage);
		Settings.Save();
	}

	/// <summary>
	/// Monta o menu principal.
	///
	/// Duas ordens de argumento mudaram entre NativeUI e LemonUI e são fatais em
	/// silêncio se copiadas do código antigo (compilam, mas trocam os valores):
	///   - NativeCheckboxItem(titulo, descricao, check)  — era (titulo, check, descricao)
	///   - O evento agora é CheckboxChanged/Activated do tipo EventHandler, e o
	///     estado é lido em ((NativeCheckboxItem)sender).Checked.
	/// </summary>
	private void ConfigureMenu()
	{
		MainMenu = new NativeMenu(Lang.T("menu.main_title"), Lang.T("menu.select_option"));
		MenuConrtoller.ObjectPool.Add(MainMenu);

		AddCheckbox(Lang.T("menu.infection_mode"), Lang.T("menu.infection_mode_desc"), defaultValue: false, @checked =>
		{
			ZombieVehicleSpawner.Instance.Spawn = @checked;
			Loot247.Instance.Spawn = @checked;
			WorldController.Configure = @checked;
			AnimalSpawner.Instance.Spawn = @checked;
			if (@checked)
			{
				WorldExtended.ClearAreaOfEverything(Database.PlayerPosition, 10000f);
				Function.Call(Hash.REQUEST_IPL, new InputArgument[1] { "cs3_07_mpgates" });
			}
		});

		AddCheckbox(Lang.T("menu.fast_zombies"), Lang.T("menu.fast_zombies_desc"), defaultValue: false, @checked =>
			ZombieCreator.Runners = @checked);

		AddCheckbox(Lang.T("menu.electricity_mode"), Lang.T("menu.electricity_mode_desc"), defaultValue: true, @checked =>
			World.Blackout = !@checked);

		AddCheckbox(Lang.T("menu.survivors_spawn"), Lang.T("menu.survivors_spawn_desc"), defaultValue: false, @checked =>
			SurvivorController.Instance.Spawn = @checked);

		AddCheckbox(Lang.T("menu.stats_toggle"), Lang.T("menu.stats_toggle_desc"), defaultValue: true, @checked =>
			PlayerStats.UseStats = @checked);

		AddAction(Lang.T("menu.load_save"), Lang.T("menu.load_save_desc"), MenuBadges.Heart, () =>
		{
			PlayerMap.Instance.Deserialize();
			PlayerVehicles.Instance.Deserialize();
			PlayerGroupManager.Instance.Deserialize();
		});

		AddAction(Lang.T("menu.save_vehicle"), Lang.T("menu.save_vehicle_desc"), MenuBadges.Car, () =>
		{
			Vehicle vehicle = Database.PlayerCurrentVehicle;
			if (vehicle == null || !vehicle.Exists())
			{
				UI.Notification.Show(Lang.T("notify.not_in_vehicle"), false);
			}
			else
			{
				PlayerVehicles.Instance.SaveVehicle(vehicle);
			}
		});

		AddAction(Lang.T("menu.save_all"), Lang.T("menu.save_all_vehicles_desc"), MenuBadges.Car, () =>
			PlayerVehicles.Instance.Serialize(notify: true));

		AddAction(Lang.T("menu.save_all"), Lang.T("menu.save_all_peds_desc"), MenuBadges.Mask, () =>
			PlayerGroupManager.Instance.SavePeds());

		MainMenu.SelectedIndex = 0;
	}

	private NativeCheckboxItem AddCheckbox(string title, string description, bool defaultValue, Action<bool> onChanged)
	{
		NativeCheckboxItem item = new(title, description, defaultValue);
		item.CheckboxChanged += (sender, _) => onChanged(((NativeCheckboxItem)sender).Checked);
		MainMenu.Add(item);
		return item;
	}

	private NativeItem AddAction(string title, string description, LemonUI.Menus.BadgeSet badge, Action onActivated)
	{
		NativeItem item = new(title, description);
		item.LeftBadgeSet = badge;
		item.Activated += (_, _) => onActivated();
		MainMenu.Add(item);
		return item;
	}
}
