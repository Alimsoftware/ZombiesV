using System;
using System.Collections.Generic;

namespace ZombiesMod;

[Serializable]
public class Stats
{
	public List<Stat> StatList;

	public Stats()
	{
		StatList = new List<Stat>
		{
			new Stat
			{
				Name = "Fome",
				Value = 1f,
				MaxVal = 1f
			},
			new Stat
			{
				Name = "Sede",
				Value = 1f,
				MaxVal = 1f
			},
			new Stat
			{
				Name = "Vigor",
				Value = 1f,
				MaxVal = 1f
			}
		};
	}
}
