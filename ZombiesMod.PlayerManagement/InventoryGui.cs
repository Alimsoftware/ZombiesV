using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.Input;
using GTA.Native;
using ZombiesMod.Extensions;
using ZombiesMod.Static;
using ContainerElement = GTA.UI.ContainerElement;
using TextElement = GTA.UI.TextElement;
using UiAlign = GTA.UI.Alignment;
using UiFont = GTA.UI.Font;
using UiScreen = GTA.UI.Screen;

namespace ZombiesMod.PlayerManagement;

/// <summary>
/// Inventario em grelha, com cursor do mouse (U). Experimental: convive com o menu antigo
/// (I) e usa o mesmo <see cref="Inventory"/>, entao nada do save muda.
/// Esquerdo usa, direito cria, roda rola.
/// </summary>
public class InventoryGui : SafeScript
{
	private readonly Keys _openKey = Keys.U;

	private const int Columns = 6;
	private const int VisibleRows = 6;
	private const float CellW = 100f;
	private const float CellH = 54f;
	private const float Gap = 4f;
	private const float Pad = 12f;
	private const float HeaderH = 30f;
	private const float InfoH = 100f;
	private const float ScreenH = 720f;


	private struct Entry
	{
		public InventoryItemBase Item;
		public ItemType Type;
	}

	private bool _open;
	private int _scrollRow;

	public InventoryGui()
	{
		_openKey = Settings.GetValue<Keys>("keys", "inventory_gui_key", _openKey);
		Settings.SetValue<Keys>("keys", "inventory_gui_key", _openKey);
		Settings.Save();
		WrapEvents();
	}

	private void OnKeyUp(object sender, KeyEventArgs e)
	{
		if (e.KeyCode != _openKey)
		{
			return;
		}
		if (_open)
		{
			_open = false;
		}
		else if (!MenuConrtoller.ObjectPool.AreAnyVisible && !Database.PlayerIsDead && PlayerInventory.Instance != null)
		{
			_open = true;
			_scrollRow = 0;
		}
	}

	private static bool EditMode => PlayerMap.Instance != null && PlayerMap.Instance.EditMode;

