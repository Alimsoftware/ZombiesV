using System.IO;
using GTA;
using UI = GTA.UI;

namespace ZombiesMod.Static;

public class Config
{
	public static string VersionId = "1.0.2d";

	// Tudo o que o mod cria vive aqui; fora da pasta só ficam a DLL e o .ini.
	public const string ModFolder = "./scripts/ZombiesMod/";

	public const string ScriptFilePath = ModFolder + "items/";

	public const string IniFilePath = "./scripts/ZombiesMod.ini";

	public const string InventoryFilePath = ModFolder + "saves/Inventory.json";

	public const string MapFilePath = ModFolder + "saves/Map.json";

	public const string WorldFilePath = ModFolder + "saves/World.json";

	public const string VehicleFilePath = ModFolder + "saves/Vehicles.json";

	public const string GuardsFilePath = ModFolder + "saves/Guards.json";

	public static void Check()
	{
		ScriptSettings val = ScriptSettings.Load("./scripts/ZombiesMod.ini");
		if (!(val.GetValue("mod", "version_id", "0") == VersionId))
		{
			UI.Notification.Show(Lang.T("notify.updating_version", VersionId), false);
			val.SetValue("mod", "version_id", VersionId);
			val.Save();
		}
	}
}
