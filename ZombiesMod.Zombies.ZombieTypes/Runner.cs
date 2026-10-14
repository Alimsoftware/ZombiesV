using System;
using GTA;
using GTA.Math;
using GTA.Native;
using ZombiesMod.Extensions;
using ZombiesMod.Static;

namespace ZombiesMod.Zombies.ZombieTypes;

public class Runner : ZombiePed
{
	private readonly Ped _ped;

	/// <summary>
	/// Intervalo mínimo entre saltos-ataque. Antes era um bool que ficava true no
	/// primeiro salto e nunca mais voltava a false, o que dava exactamente um salto
	/// por Runner, por vida — e passava a degradar-se em silêncio.
	/// </summary>
	private const double JumpAttackCooldownSeconds = 6.0;

	private DateTime _nextJumpAttack = DateTime.MinValue;

	/// <summary>Uma tentativa de salto por ciclo de ataque (~1 s), nao uma por frame.</summary>
	private const double JumpRollIntervalSeconds = 1.2;

	private DateTime _nextJumpRoll = DateTime.MinValue;

	private bool CanJumpAttack => DateTime.UtcNow >= _nextJumpAttack;

	public override bool PlayAudio { get; set; } = true;

	public override string MovementStyle { get; set; } = "move_m@injured";

	public Runner(int handle)
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
		else if (CanJumpAttack && RollJumpAttack() && !target.IsPerformingStealthKill && !target.IsGettingUp && !target.IsRagdoll)
		{
			_ped.Jump();
			Ped ped = _ped;
			Vector3 val = ((Entity)target).Position - _ped.Position;
			((Entity)ped).Heading = val.ToHeading();
			_nextJumpAttack = DateTime.UtcNow.AddSeconds(JumpAttackCooldownSeconds);
			target.SetToRagdoll(2000);
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

	private bool RollJumpAttack()
	{
		if (DateTime.UtcNow < _nextJumpRoll)
		{
			return false;
		}
		_nextJumpRoll = DateTime.UtcNow.AddSeconds(JumpRollIntervalSeconds);
		return Database.Random.NextDouble() < 0.3;
	}

	public override void OnGoToTarget(Ped target)
	{
		Function.Call(Hash.TASK_GO_TO_ENTITY, new InputArgument[7]
		{
			((Entity)_ped).Handle,
			((Entity)target).Handle,
			-1,
			0f,
			5f,
			1073741824,
			0
		});
	}
}
