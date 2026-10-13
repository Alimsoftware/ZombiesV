using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using ZombiesMod.Extensions;
using ZombiesMod.PlayerManagement;
using ZombiesMod.Static;
using GTA.Input;

namespace ZombiesMod.Scripts;

public class Loot247 : SafeScript, ISpawner
{
	public const float InteractDistance = 1.5f;

	private readonly List<Blip> _blips = new List<Blip>();

	private readonly List<Prop> _lootedShelfes = new List<Prop>();

	private readonly int[] _propHashes;

	public static Loot247 Instance { get; private set; }

	public bool Spawn { get; set; }

	private static Vector3 PlayerPosition
	{
		get
		{
			return Database.PlayerPosition;
		}
	}

	private static Ped PlayerPed => Database.PlayerPed;

	public Loot247()
	{
		Instance = this;
		_propHashes = new int[5]
		{
			Game.GenerateHash("v_ret_247shelves01"),
			Game.GenerateHash("v_ret_247shelves02"),
			Game.GenerateHash("v_ret_247shelves03"),
			Game.GenerateHash("v_ret_247shelves04"),
			Game.GenerateHash("v_ret_247shelves05")
		};
		WrapEvents();
		((Script)this).Aborted += (object sender, EventArgs args) =>
		{
			Clear();
		};
	}

	private void OnTick(object sender, EventArgs e)
	{
		SpawnBlips();
		ClearBlips();
		LootShops();
	}

	private void LootShops()
	{
		if (!Spawn || ((Entity)(object)PlayerPed).IsPlayingAnim("oddjobs@shop_robbery@rob_till", "loop"))
		{
			return;
		}
		IEnumerable<Prop> source = World.GetNearbyProps(PlayerPosition, 15f).Where(IsShelf);
		Prop closest = World.GetClosest<Prop>(PlayerPosition, source.ToArray());
		if ((Entity)(object)closest == (Entity)null)
		{
			return;
		}
		float num = ((Entity)closest).Position.VDist(PlayerPosition);
		if (!(num > 1.5f))
		{
			Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)51);
			UiExtended.DisplayHelpTextThisFrame(Lang.T("help.loot_shelf"));
			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)51))
			{
				_lootedShelfes.Add(closest);
				bool flag = Database.Random.NextDouble() > 0.30000001192092896;
				PlayerInventory.Instance.PickupItem(flag ? PlayerInventory.Instance.ItemFromName("Packaged Food") : PlayerInventory.Instance.ItemFromName("Clean Water"), ItemType.Item);
				PlayerPed.Task.PlayAnimation("oddjobs@shop_robbery@rob_till", "loop");
				Ped playerPed = PlayerPed;
				Vector3 val = ((Entity)closest).Position - PlayerPosition;
				((Entity)playerPed).Heading = val.ToHeading();
			}
		}
	}

	private bool IsShelf(Prop arg)
	{
		int[] propHashes = _propHashes;
		Model model = ((Entity)arg).Model;
		return propHashes.Contains(model.Hash) && !_lootedShelfes.Contains(arg);
	}

	private void ClearBlips()
	{
		if (!Spawn)
		{
			Clear();
		}
	}

	private void SpawnBlips()
	{
		if (Spawn && _blips.Count < Database.Shops247Locations.Length)
		{
			Vector3[] shops247Locations = Database.Shops247Locations;
			foreach (Vector3 val in shops247Locations)
			{
				Blip val2 = World.CreateBlip(val);
				val2.Sprite = (BlipSprite)52;
				val2.Name = Lang.T("msg.blip_shop");
				val2.IsShortRange = true;
				_blips.Add(val2);
			}
		}
	}

	public void Clear()
	{
		while (_blips.Count > 0)
		{
			Blip val = _blips[0];
			val.Delete();
			_blips.RemoveAt(0);
		}
	}
}
