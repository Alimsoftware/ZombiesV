using System;
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

	private static void OnTick(object sender, EventArgs eventArgs)
	{
		if (MenuConrtoller.ObjectPool.AreAnyVisible || PlayerPed.PedGroup.MemberCount >= 6)
		{
			return;
		}
		Ped[] nearbyPeds = World.GetNearbyPeds(PlayerPed, 1.5f);
		Ped closest = World.GetClosest<Ped>(PlayerPosition, nearbyPeds);
		if ((Entity)(object)closest == (Entity)null || ((Entity)closest).IsDead || closest.IsInCombatAgainst(PlayerPed) || (int)closest.GetRelationshipWithPed(PlayerPed) == 5 || closest.RelationshipGroup != Relationships.FriendlyRelationship || closest.PedGroup == PlayerPed.PedGroup)
		{
			return;
		}
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)23);
		UiExtended.DisplayHelpTextThisFrame(Lang.T("help.recruit_ped"));
		if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, (ControlAction)23))
		{
			if (FriendlySurvivors.Instance != null)
			{
				FriendlySurvivors.Instance.RemovePed(closest);
			}
			closest.Recruit(PlayerPed);
			if (PlayerPed.PedGroup.MemberCount >= 6)
			{
				UI.Notification.Show(Lang.T("notify.max_guards_reached"), false);
			}
		}
	}
}