	private static List<Entry> BuildEntries(Inventory inventory)
	{
		List<Entry> list = new List<Entry>();
		foreach (InventoryItemBase item in inventory.Items.Where(i => i.Amount > 0 || PlayerInventory.IsFreeBuild(i)))
		{
			list.Add(new Entry { Item = item, Type = ItemType.Item });
		}
		foreach (InventoryItemBase item in inventory.Resources.Where(i => i.Amount > 0))
		{
			list.Add(new Entry { Item = item, Type = ItemType.Resource });
		}
		return list;
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (!_open)
		{
			return;
		}
		Inventory inventory = PlayerInventory.Instance?.Backpack;
		if (inventory == null || Database.PlayerIsDead || MenuConrtoller.ObjectPool.AreAnyVisible || Game.IsPaused)
		{
			_open = false;
			return;
		}

		Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
		Function.Call(Hash.SET_MOUSE_CURSOR_THIS_FRAME);

		float width = UiScreen.ScaledWidth;
		float mouseX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)ControlAction.CursorX) * width;
		float mouseY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)ControlAction.CursorY) * ScreenH;

		List<Entry> entries = BuildEntries(inventory);
		int totalRows = Math.Max(1, (entries.Count + Columns - 1) / Columns);
		int maxScroll = Math.Max(0, totalRows - VisibleRows);
		if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, ControlAction.WeaponWheelNext))
		{
			_scrollRow = Math.Min(maxScroll, _scrollRow + 1);
		}
		else if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, ControlAction.WeaponWheelPrev))
		{
			_scrollRow = Math.Max(0, _scrollRow - 1);
		}
		_scrollRow = Math.Min(_scrollRow, maxScroll);

		float gridW = Columns * (CellW + Gap) - Gap;
		float gridH = VisibleRows * (CellH + Gap) - Gap;
		float panelW = gridW + 2f * Pad;
		float panelH = HeaderH + gridH + InfoH + 2f * Pad;
		float panelX = (width - panelW) / 2f;
		float panelY = (ScreenH - panelH) / 2f;
		float gridX = panelX + Pad;
		float gridY = panelY + HeaderH + Pad;

		Draw(panelX, panelY, panelW, panelH, Color.FromArgb(215, 12, 12, 12));
		DrawText(Lang.T(EditMode ? "inv_gui.title_edit" : "inv_gui.title"), panelX + Pad, panelY + 6f, 0.5f, Color.White);

		int hovered = -1;
		float localX = mouseX - gridX;
		float localY = mouseY - gridY;
		if (localX >= 0f && localY >= 0f && localX < gridW && localY < gridH)
		{
			int col = (int)(localX / (CellW + Gap));
			int row = (int)(localY / (CellH + Gap));
			bool insideCell = localX - col * (CellW + Gap) < CellW && localY - row * (CellH + Gap) < CellH;
			int index = (_scrollRow + row) * Columns + col;
			if (insideCell && index < entries.Count)
			{
				hovered = index;
			}
		}

		for (int row = 0; row < VisibleRows; row++)
		{
			for (int col = 0; col < Columns; col++)
			{
				int index = (_scrollRow + row) * Columns + col;
				float cx = gridX + col * (CellW + Gap);
				float cy = gridY + row * (CellH + Gap);
				if (index >= entries.Count)
				{
					Draw(cx, cy, CellW, CellH, Color.FromArgb(70, 40, 40, 40));
					continue;
				}
				InventoryItemBase item = entries[index].Item;
				bool isHovered = index == hovered;
				Color back = entries[index].Type == ItemType.Item
					? Color.FromArgb(isHovered ? 230 : 170, isHovered ? 90 : 55, isHovered ? 110 : 62, isHovered ? 70 : 48)
					: Color.FromArgb(isHovered ? 230 : 170, isHovered ? 100 : 60, isHovered ? 90 : 55, isHovered ? 60 : 45);
				Draw(cx, cy, CellW, CellH, back);
				DrawText(Truncate(Lang.ItemName(item.Id), 15), cx + 5f, cy + 5f, 0.32f, Color.White);
				DrawText(PlayerInventory.IsFreeBuild(item) ? Lang.T("inv_gui.free") : item.Amount + "/" + item.MaxAmount, cx + 5f, cy + 30f, 0.34f, Color.FromArgb(255, 220, 190, 90));
			}
		}

		if (maxScroll > 0)
		{
			DrawText((_scrollRow + 1) + "/" + (maxScroll + 1), panelX + panelW - 52f, panelY + 8f, 0.3f, Color.Gray);
		}

		float infoY = gridY + gridH + Pad;
		if (hovered >= 0)
		{
			DrawInfo(entries[hovered].Item, panelX + Pad, infoY, gridW);
			HandleClick(inventory, entries[hovered]);
		}
		else
		{
			DrawText(Lang.T("inv_gui.hint", _openKey), panelX + Pad, infoY + 4f, 0.32f, Color.Gray);
		}
	}

	private void HandleClick(Inventory inventory, Entry entry)
	{
		bool left = Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, ControlAction.Attack)
			|| Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, ControlAction.CursorAccept);
		bool right = Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, ControlAction.Aim)
			|| Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, ControlAction.CursorCancel);
		if (left)
		{
			// Fecha antes: construiveis bloqueiam o script no preview.
			_open = false;
			// Gratis: garante 1 para o Use e repoe a quantidade original depois (o Use e sincrono).
			int original = entry.Item.Amount;
			if (PlayerInventory.IsFreeBuild(entry.Item) && original < 1)
			{
				entry.Item.Amount = 1;
			}
			try
			{
				inventory.Use(entry.Item, entry.Type);
			}
			finally
			{
				if (PlayerInventory.IsFreeBuild(entry.Item))
				{
					entry.Item.Amount = original;
				}
			}
		}
		else if (right && !PlayerInventory.IsFreeBuild(entry.Item) && entry.Item is ICraftable)
		{
			inventory.TryCraftItem(entry.Item);
		}
	}

	private static void DrawInfo(InventoryItemBase item, float x, float y, float width)
	{
		DrawText(Lang.ItemName(item.Id), x, y, 0.45f, Color.White);
		StringBuilder text = new StringBuilder(Lang.ItemDescription(item.Id, item.Description) ?? string.Empty);
		if (item is ICraftable { RequiredComponents: { Length: > 0 } components })
		{
			text.Append("  |  ").Append(Lang.T("inv_gui.recipe")).Append(" ");
			text.Append(string.Join(", ", components.Select(c => Lang.ItemName(c.Resource.Id) + " " + c.Resource.Amount + "/" + c.RequiredAmount)));
		}
		new TextElement(text.ToString(), new PointF(x, y + 24f), 0.32f, Color.LightGray, UiFont.ChaletLondon, UiAlign.Left, false, false, width).Draw();
	}

	private static void Draw(float x, float y, float w, float h, Color color)
	{
		new ContainerElement(new PointF(x, y), new SizeF(w, h), color).Draw();
	}

	private static void DrawText(string text, float x, float y, float scale, Color color)
	{
		new TextElement(text, new PointF(x, y), scale, color, UiFont.ChaletLondon, UiAlign.Left).Draw();
	}

	private static string Truncate(string text, int max)
	{
		return text.Length <= max ? text : text.Substring(0, max - 2) + "..";
	}
}
