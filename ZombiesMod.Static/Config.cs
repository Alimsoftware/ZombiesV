using System.IO;
using GTA;
using UI = GTA.UI;

namespace ZombiesMod.Static;

public class Config
{
	public static string VersionId = "1.0.2d";

	public const string ScriptFilePath = "./scripts/";

	public const string IniFilePath = "./scripts/ZombiesMod.ini";

	public const string InventoryFilePath = "./scripts/Inventory.dat";

	public const string MapFilePath = "./scripts/Map.dat";

	public const string VehicleFilePath = "./scripts/Vehicles.dat";

	public const string GuardsFilePath = "./scripts/Guards.dat";

	public static void Check()
	{
		ScriptSettings val = ScriptSettings.Load("./scripts/ZombiesMod.ini");
		if (!(val.GetValue("mod", "version_id", "0") == VersionId))
		{
			if (File.Exists("./scripts/ZombiesMod.ini"))
			{
				File.Delete("./scripts/ZombiesMod.ini");
			}
			if (File.Exists("./scripts/Inventory.dat"))
			{
				File.Delete("./scripts/Inventory.dat");
			}
			UI.Notification.Show(Lang.T("notify.updating_version", VersionId), false);
			val.SetValue("mod", "version_id", VersionId);
			val.Save();
		}
	}
}
