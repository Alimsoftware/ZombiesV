using System;
using GTA;
using GTA.Math;

namespace ZombiesMod;

[Serializable]
public class BuildableInventoryItem : InventoryItemBase, IProp, ICraftable
{
	public string PropName { get; set; }

	public BlipSprite BlipSprite { get; set; }

	public BlipColor BlipColor { get; set; }

	public Vector3 GroundOffset { get; set; }

	public bool Interactable { get; set; }

	public bool IsDoor { get; set; }

	/// <summary>
	/// Se true, a prop participa no encaixe de bordas — alinha-se a outras peças
	/// colocadas e detecta espaço ocupado. Para peças de perímetro (paredes, portões,
	/// barreiras), não para mobiliário solto.
	/// </summary>
	public bool IsSnappable { get; set; }

	public bool CanBePickedUp { get; set; }

	public CraftableItemComponent[] RequiredComponents { get; set; }

	public BuildableInventoryItem(int amount, int maxAmount, string id, string description, string propName, BlipSprite blipSprite, BlipColor blipColor, Vector3 groundOffset, bool interactable, bool isDoor, bool canBePickedUp, bool isSnappable = false)
		: base(amount, maxAmount, id, description)
	{
		PropName = propName;
		BlipSprite = blipSprite;
		BlipColor = blipColor;
		GroundOffset = groundOffset;
		Interactable = interactable;
		IsDoor = isDoor;
		IsSnappable = isSnappable;
		CanBePickedUp = canBePickedUp;
	}
}
