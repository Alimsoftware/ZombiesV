using System;
using System.Collections.Generic;
using System.Linq;
using GTA.Math;

namespace ZombiesMod.Static;

public class WorldSave
{
	public int Seed;

	/// <summary>Eventos já resolvidos pelo jogador ("h:x,y" horda, "e:x,y" encontro).</summary>
	public List<string> Cleared = new List<string>();
}

/// <summary>O que a semente põe numa célula do mapa. Uma célula tem no máximo uma coisa.</summary>
public enum CellContent
{
	None,
	Horde,
	Friendly,
	Hostile,
	Merryweather,
	Herd
}

/// <summary>
/// Geração procedural do mundo: o mapa é dividido em células e tudo o que a célula
/// "contém" (densidade, hordas, acampamentos, hostis, patrulhas, manadas) é função de
/// (semente, célula). Mesma semente, mesmo mundo. Sem estado: nada aqui precisa de ser
/// gravado além da semente e dos eventos que o jogador já resolveu.
/// </summary>
public static class WorldNoise
{
	public const float CellSize = 150f;

	/// <summary>Multiplicador da população máxima: 0.2 = zona quase morta, 1.8 = infestada.</summary>
	public const float MinDensity = 0.2f;

	public const float MaxDensity = 1.8f;

	public const int HerdSpecies = 5;

	// Limites acumulados do sorteio da célula (um único sorteio, por isso nunca há dois eventos na mesma).
	private const float HordeLimit = 0.04f;
	private const float FriendlyLimit = 0.065f;
	private const float HostileLimit = 0.085f;
	private const float MerryweatherLimit = 0.095f;
	private const float HerdLimit = 0.155f;

	private static uint Hash(int seed, int x, int y, uint salt)
	{
		unchecked
		{
			uint h = (uint)seed * 374761393u + (uint)x * 668265263u + (uint)y * 2246822519u + salt * 3266489917u;
			h = (h ^ (h >> 13)) * 1274126177u;
			return h ^ (h >> 16);
		}
	}

	private static float Unit(uint hash)
	{
		return (hash & 0xFFFFFFu) / (float)0x1000000;
	}

	private static float Smooth(float t)
	{
		return t * t * (3f - 2f * t);
	}

	public static int Cell(float coordinate)
	{
		return (int)Math.Floor(coordinate / CellSize);
	}

	/// <summary>Ruído de valores em [0, 1], contínuo entre células vizinhas.</summary>
	public static float Noise(int seed, float x, float y)
	{
		float gx = x / CellSize;
		float gy = y / CellSize;
		int x0 = (int)Math.Floor(gx);
		int y0 = (int)Math.Floor(gy);
		float tx = Smooth(gx - x0);
		float ty = Smooth(gy - y0);
		float a = Unit(Hash(seed, x0, y0, 1));
		float b = Unit(Hash(seed, x0 + 1, y0, 1));
		float c = Unit(Hash(seed, x0, y0 + 1, 1));
		float d = Unit(Hash(seed, x0 + 1, y0 + 1, 1));
		float top = a + (b - a) * tx;
		float bottom = c + (d - c) * tx;
		return top + (bottom - top) * ty;
	}

	public static float Density(int seed, float x, float y)
	{
		return MinDensity + (MaxDensity - MinDensity) * Noise(seed, x, y);
	}

	public static CellContent Content(int seed, int cellX, int cellY)
	{
		float roll = Unit(Hash(seed, cellX, cellY, 2));
		if (roll < HordeLimit) return CellContent.Horde;
		if (roll < FriendlyLimit) return CellContent.Friendly;
		if (roll < HostileLimit) return CellContent.Hostile;
		if (roll < MerryweatherLimit) return CellContent.Merryweather;
		if (roll < HerdLimit) return CellContent.Herd;
		return CellContent.None;
	}

	/// <summary>Hordas: 12 a 30 zumbis.</summary>
	public static bool TryGetHorde(int seed, int cellX, int cellY, out int size)
	{
		size = 0;
		if (Content(seed, cellX, cellY) != CellContent.Horde)
		{
			return false;
		}
		size = 12 + (int)(Unit(Hash(seed, cellX, cellY, 3)) * 19f);
		return true;
	}

	/// <summary>Manadas: espécie (índice 0..HerdSpecies-1) e 3 a 9 animais.</summary>
	public static bool TryGetHerd(int seed, int cellX, int cellY, out int species, out int size)
	{
		species = 0;
		size = 0;
		if (Content(seed, cellX, cellY) != CellContent.Herd)
		{
			return false;
		}
		species = Math.Min(HerdSpecies - 1, (int)(Unit(Hash(seed, cellX, cellY, 4)) * HerdSpecies));
		size = 3 + (int)(Unit(Hash(seed, cellX, cellY, 5)) * 7f);
		return true;
	}
}

/// <summary>A semente do mundo e os eventos resolvidos: tudo em <c>saves\World.json</c>, fixo por save.</summary>
public static class WorldSeed
{
	// Teto para um World.json adulterado não encher a memória.
	private const int MaxCleared = 20000;

	private static readonly HashSet<string> Cleared = new HashSet<string>();

	private static bool _loaded;
	private static int _seed;

	public static int Seed
	{
		get
		{
			EnsureLoaded();
			return _seed;
		}
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		WorldSave save = Serializer.Deserialize<WorldSave>(Config.WorldFilePath);
		if (save != null && save.Seed != 0)
		{
			_seed = save.Seed;
			foreach (string key in (save.Cleared ?? new List<string>()).Take(MaxCleared))
			{
				if (!string.IsNullOrEmpty(key) && key.Length <= 32)
				{
					Cleared.Add(key);
				}
			}
		}
		else
		{
			_seed = new Random().Next(1, int.MaxValue);
			Save();
		}
		ModLogger.Info("WorldSeed", "semente do mundo: " + _seed + " (" + Cleared.Count + " evento(s) resolvido(s)).");
	}

	private static void Save()
	{
		Serializer.Serialize(Config.WorldFilePath, new WorldSave { Seed = _seed, Cleared = Cleared.ToList() });
	}

	private static string Key(string kind, int cellX, int cellY)
	{
		return kind + ":" + cellX + "," + cellY;
	}

	public static bool IsCleared(string kind, int cellX, int cellY)
	{
		EnsureLoaded();
		return Cleared.Contains(Key(kind, cellX, cellY));
	}

	public static void MarkCleared(string kind, int cellX, int cellY)
	{
		EnsureLoaded();
		if (Cleared.Count < MaxCleared && Cleared.Add(Key(kind, cellX, cellY)))
		{
			Save();
		}
	}

	public static float Density(Vector3 position)
	{
		return WorldNoise.Density(Seed, position.X, position.Y);
	}

	public static bool TryGetHorde(int cellX, int cellY, out int size)
	{
		return WorldNoise.TryGetHorde(Seed, cellX, cellY, out size);
	}

	public static bool TryGetHerd(int cellX, int cellY, out int species, out int size)
	{
		return WorldNoise.TryGetHerd(Seed, cellX, cellY, out species, out size);
	}

	public static CellContent Content(int cellX, int cellY)
	{
		return WorldNoise.Content(Seed, cellX, cellY);
	}
}
