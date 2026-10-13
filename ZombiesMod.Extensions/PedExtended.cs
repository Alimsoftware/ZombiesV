using System;
using GTA;
using GTA.Native;
using ZombiesMod.Wrappers;

namespace ZombiesMod.Extensions;

public static class PedExtended
{
	internal static readonly string[] SpeechModifierNames = new string[37]
	{
		"SPEECH_PARAMS_STANDARD", "SPEECH_PARAMS_ALLOW_REPEAT", "SPEECH_PARAMS_BEAT", "SPEECH_PARAMS_FORCE", "SPEECH_PARAMS_FORCE_FRONTEND", "SPEECH_PARAMS_FORCE_NO_REPEAT_FRONTEND", "SPEECH_PARAMS_FORCE_NORMAL", "SPEECH_PARAMS_FORCE_NORMAL_CLEAR", "SPEECH_PARAMS_FORCE_NORMAL_CRITICAL", "SPEECH_PARAMS_FORCE_SHOUTED",
		"SPEECH_PARAMS_FORCE_SHOUTED_CLEAR", "SPEECH_PARAMS_FORCE_SHOUTED_CRITICAL", "SPEECH_PARAMS_FORCE_PRELOAD_ONLY", "SPEECH_PARAMS_MEGAPHONE", "SPEECH_PARAMS_HELI", "SPEECH_PARAMS_FORCE_MEGAPHONE", "SPEECH_PARAMS_FORCE_HELI", "SPEECH_PARAMS_INTERRUPT", "SPEECH_PARAMS_INTERRUPT_SHOUTED", "SPEECH_PARAMS_INTERRUPT_SHOUTED_CLEAR",
		"SPEECH_PARAMS_INTERRUPT_SHOUTED_CRITICAL", "SPEECH_PARAMS_INTERRUPT_NO_FORCE", "SPEECH_PARAMS_INTERRUPT_FRONTEND", "SPEECH_PARAMS_INTERRUPT_NO_FORCE_FRONTEND", "SPEECH_PARAMS_ADD_BLIP", "SPEECH_PARAMS_ADD_BLIP_ALLOW_REPEAT", "SPEECH_PARAMS_ADD_BLIP_FORCE", "SPEECH_PARAMS_ADD_BLIP_SHOUTED", "SPEECH_PARAMS_ADD_BLIP_SHOUTED_FORCE", "SPEECH_PARAMS_ADD_BLIP_INTERRUPT",
		"SPEECH_PARAMS_ADD_BLIP_INTERRUPT_FORCE", "SPEECH_PARAMS_FORCE_PRELOAD_ONLY_SHOUTED", "SPEECH_PARAMS_FORCE_PRELOAD_ONLY_SHOUTED_CLEAR", "SPEECH_PARAMS_FORCE_PRELOAD_ONLY_SHOUTED_CRITICAL", "SPEECH_PARAMS_SHOUTED", "SPEECH_PARAMS_SHOUTED_CLEAR", "SPEECH_PARAMS_SHOUTED_CRITICAL"
	};

	public static void PlayPain(this Ped ped, int type)
	{
		Function.Call(Hash.PLAY_PAIN, new InputArgument[4]
		{
			((Entity)ped).Handle,
			type,
			0,
			0
		});
	}

	public static void PlayFacialAnim(this Ped ped, string animSet, string animName)
	{
		Function.Call(Hash.PLAY_FACIAL_ANIM, new InputArgument[3]
		{
			((Entity)ped).Handle,
			animName,
			animSet
		});
	}

