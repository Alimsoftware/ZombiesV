using System;
using System.Linq;
using GTA;
using ZombiesMod.Extensions;
using ZombiesMod.Scripts;
using ZombiesMod.Static;
using ZombiesMod.Wrappers;

namespace ZombiesMod.Zombies;

public abstract class ZombiePed : IEquatable<Ped>
{
	public delegate void OnGoingToTargetEvent(Ped target);

	public delegate void OnAttackingTargetEvent(Ped target);

	public const int MovementUpdateInterval = 5;

	public static int ZombieDamage = 15;

	public static float SensingRange = 120f;

	public static float SilencerEffectiveRange = 15f;

	public static float BehindZombieNoticeDistance = 5f;

	public static float RunningNoticeDistance = 25f;

	public static float AttackRange = 1.2f;

	public static float VisionDistance = 35f;

	public static float WanderRadius = 100f;

	private Ped _target;

	private readonly Ped _ped;

	private EntityEventWrapper _eventWrapper;

	private bool _goingToTarget;

	private bool _attackingTarget;

	private DateTime _currentMovementUpdateTime;

	public virtual bool PlayAudio { get; set; }

	public Ped Target
	{
		get
		{
			return _target;
		}
		private set
		{
			if ((Entity)(object)value == (Entity)null && (Entity)(object)_target != (Entity)null)
			{
				_ped.Task.WanderAround(_ped.Position, WanderRadius);
				bool goingToTarget = (AttackingTarget = false);
				GoingToTarget = goingToTarget;
			}
			_target = value;
		}
	}

	public bool GoingToTarget
	{
		get
		{
			return _goingToTarget;
		}
		set
		{
			if (value && !_goingToTarget)
			{
				GoToTarget?.Invoke(Target);
			}
			_goingToTarget = value;
		}
	}

	public bool AttackingTarget
	{
		get
		{
			return _attackingTarget;
		}
		set
		{
			if (value && !_ped.IsRagdoll && !_ped.IsDead && !_ped.IsClimbing && !_ped.IsFalling && !_ped.IsBeingStunned && !_ped.IsGettingUp)
			{
				AttackTarget?.Invoke(Target);
			}
			_attackingTarget = value;
		}
	}

	public virtual string MovementStyle { get; set; }

	public event OnGoingToTargetEvent GoToTarget;

	public event OnAttackingTargetEvent AttackTarget;

	protected ZombiePed(int handle)
	{
		// Entity.FromHandle devolve null para um handle velho, e devolve outro tipo de
		// entidade se o handle não for de um ped. Sem isto o construtor seguia a ligar
		// eventos a um ped nulo e o resultado era um zombie inerte que morria sozinho no
		// primeiro tick, sem ninguém perceber porquê.
		Ped ped = Entity.FromHandle(handle) as Ped;

		if (ped == null || !ped.Exists())
		{
			throw new ArgumentException(
				string.Format("Handle {0} não corresponde a um ped vivo.", handle),
				nameof(handle));
		}

		_ped = ped;
		_eventWrapper = new EntityEventWrapper((Entity)(object)_ped);
		_eventWrapper.Died += OnDied;
		_eventWrapper.Updated += Update;
		_eventWrapper.Aborted += Abort;
		_currentMovementUpdateTime = DateTime.UtcNow;
		GoToTarget += OnGoToTarget;
		AttackTarget += OnAttackTarget;
	}

	public abstract void OnAttackTarget(Ped target);

	public abstract void OnGoToTarget(Ped target);

	private void OnDied(EntityEventWrapper sender, Entity entity)
	{
		Blip currentBlip = _ped.AttachedBlip;
		if (currentBlip != null)
		{
			currentBlip.Delete();
		}
		if (ZombieVehicleSpawner.Instance.IsInvalidZone(entity.Position) && ZombieVehicleSpawner.Instance.IsValidSpawn(entity.Position))
		{
			ZombieVehicleSpawner.Instance.SpawnBlocker.Add(entity.Position);
		}
	}

