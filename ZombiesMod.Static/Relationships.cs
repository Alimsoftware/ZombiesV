using GTA;

namespace ZombiesMod.Static;

public class Relationships
{
	public static RelationshipGroup InfectedRelationship;

	public static RelationshipGroup FriendlyRelationship;

	public static RelationshipGroup MilitiaRelationship;

	public static RelationshipGroup HostileRelationship;

	public static RelationshipGroup PlayerRelationship;

	static Relationships()
	{
	}

	public static void SetRelationships()
	{
		InfectedRelationship = World.AddRelationshipGroup("Zombie");
		FriendlyRelationship = World.AddRelationshipGroup("Friendly");
		MilitiaRelationship = World.AddRelationshipGroup("Private_Militia");
		HostileRelationship = World.AddRelationshipGroup("Hostile");
		PlayerRelationship = Database.PlayerPed.RelationshipGroup;
		SetRelationshipBothWays((Relationship)5, InfectedRelationship, FriendlyRelationship);
		SetRelationshipBothWays((Relationship)5, InfectedRelationship, MilitiaRelationship);
		SetRelationshipBothWays((Relationship)5, InfectedRelationship, HostileRelationship);
		SetRelationshipBothWays((Relationship)5, InfectedRelationship, PlayerRelationship);
		SetRelationshipBothWays((Relationship)5, FriendlyRelationship, MilitiaRelationship);
		SetRelationshipBothWays((Relationship)5, FriendlyRelationship, HostileRelationship);
		SetRelationshipBothWays((Relationship)5, HostileRelationship, MilitiaRelationship);
		SetRelationshipBothWays((Relationship)5, HostileRelationship, PlayerRelationship);
		SetRelationshipBothWays((Relationship)5, PlayerRelationship, MilitiaRelationship);
		SetRelationshipBothWays((Relationship)2, PlayerRelationship, FriendlyRelationship);
		Database.PlayerPed.IsPriorityTargetForEnemies = true;
	}

	public static void SetRelationshipBothWays(Relationship rel, RelationshipGroup group1, RelationshipGroup group2)
	{
		group1.SetRelationshipBetweenGroups(group2, rel, false);
		group2.SetRelationshipBetweenGroups(group1, rel, false);
	}
}
