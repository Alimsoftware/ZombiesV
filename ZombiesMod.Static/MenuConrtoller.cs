using System;
using GTA;
using LemonUI;
using LemonUI.Menus;
using LemonUI.TimerBars;
using LemonUI.Scaleform;

namespace ZombiesMod.Static;

public class MenuConrtoller : SafeScript
{
	private static ObjectPool _menuPool;

	private static TimerBarCollection _barPool;

	public static ObjectPool ObjectPool
	{
		get
		{
			ObjectPool val = _menuPool;
			if (val == null)
			{
				ObjectPool val2 = new ObjectPool();
				_menuPool = val2;
				val = val2;
			}
			return val;
		}
	}

	public static TimerBarCollection BarPool
	{
		get
		{
			TimerBarCollection val = _barPool;
			if (val == null)
			{
				TimerBarCollection val2 = new TimerBarCollection();
				_barPool = val2;
				val = val2;
			}
			return val;
		}
	}

	public MenuConrtoller()
	{
		WrapEvents();
	}

	public void OnTick(object sender, EventArgs eventArgs)
	{
		if (_barPool != null && (_menuPool == null || (_menuPool != null && !_menuPool.AreAnyVisible)))
		{
			_barPool.Process();
		}
		ObjectPool ObjectPool = _menuPool;
		if (ObjectPool != null)
		{
			ObjectPool.Process();
		}
	}
}
