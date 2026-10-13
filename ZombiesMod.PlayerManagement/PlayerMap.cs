using System;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using ZombiesMod.Extensions;
using ZombiesMod.Scripts;
using ZombiesMod.Static;
using ZombiesMod.DataClasses;
using GTA.Input;
using UI = GTA.UI;

namespace ZombiesMod.PlayerManagement;

public class PlayerMap : SafeScript
{
	public delegate void InteractedEvent(MapProp mapProp, InventoryItemBase inventoryItem);

	public const float InteractDistance = 3f;

	private Map _map;

	/// <summary>Handle da peça que está actualmente em foco no modo edição (0 = nenhuma).</summary>
	private int _focusedHandle;

	public static PlayerMap Instance { get; private set; }

	public bool EditMode { get; set; } = true;

	public Vector3 PlayerPosition
	{
		get
		{
			return Database.PlayerPosition;
		}
	}

	public static event InteractedEvent Interacted;

	public PlayerMap()
	{
		Instance = this;
		WrapEvents();
		((Script)this).Aborted += OnAborted;
		PlayerInventory.BuildableUsed += InventoryOnBuildableUsed;
	}

	public void Deserialize()
	{
		if (_map == null)
		{
			Map map = Serializer.Deserialize<Map>("./scripts/Map.dat");
			if (map == null)
			{
				map = new Map();
			}
			_map = map;
			_map.ListChanged += (int count) =>
			{
				Serializer.Serialize("./scripts/Map.dat", _map);
			};
			LoadProps();
		}
	}

	private void LoadProps()
	{
		if (_map.Count <= 0)
		{
			return;
		}
		foreach (MapProp item in _map)
		{
			Model val = (Model)(item.PropName);
			if (!val.Request(1000))
			{
				UI.Notification.Show(Lang.T("notify.prop_load_failed", item.PropName), false);
				continue;
			}
			Vector3 position = item.Position;
		Prop val2 = (Prop)Entity.FromHandle(Function.Call<int>(Hash.CREATE_OBJECT_NO_OFFSET, new InputArgument[7]
			{
				val.Hash,
				position.X,
				position.Y,
				position.Z,
				1,
				1,
				false
			}));
		val2.IsPositionFrozen = !item.IsDoor;
		val2.Rotation = item.Rotation;
			item.Handle = ((Entity)val2).Handle;
			if (item.IsSnappable)
			{
				ItemPreview.RegisterSnappable(val2);
			}
			if ((int)item.BlipSprite != 1)
			{
				Blip val3 = ((Entity)val2).AddBlip();
				val3.Sprite = item.BlipSprite;
				val3.Color = item.BlipColor;
				val3.Name = item.Id;
				ZombieVehicleSpawner.Instance.SpawnBlocker.Add(item.Position);
			}
		}
	}

	private void InventoryOnBuildableUsed(BuildableInventoryItem item, Prop newProp)
	{
		if (_map == null)
		{
			Deserialize();
		}
		MapProp mapProp = new MapProp(item.Id, item.PropName, item.BlipSprite, item.BlipColor, item.GroundOffset, item.Interactable, item.IsDoor, item.CanBePickedUp, item.IsSnappable, ((Entity)newProp).Rotation, ((Entity)newProp).Position, ((Entity)newProp).Handle, (item as WeaponStorageInventoryItem)?.WeaponsList);
		_map.Add(mapProp);
		ZombieVehicleSpawner.Instance.SpawnBlocker.Add(mapProp.Position);
	}

	private void OnAborted(object sender, EventArgs eventArgs)
	{
		RestoreFocus();
		_map.Clear();
	}

	private void OnTick(object sender, EventArgs eventArgs)
	{
		if (_map == null || !_map.Any() || MenuConrtoller.ObjectPool.AreAnyVisible)
		{
			return;
		}

		// Antes da lógica de interacção: essa pode remover entradas de _map a meio
		// de uma iteração.
		MapProp closest = World.GetClosest<MapProp>(PlayerPosition, _map.ToArray());

		UpdateEditModeFocus(closest);
		if (closest != null && closest.CanBePickedUp)
		{
			float num = closest.Position.VDist(PlayerPosition);
			if (!(num > 3f))
			{
				TryUseMapProp(closest);
			}
		}
	}

	/// <summary>
	/// Faz pulsar a construção mais próxima do jogador, enquanto o modo construção
	/// está activo e ao alcance de interacção, restaurando a anterior.
	///
	/// Substitui a antiga gaiola wireframe (caixa preta à volta de todas as peças num
	/// raio de 20 m) e a pegada em círculo: o destaque é agora a transparência da
	/// própria peça — não se desenha nada por cima dela.
	/// </summary>
	private void UpdateEditModeFocus(MapProp closest)
	{
		bool inRange = EditMode
			&& closest != null
			&& closest.Exists()
			&& closest.Position.VDist(PlayerPosition) <= InteractDistance;

		if (!inRange)
		{
			RestoreFocus();
			return;
		}

		Entity entity = Entity.FromHandle(closest.Handle);

		if (entity == null)
		{
			RestoreFocus();
			return;
		}

		if (_focusedHandle != 0 && _focusedHandle != closest.Handle)
		{
			RestoreFocus();
		}

		SafeExecutor.Run("PlayerMap.Focus", () => BuildFeedback.ApplyFocus(entity));
		_focusedHandle = closest.Handle;
	}

	/// <summary>Repõe a opacidade normal da peça que estava em foco, se houver.</summary>
	private void RestoreFocus()
	{
		if (_focusedHandle == 0)
		{
			return;
		}

		Entity previous = Entity.FromHandle(_focusedHandle);
		if (previous != null && previous.Exists())
		{
			previous.ResetOpacity();
		}

		_focusedHandle = 0;
	}

	private void TryUseMapProp(MapProp mapProp)
	{
		bool flag = mapProp.CanBePickedUp && EditMode;
		if (flag || mapProp.Interactable)
		{
			if (flag)
			{
				Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)51);
			}
			if (mapProp.Interactable)
			{
				DisableAttackActions();
			}
			GameExtended.DisableWeaponWheel();
			string arg;
			if (flag)
			{
				arg = Lang.T("help.pickup_prop", mapProp.Id);
			}
			else
			{
				arg = ((!EditMode) ? Lang.T("help.edit_mode_required") : "");
			}
			UiExtended.DisplayHelpTextThisFrame($"{arg}" + string.Format("{0}", mapProp.Interactable ? Lang.T("help.interact_prop", mapProp.IsDoor ? Lang.T("help.lock_unlock") : Lang.T("help.interact")) : ""));
			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)24) && mapProp.Interactable)
			{
				Interacted?.Invoke(mapProp, PlayerInventory.Instance.ItemFromName(mapProp.Id));
			}
			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)51) && mapProp.CanBePickedUp && PlayerInventory.Instance.PickupItem(PlayerInventory.Instance.ItemFromName(mapProp.Id), ItemType.Item))
			{
				mapProp.Delete();
				_map.Remove(mapProp);
				ZombieVehicleSpawner.Instance.SpawnBlocker.Remove(mapProp.Position);
			}
		}
	}

	public bool Find(Prop prop)
	{
		return _map != null && _map.Contains(prop);
	}

	private static void DisableAttackActions()
	{
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)257);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)24);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)25);
	}

	public void NotifyListChanged()
	{
		_map.NotifyListChanged();
	}
}