	public static bool HasBeenDamagedByMelee(this Ped ped)
	{
		return Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_WEAPON, new InputArgument[3]
		{
			((Entity)ped).Handle,
			0,
			1
		});
	}

	public static bool HasBeenDamagedBy(this Ped ped, WeaponHash weapon)
	{
		return Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_WEAPON, new InputArgument[3]
		{
			((Entity)ped).Handle,
			(int)weapon,
			0
		});
	}

	public unsafe static Bone LastDamagedBone(this Ped ped)
	{
		int num = default;
		if (Function.Call<bool>(Hash.GET_PED_LAST_DAMAGE_BONE, new InputArgument[2]
		{
			((Entity)ped).Handle,
			&num
		}))
		{
			return (Bone)num;
		}
		return (Bone)0;
	}

	public static void SetPathAvoidWater(this Ped ped, bool toggle)
	{
		Function.Call(Hash.SET_PED_PATH_PREFER_TO_AVOID_WATER, new InputArgument[2]
		{
			((Entity)ped).Handle,
			toggle ? 1 : 0
		});
	}

	public static void SetStealthMovement(this Ped ped, bool toggle)
	{
		Function.Call(Hash.SET_PED_STEALTH_MOVEMENT, new InputArgument[2]
		{
			toggle ? 1 : 0,
			"DEFAULT_ACTION"
		});
	}

	public static bool GetStealthMovement(this Ped ped)
	{
		return Function.Call<bool>(Hash.GET_PED_STEALTH_MOVEMENT, new InputArgument[1] { ((Entity)ped).Handle });
	}

	public static void SetComponentVariation(this Ped ped, ComponentId id, int drawableId, int textureId, int paletteId)
	{
		Function.Call(Hash.SET_PED_COMPONENT_VARIATION, new InputArgument[5]
		{
			((Entity)ped).Handle,
			(int)id,
			drawableId,
			textureId,
			paletteId
		});
	}

	public static int GetDrawableVariation(this Ped ped, ComponentId id)
	{
		return Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, new InputArgument[2]
		{
			((Entity)ped).Handle,
			(int)id
		});
	}

	public static int GetNumberOfDrawableVariations(this Ped ped, ComponentId id)
	{
		return Function.Call<int>(Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, new InputArgument[2]
		{
			((Entity)ped).Handle,
			(int)id
		});
	}

	public static bool IsSubttaskActive(this Ped ped, Subtask task)
	{
		return Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, new InputArgument[2]
		{
			ped,
			(int)task
		});
	}

	public static bool IsDriving(this Ped ped)
	{
		return ped.IsSubttaskActive(Subtask.DrivingWandering) || ped.IsSubttaskActive(Subtask.DrivingGoingToDestinationOrEscorting);
	}

	public static void SetPathCanUseLadders(this Ped ped, bool toggle)
	{
		Function.Call(Hash.SET_PED_PATH_CAN_USE_LADDERS, new InputArgument[2]
		{
			((Entity)ped).Handle,
			toggle ? 1 : 0
		});
	}

	public static void SetPathCanClimb(this Ped ped, bool toggle)
	{
		Function.Call(Hash.SET_PED_PATH_CAN_USE_CLIMBOVERS, new InputArgument[2]
		{
			((Entity)ped).Handle,
			toggle ? 1 : 0
		});
	}

	public static void SetMovementAnimSet(this Ped ped, string animation)
	{
		if (!((Entity)(object)ped == (Entity)null))
		{
			while (!Function.Call<bool>(Hash.HAS_ANIM_SET_LOADED, new InputArgument[1] { animation }))
			{
				Function.Call(Hash.REQUEST_ANIM_SET, new InputArgument[1] { animation });
				Script.Yield();
			}
			Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, new InputArgument[3]
			{
				((Entity)ped).Handle,
				animation,
				1048576000
			});
		}
	}

	public static void RemoveElegantly(this Ped ped)
	{
		Function.Call(Hash.REMOVE_PED_ELEGANTLY, new InputArgument[1] { ((Entity)ped).Handle });
	}

	public static void SetRagdollOnCollision(this Ped ped, bool toggle)
	{
		Function.Call(Hash.SET_PED_RAGDOLL_ON_COLLISION, new InputArgument[2]
		{
			((Entity)ped).Handle,
			toggle
		});
	}

	public static void SetAlertness(this Ped ped, Alertness alertness)
	{
		Function.Call(Hash.SET_PED_ALERTNESS, new InputArgument[2]
		{
			((Entity)ped).Handle,
			(int)alertness
		});
	}

	public static void SetCombatAblility(this Ped ped, CombatAbility ability)
	{
		Function.Call(Hash.SET_PED_COMBAT_ABILITY, new InputArgument[2]
		{
			((Entity)ped).Handle,
			(int)ability
		});
	}

	public static void SetCanEvasiveDive(this Ped ped, bool toggle)
	{
		Function.Call(Hash.SET_PED_CAN_EVASIVE_DIVE, new InputArgument[2]
		{
			((Entity)ped).Handle,
			toggle ? 1 : 0
		});
	}

	public static void StopAmbientSpeechThisFrame(this Ped ped)
	{
		if (ped.IsAmbientSpeechPlaying())
		{
			Function.Call(Hash.STOP_CURRENT_PLAYING_AMBIENT_SPEECH, new InputArgument[1] { ((Entity)ped).Handle });
		}
	}

	public static bool IsAmbientSpeechPlaying(this Ped ped)
	{
		return Function.Call<bool>(Hash.IS_AMBIENT_SPEECH_PLAYING, new InputArgument[1] { ((Entity)ped).Handle });
	}

	public static void DisablePainAudio(this Ped ped, bool toggle)
	{
		Function.Call(Hash.DISABLE_PED_PAIN_AUDIO, new InputArgument[2]
		{
			((Entity)ped).Handle,
			toggle ? 1 : 0
		});
	}

	public static void StopSpeaking(this Ped ped, bool shaking)
	{
		Function.Call(Hash.STOP_PED_SPEAKING, new InputArgument[2]
		{
			((Entity)ped).Handle,
			shaking ? 1 : 0
		});
	}

	public static void SetCanPlayAmbientAnims(this Ped ped, bool toggle)
	{
		Function.Call(Hash.SET_PED_CAN_PLAY_AMBIENT_ANIMS, new InputArgument[2]
		{
			((Entity)ped).Handle,
			toggle ? 1 : 0
		});
	}

	public static void SetCombatAttributes(this Ped ped, CombatAttributes attribute, bool enabled)
	{
		Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, new InputArgument[3]
		{
			((Entity)ped).Handle,
			(int)attribute,
			enabled
		});
	}

	public static void SetPathAvoidFires(this Ped ped, bool toggle)
	{
		Function.Call(Hash.SET_PED_PATH_AVOID_FIRE, new InputArgument[2]
		{
			((Entity)ped).Handle,
			toggle ? 1 : 0
		});
	}

	public static void ApplyDamagePack(this Ped ped, float damage, float multiplier, DamagePack damagePack)
	{
		Function.Call(Hash.APPLY_PED_DAMAGE_PACK, new InputArgument[4]
		{
			((Entity)ped).Handle,
			damagePack.ToString(),
			damage,
			multiplier
		});
	}

	public static void SetCanAttackFriendlies(this Ped ped, FirendlyFireType type)
	{
		switch (type)
		{
		case FirendlyFireType.CanAttack:
			Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, new InputArgument[3]
			{
				((Entity)ped).Handle,
				true,
				false
			});
			break;
		case FirendlyFireType.CantAttack:
			Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, new InputArgument[3]
			{
				((Entity)ped).Handle,
				false,
				false
			});
			break;
		}
	}

	public static void PlayAmbientSpeech(this Ped ped, string speechName, SpeechModifier modifier = SpeechModifier.Standard)
	{
		if (modifier >= SpeechModifier.Standard && (int)modifier < SpeechModifierNames.Length)
		{
			Function.Call(Hash.PLAY_PED_AMBIENT_SPEECH_NATIVE, new InputArgument[3]
			{
				((Entity)ped).Handle,
				speechName,
				SpeechModifierNames[(int)modifier]
			});
			return;
		}
		throw new ArgumentOutOfRangeException("modifier");
	}

	public static void Recruit(this Ped ped, Ped leader, bool canBeTargeted, bool invincible, int accuracy)
	{
		if ((Entity)(object)leader == (Entity)null)
		{
			return;
		}
		ped.LeaveGroup();
		ped.SetRagdollOnCollision(toggle: false);
		ped.Task.ClearAll();
		PedGroup currentPedGroup = leader.PedGroup;
		currentPedGroup.SeparationRange = 2.1474836E+09f;
		if (!currentPedGroup.Contains(leader))
		{
			currentPedGroup.Add(leader, true);
		}
		if (!currentPedGroup.Contains(ped))
		{
			currentPedGroup.Add(ped, false);
		}
		ped.CanBeTargetted = canBeTargeted;
		ped.Accuracy = accuracy;
		((Entity)ped).IsInvincible = invincible;
		((Entity)ped).IsPersistent = true;
		ped.RelationshipGroup = leader.RelationshipGroup;
		ped.NeverLeavesGroup = true;
		Blip currentBlip = ((Entity)ped).AttachedBlip;
		if (currentBlip != null)
		{
			currentBlip.Delete();
		}
		Blip val = ((Entity)ped).AddBlip();
		val.Color = (BlipColor)3;
		val.Scale = 0.7f;
		val.Name = "Friend";
		EntityEventWrapper wrapper = new EntityEventWrapper((Entity)(object)ped);
		wrapper.Died += (EntityEventWrapper sender, Entity entity) =>
		{
			Blip currentBlip2 = entity.AttachedBlip;
			if (currentBlip2 != null)
			{
				currentBlip2.Delete();
			}
			wrapper.Dispose();
		};
		ped.PlayAmbientSpeech("GENERIC_HI");
	}

	public static void Recruit(this Ped ped, Ped leader, bool canBeTargetted)
	{
		ped.Recruit(leader, canBeTargetted, invincible: false, 100);
	}

	public static void Recruit(this Ped ped, Ped leader)
	{
		ped.Recruit(leader, canBeTargetted: true);
	}

	public static void SetCombatRange(this Ped ped, CombatRange range)
	{
		Function.Call(Hash.SET_PED_COMBAT_RANGE, new InputArgument[2]
		{
			((Entity)ped).Handle,
			(int)range
		});
	}

	public static void SetCombatMovement(this Ped ped, CombatMovement movement)
	{
		Function.Call(Hash.SET_PED_COMBAT_MOVEMENT, new InputArgument[2]
		{
			((Entity)ped).Handle,
			(int)movement
		});
	}

	public static void ClearFleeAttributes(this Ped ped)
	{
		Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, new InputArgument[3]
		{
			((Entity)ped).Handle,
			0,
			0
		});
	}

	public static bool IsUsingAnyScenario(this Ped ped)
	{
		return Function.Call<bool>(Hash.IS_PED_USING_ANY_SCENARIO, new InputArgument[1] { ((Entity)ped).Handle });
	}

	public static bool CanHearPlayer(this Ped ped, Player player)
	{
		return Function.Call<bool>(Hash.CAN_PED_HEAR_PLAYER, new InputArgument[2]
		{
			player.Handle,
			((Entity)ped).Handle
		});
	}

	public static void SetHearingRange(this Ped ped, float hearingRange)
	{
		Function.Call(Hash.SET_PED_HEARING_RANGE, new InputArgument[2]
		{
			((Entity)ped).Handle,
			hearingRange
		});
	}

	public static bool IsCurrentWeaponSileced(this Ped ped)
	{
		return Function.Call<bool>(Hash.IS_PED_CURRENT_WEAPON_SILENCED, new InputArgument[1] { ((Entity)ped).Handle });
	}

	public static void Jump(this Ped ped)
	{
		Function.Call(Hash.TASK_JUMP, new InputArgument[4]
		{
			((Entity)ped).Handle,
			true,
			0,
			0
		});
	}

	public static void SetToRagdoll(this Ped ped, int time)
	{
		Function.Call(Hash.SET_PED_TO_RAGDOLL, new InputArgument[7]
		{
			((Entity)ped).Handle,
			time,
			0,
			0,
			0,
			0,
			0
		});
	}
}
