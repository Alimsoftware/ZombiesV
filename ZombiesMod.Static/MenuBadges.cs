using LemonUI.Menus;

namespace ZombiesMod.Static;

/// <summary>
/// Badges do menu com as mesmas texturas que o NativeUI resolvia internamente
/// a partir do enum <c>BadgeStyle</c>.
///
/// Valores e texturas extraídos por reflexão de NativeUI.dll 1.9.0.0, invocando
/// os métodos internos <c>UIMenuItem.BadgeToSpriteLib</c> e <c>BadgeToSpriteName</c>.
/// O LemonUI não tem enum equivalente: ele exige um <see cref="BadgeSet"/> explícito.
/// </summary>
public static class MenuBadges
{
	/// <summary>NativeUI.BadgeStyle.Car = 12 — usado para itens de veículo.</summary>
	public static readonly BadgeSet Car = new("commonmenu", "shop_garage_icon_a", "shop_garage_icon_a");

	/// <summary>NativeUI.BadgeStyle.Heart = 14 — usado para "Carregar" (vida/partida).</summary>
	public static readonly BadgeSet Heart = new("commonmenu", "shop_health_icon_a", "shop_health_icon_a");

	/// <summary>NativeUI.BadgeStyle.Mask = 16 — usado para o grupo de peds.</summary>
	public static readonly BadgeSet Mask = new("commonmenu", "shop_mask_icon_a", "shop_mask_icon_a");

	/// <summary>NativeUI.BadgeStyle.Lock = 21 — item ainda não fabricável.</summary>
	public static readonly BadgeSet Lock = new("commonmenu", "shop_lock", "shop_lock");

	/// <summary>NativeUI.BadgeStyle.Tick = 22 — item fabricável.</summary>
	public static readonly BadgeSet Tick = new("commonmenu", "shop_tick_icon", "shop_tick_icon");
	
	/// <summary>NativeUI.BadgeStyle.Ammo = 6 - itens do menu de fabricacao de armas.</summary>
	public static readonly BadgeSet Ammo = new("commonmenu", "shop_ammo_icon_a", "shop_ammo_icon_a");
}
