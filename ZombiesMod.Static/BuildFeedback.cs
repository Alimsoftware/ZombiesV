using System;
using GTA;

namespace ZombiesMod.Static;

/// <summary>
/// Resultado da validação de uma colocação. Serve ao mesmo tempo de regra (o que
/// conta como válido) e de fonte de feedback visual (a aparência do ghost).
/// </summary>
public enum PlacementStatus
{
	/// <summary>Assenta livremente na superfície sob a mira.</summary>
	Valid,

	/// <summary>Encaixou numa peça vizinha — a peça passa a fazer parte da estrutura.</summary>
	Snapped,

	/// <summary>Espaço ocupado: não pode ser colocado aqui.</summary>
	Blocked
}

/// <summary>
/// Feedback visual e regras de colocação do modo construção.
///
/// Não desenha nada por cima dos objectos, de propósito. As duas tentativas
/// anteriores falharam: a gaiola wireframe (<c>DRAW_LINE</c>) saía preta, fina e era
/// uma caixa delimitadora — nunca a silhueta; e a pegada (<c>DRAW_MARKER</c>)
/// aparecia como um círculo colorido em cima da peça. Ambas eram formas desenhadas,
/// não o objecto.
///
/// Aqui o feedback usa a <b>transparência do próprio objecto</b>, que é fiável e não
/// acrescenta geometria nenhuma:
/// <list type="bullet">
///   <item><description><b>Valid</b> — opacidade normal de ghost: a peça cabe.</description></item>
///   <item><description><b>Snapped</b> — mais sólida: agarrou numa vizinha.</description></item>
///   <item><description><b>Blocked</b> — pisca: não entra ali.</description></item>
/// </list>
/// </summary>
public static class BuildFeedback
{
	// --- ghost de colocação -------------------------------------------------------

	private const int GhostOpacity = 150;
	private const int SnappedOpacity = 210;
	private const int BlockedOpacityLow = 30;
	private const int BlockedOpacityHigh = 160;
	private const float BlockedBlinkHz = 3f;

	/// <summary>Opacidade normal do ghost (usada enquanto o modelo não tem estado).</summary>
	public const int DefaultGhostOpacity = GhostOpacity;

	/// <summary>Aplica ao ghost a aparência do estado de colocação corrente.</summary>
	public static void ApplyGhost(Prop ghost, PlacementStatus status)
	{
		if (ghost == null || !ghost.Exists())
		{
			return;
		}

		switch (status)
		{
			case PlacementStatus.Blocked:
				ghost.Opacity = Pulse(BlockedOpacityLow, BlockedOpacityHigh, BlockedBlinkHz);
				break;

			case PlacementStatus.Snapped:
				ghost.Opacity = SnappedOpacity;
				break;

			default:
				ghost.Opacity = GhostOpacity;
				break;
		}
	}

	// --- destaque no modo edição --------------------------------------------------

	private const int FocusOpacityLow = 150;
	private const int FocusOpacityHigh = 214;
	private const float FocusPulseHz = 1.2f;

	/// <summary>Pulso lento que destaca a peça em foco no modo edição.</summary>
	public static void ApplyFocus(Entity entity)
	{
		if (entity == null || !entity.Exists())
		{
			return;
		}

		entity.Opacity = Pulse(FocusOpacityLow, FocusOpacityHigh, FocusPulseHz);
	}

	// --- utilitário -----------------------------------------------------------------

	/// <summary>Onda contínua entre <paramref name="low"/> e <paramref name="high"/>.</summary>
	private static int Pulse(int low, int high, float hertz)
	{
		double phase = Game.GameTime / 1000.0 * hertz * Math.PI * 2.0;
		double wave = (Math.Sin(phase) + 1.0) * 0.5;
		return low + (int)((high - low) * wave);
	}

	// --- som ----------------------------------------------------------------------

	private const string SoundSet = "HUD_FRONTEND_DEFAULT_SOUNDSET";

	/// <summary>Som de confirmação ao colocar uma peça.</summary>
	public static void PlayPlaceSound()
	{
		SafeExecutor.Run(
			"BuildFeedback.PlaceSound",
			() => Audio.PlaySoundFrontendAndForget("SELECT", SoundSet, true));
	}

	/// <summary>Som de recusa quando a colocação não é permitida.</summary>
	public static void PlayRefuseSound()
	{
		SafeExecutor.Run(
			"BuildFeedback.RefuseSound",
			() => Audio.PlaySoundFrontendAndForget("ERROR", SoundSet, true));
	}
}
