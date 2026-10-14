using System;
using System.Linq;
using GTA;
using GTA.Math;
using ZombiesMod.Extensions;
using ZombiesMod.Static;
using ZombiesMod.SurvivorTypes;
using GTA.Input;
using UI = GTA.UI;

namespace ZombiesMod.Scripts;

public class RecruitPeds : SafeScript
{
	public const float InteractDistance = 1.5f;

	private static Ped PlayerPed => Database.PlayerPed;

	private static Vector3 PlayerPosition
	{
		get
		{
			return Database.PlayerPosition;
		}
	}

	public RecruitPeds()
	{
		WrapEvents();
	}

	public const float SearchRadius = 2.5f;

	public const int MaxGroupMembers = 6;

	private static void OnTick(object sender, EventArgs eventArgs)
	{
		if (MenuConrtoller.ObjectPool.AreAnyVisible || Database.PlayerIsDead || PlayerPed.IsInVehicle()
			|| PlayerPed.PedGroup.MemberCount >= MaxGroupMembers)
		{
			return;
		}

		// O mais perto *recrutavel*, e nao o mais perto de todos: um zumbi ou um curioso
		// encostado no sobrevivente bloqueava o recrutamento sem aviso.
		Ped closest = World.GetClosest<Ped>(PlayerPosition, World.GetNearbyPeds(PlayerPed, SearchRadius).Where(IsRecruitable).ToArray());
		if (closest == null)
		{
			return;
		}

		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)23);
		UiExtended.DisplayHelpTextThisFrame(Lang.T("help.recruit_ped"));
		if (!Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)23))
		{
			return;
		}

		FriendlySurvivors.Instance?.RemovePed(closest);
		closest.Recruit(PlayerPed);
		if (PlayerPed.PedGroup.MemberCount >= MaxGroupMembers)
		{
			UI.Notification.Show(Lang.T("notify.max_guards_reached"), false);
		}
	}

	private static bool IsRecruitable(Ped ped)
	{
		return ped != null
			&& ped.Exists()
			&& !ped.IsDead
			&& !ped.IsPlayer
			&& ped.RelationshipGroup == Relationships.FriendlyRelationship
			&& !ped.IsInCombatAgainst(PlayerPed)
			&& (int)ped.GetRelationshipWithPed(PlayerPed) != 5
			&& !PlayerPed.PedGroup.Contains(ped);
	}
}
