using GTA;
using ZombiesMod.Extensions;

namespace ZombiesMod.Zombies.ZombieTypes;

public class Walker : ZombiePed
{
	private readonly Ped _ped;

	public override string MovementStyle { get; set; } = "move_m@drunk@verydrunk";

	public Walker(int handle)
		: base(handle)
	{
		_ped = this;
	}

	public override void OnAttackTarget(Ped target)
	{
		if (((Entity)target).IsDead)
		{
			if (!((Entity)(object)_ped).IsPlayingAnim("amb@world_human_bum_wash@male@high@idle_a", "idle_b"))
			{
				_ped.Task.PlayAnimation("amb@world_human_bum_wash@male@high@idle_a", "idle_b", 8f, -1, (AnimationFlags)1);
			}
		}
		else if (!((Entity)(object)_ped).IsPlayingAnim("rcmbarry", "bar_1_teleport_aln"))
		{
			_ped.Task.PlayAnimation("rcmbarry", "bar_1_teleport_aln", 8f, 1000, (AnimationFlags)16);
			if (!((Entity)target).IsInvincible)
			{
				target.ApplyDamage(ZombiePed.ZombieDamage);
			}
			InfectTarget(target);
		}
	}

	public override void OnGoToTarget(Ped target)
	{
		_ped.Task.GoTo((Entity)(object)target);
	}
}
