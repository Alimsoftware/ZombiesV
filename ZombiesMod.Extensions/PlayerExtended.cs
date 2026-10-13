using GTA;
using GTA.Native;

namespace ZombiesMod.Extensions;

public static class PlayerExtended
{
	public static void IgnoreLowPriorityShockingEvents(this Player player, bool toggle)
	{
		Function.Call(Hash.SET_IGNORE_LOW_PRIORITY_SHOCKING_EVENTS, new InputArgument[2]
		{
			player.Handle,
			toggle ? 1 : 0
		});
	}
}
