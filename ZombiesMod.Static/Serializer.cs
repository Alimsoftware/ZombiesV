using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ZombiesMod.Static;

/// <summary>
/// Lê e grava os saves em JSON. Só instancia os tipos pedidos pelo chamador (os DTOs de
/// <see cref="SaveData"/>), nunca tipos escolhidos pelo ficheiro, por isso um .json vindo de
/// fora não consegue executar código.
/// </summary>
public static class Serializer
{
	private const int MaxBytes = 4 * 1024 * 1024;

	public static T Deserialize<T>(string path) where T : class
	{
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			if (new FileInfo(path).Length > MaxBytes)
			{
				throw new InvalidDataException("Save demasiado grande (> " + MaxBytes + " bytes).");
			}
			string json = File.ReadAllText(path, Encoding.UTF8);
			return new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 16 }.Deserialize<T>(json);
		}
		catch (Exception ex)
		{
			ModLogger.Error("Serializer.Deserialize " + path, ex);
			// Põe o save de lado: sem isto o próximo Serialize gravava um objecto vazio por cima.
			try
			{
				File.Delete(path + ".bad");
				File.Move(path, path + ".bad");
			}
			catch (Exception moveEx)
			{
				ModLogger.Error("Serializer.Backup " + path, moveEx);
			}
			return null;
		}
	}

	public static void Serialize<T>(string path, T obj)
	{
		try
		{
			// Grava num temporário e troca: um crash a meio nunca deixa o save pela metade.
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			string tmp = path + ".tmp";
			string json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(obj);
			File.WriteAllText(tmp, json, new UTF8Encoding(false));
			if (File.Exists(path))
			{
				File.Replace(tmp, path, path + ".bak");
			}
			else
			{
				File.Move(tmp, path);
			}
		}
		catch (Exception ex)
		{
			ModLogger.Error("Serializer.Serialize " + path, ex);
		}
	}
}
