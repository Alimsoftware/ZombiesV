using System;

namespace ZombiesMod;

[Serializable]
public class UsableItemEvent
{
	public ItemEvent Event;

	public object EventArgument;

	public UsableItemEvent(ItemEvent @event, object eventArgument)
	{
		Event = @event;
		EventArgument = eventArgument;
	}
}
