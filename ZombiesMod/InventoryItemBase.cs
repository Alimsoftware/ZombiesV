using System;
using LemonUI;
using LemonUI.Menus;
using LemonUI.TimerBars;
using LemonUI.Scaleform;
using ZombiesMod.Static;

namespace ZombiesMod;

[Serializable]
public class InventoryItemBase : IIdentifier
{
	[NonSerialized]
	public NativeItem MenuItem;

	public int Amount { get; set; }

	public int MaxAmount { get; set; }

	public string Id { get; set; }

	public string Description { get; set; }

	public InventoryItemBase(int amount, int maxAmount, string id, string description)
	{
		Amount = amount;
		MaxAmount = maxAmount;
		Id = id;
		Description = description;
	}

	public void CreateMenuItem()
	{
		MenuItem = new NativeItem(Lang.ItemName(Id), Lang.ItemDescription(Id, Description));
	}
}