	public void Update(EntityEventWrapper entityEventWrapper, Entity entity)
	{
		// Limpeza primeiro, e independentemente de estar morto: é ela que liberta o
		// wrapper, porque a entidade deixa de existir e o wrapper desfaz-se sozinho.
		if (_ped.Position.VDist(Database.PlayerPosition) > 120f && (!_ped.IsOnScreen || _ped.IsDead))
		{
			_ped.Delete();
		}
		if (_ped.IsDead)
		{
			// AI completa em cadáveres — alvo, tarefas, fala — todos os frames até o
			// corpo ser apagado a >120 m. Só interessa manter a limpeza acima.
			ForgetTarget();
			return;
		}
		if (PlayAudio && _ped.IsRunning)
		{
			_ped.DisablePainAudio(toggle: false);
			_ped.PlayPain(8);
			_ped.PlayFacialAnim("facials@gen_male@base", "burning_1");
		}
		GetTarget();
		SetWalkStyle();
		if (((Entity)_ped).IsOnFire && !((Entity)_ped).IsDead)
		{
			_ped.Kill();
		}
		_ped.StopAmbientSpeechThisFrame();
		if (!PlayAudio)
		{
			_ped.StopSpeaking(shaking: true);
		}
		if (!((Entity)(object)Target == (Entity)null))
		{
			float num = _ped.Position.VDist(((Entity)Target).Position);
			if (num > AttackRange)
			{
				AttackingTarget = false;
				GoingToTarget = true;
			}
			else
			{
				AttackingTarget = true;
				GoingToTarget = false;
			}
		}
	}

	public void Abort(EntityEventWrapper sender, Entity entity)
	{
		_ped.Delete();
	}

	public void InfectTarget(Ped target)
	{
		if (!target.IsPlayer && ((Entity)target).Health <= ((Entity)target).MaxHealth / 4)
		{
			target.SetToRagdoll(3000);
			ZombieCreator.InfectPed(target, _ped.MaxHealth, overrideAsFastZombie: true);
			ForgetTarget();
			target.LeaveGroup();
			target.Weapons.Drop();
			EntityEventWrapper.Dispose((Entity)(object)target);
		}
	}

	public void ForgetTarget()
	{
		_target = null;
	}

	private void SetWalkStyle()
	{
		if (!(DateTime.UtcNow <= _currentMovementUpdateTime))
		{
			_ped.SetMovementAnimSet(MovementStyle);
			UpdateTime();
		}
	}

	private void UpdateTime()
	{
		_currentMovementUpdateTime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 5);
	}

	private void GetTarget()
	{
		Ped[] array = World.GetNearbyPeds(_ped, SensingRange).Where(IsGoodTarget).ToArray();
		Ped closest = World.GetClosest<Ped>(_ped.Position, array);
		if ((Entity)(object)closest != (Entity)null && (((Entity)(object)_ped).HasClearLineOfSight((Entity)(object)closest, VisionDistance) || CanHearPed(closest)))
		{
			Target = closest;
		}
		else if ((Entity)(object)Target != (Entity)null && (!IsGoodTarget(Target) || ((Entity)Target).IsDead))
		{
			// Só se larga o alvo quando ele mesmo deixou de servir. A condição anterior
			// tinha um «|| closest != Target» que largava um alvo válido todas as vezes
			// que outro ped odiado ficava mais perto ou se perdia a linha de visão — o
			// zombie andava a re-alvo sem parar.
			Target = null;
		}
	}

	private bool CanHearPed(Ped ped)
	{
		float distance = ((Entity)ped).Position.VDist(_ped.Position);
		return !IsWeaponWellSilenced(ped, distance) || IsBehindZombie(distance) || IsRunningNoticed(ped, distance);
	}

	private static bool IsRunningNoticed(Ped ped, float distance)
	{
		return ped.IsSprinting && distance < RunningNoticeDistance;
	}

	private static bool IsBehindZombie(float distance)
	{
		return distance < BehindZombieNoticeDistance;
	}

	private static bool IsWeaponWellSilenced(Ped ped, float distance)
	{
		if (!ped.IsShooting)
		{
			return true;
		}
		return ped.IsCurrentWeaponSileced() && distance > SilencerEffectiveRange;
	}

	private bool IsGoodTarget(Ped ped)
	{
		return ped != null && ped.Exists() && (int)ped.GetRelationshipWithPed(_ped) == 5;
	}

	protected bool Equals(ZombiePed other)
	{
		return other != null && object.Equals(_ped, other._ped);
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (this == obj)
		{
			return true;
		}
		return obj.GetType() == ((object)this).GetType() && Equals((ZombiePed)obj);
	}

	public override int GetHashCode()
	{
		return _ped.GetHashCode();
	}

	public bool Equals(Ped other)
	{
		return object.Equals(_ped, other);
	}

	public static implicit operator Ped(ZombiePed v)
	{
		return v._ped;
	}
}
