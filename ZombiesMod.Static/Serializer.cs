using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ZombiesMod.Static;

public static class Serializer
{
	public static T Deserialize<T>(string path)
	{
		T result = default;
		if (!File.Exists(path))
		{
			return result;
		}
		try
		{
			FileStream fileStream = new FileStream(path, FileMode.Open);
			BinaryFormatter binaryFormatter = new BinaryFormatter();
			result = (T)binaryFormatter.Deserialize(fileStream);
			fileStream.Close();
		}
		catch (Exception ex)
		{
			File.WriteAllText("./scripts/ZombiesModCrashLog.txt", $"\n[{DateTime.UtcNow.ToShortDateString()}] {ex.Message}");
		}
		return result;
	}

	public static void Serialize<T>(string path, T obj)
	{
		try
		{
			FileStream fileStream = new FileStream(path, FileMode.Create);
			BinaryFormatter binaryFormatter = new BinaryFormatter();
			binaryFormatter.Serialize(fileStream, obj);
			fileStream.Close();
		}
		catch (Exception ex)
		{
			File.WriteAllText("./scripts/ZombiesModCrashLog.txt", $"\n[{DateTime.UtcNow.ToShortDateString()}] {ex.Message}");
		}
	}
}
