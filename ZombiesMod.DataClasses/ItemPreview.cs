using System;
using System.Collections.Generic;
using GTA;
using GTA.Input;
using GTA.Math;
using GTA.Native;
using ZombiesMod.Extensions;
using ZombiesMod.Scripts;
using ZombiesMod.Static;
using UI = GTA.UI;

namespace ZombiesMod.DataClasses;

/// <summary>Como terminou um ciclo de posicionamento.</summary>
public enum PreviewResult
{
	/// <summary>Ainda não terminou.</summary>
	None,

	/// <summary>O jogador confirmou e a prop ficou no mundo.</summary>
	Placed,

	/// <summary>Cancelado, modelo falhou ou o script abortou.</summary>
	Cancelled
}

/// <summary>
/// Colocação de objectos com pré-visualização.
///
/// Regista-se no <see cref="ScriptEventHandler"/> em vez de correr no tick de um
/// <see cref="GTA.Script"/> próprio — por isso é obrigatório desregistar em TODOS os
/// caminhos de saída (colocar, cancelar, falhar, abortar). Falhar um deles deixa o
/// handler activo para sempre, a criar uma prop nova por frame.
///
/// O modelo carrega de forma assíncrona, ao longo de frames distintos. Nada de
/// <c>Script.Yield()</c> aqui dentro: bloquearia o ScriptEventHandler inteiro até ao
/// timeout de 8 s, e com ele todos os outros handlers registados.
/// </summary>
public class ItemPreview
{
	// --- parâmetros de colocação -------------------------------------------------

	private const float RaycastRange = 15f;
	private const float MinPlacementDistance = 1.5f;
	private const float CollisionCheckRadius = 0.6f;
	private const float WallSearchRadius = 6f;

	/// <summary>
	/// Até quanto a peça pode estar rodada face à vizinha e ainda assim encaixar.
	/// Acima disto assume-se que o jogador está a fazer um canto, não a continuar a
	/// fila, e não se cola.
	/// </summary>
	private const float MaxSnapAngleDegrees = 45f;

	/// <summary>
	/// Distância máxima entre o alvo e a posição encaixada. Sem este limite o encaixe
	/// disparava sempre que havia uma parede até <see cref="WallSearchRadius"/> de
	/// distância — a peça «saltava» para uma parede longe, mesmo apontando para o meio.
	/// Assim só cola quando o jogador aponta perto da junta.
	/// </summary>
	private const float MaxSnapDistance = 1.75f;
	private const float RotationStepDegrees = 45f;
	private const float FineRotationStepDegrees = 15f;
	private const float FastMultiplier = 3f;

	/// <summary>
	/// Lado da célula da grelha, em metros. 0,25 m é o passo típico de construção:
	/// fino que chegue para encostar peças sem elas «saltarem» de sítio.
	/// </summary>
	private const float GridSize = 0.25f;

	/// <summary>
	/// Regra «não flutuar»: quanto acima e abaixo da base do modelo se procura apoio.
	/// Sonda curta de propósito — um vão maior deixava passar peças a pairar no ar.
	/// </summary>
	private const float SupportProbeUp = 0.25f;
	private const float SupportProbeDown = 0.4f;

	private static readonly TimeSpan ModelLoadTimeout = TimeSpan.FromSeconds(8);

	// --- controlos ---------------------------------------------------------------
	//
	// Todos são lidos com IsDisabledControlPressed, nunca com IsControlPressed: o modo
	// de colocação desactiva estes controlos logo abaixo, e IS_CONTROL_PRESSED devolve
	// false para um controlo desactivado — que é exactamente o que acontecia no original.
	// IS_DISABLED_CONTROL_PRESSED devolve a intenção do jogador mesmo com o controlo
	// desactivado, que é para isso que ele serve.
	private const ControlAction CancelKey = ControlAction.Aim;
	private const ControlAction PlaceKey = ControlAction.Attack;
	private const ControlAction RotateCwKey = ControlAction.WeaponWheelNext;
	private const ControlAction RotateCcwKey = ControlAction.WeaponWheelPrev;
	private const ControlAction FastKey = ControlAction.Sprint;
	private const ControlAction UpKey = ControlAction.PhoneUp;
	private const ControlAction DownKey = ControlAction.PhoneDown;

	/// <summary>Liga/desliga o snap em grelha. Toque, não «enquanto premido».</summary>
	private const ControlAction GridToggleKey = ControlAction.Context;

	/// <summary>Alinha a peça ao declive do terreno (desligado = sempre direita).</summary>
	private const ControlAction TerrainToggleKey = ControlAction.LookBehind;

	/// <summary>Cola a peça à borda de uma vizinha.</summary>
	private const ControlAction ObjectSnapToggleKey = ControlAction.Cover;

	/// <summary>Regras de construção (não flutuar).</summary>
	private const ControlAction RulesToggleKey = ControlAction.Reload;

	/// <summary>Feedback de construção (visual + som).</summary>
	private const ControlAction FeedbackToggleKey = ControlAction.Duck;

	/// <summary>
	/// Props já colocadas que podem receber o encaixe de bordas. Só se entra aqui para
	/// itens marcados como engatáveis, para que mesas e fogueiras não coloquem paredes.
	/// </summary>
	private static readonly List<Prop> SnappableProps = new List<Prop>();

	// --- estado ------------------------------------------------------------------

	private Vector3 _initialOffset;
	private Vector3 _currentOffset;
	private Prop _currentPreview;
	private Prop _resultProp;
	private string _currentPropHash;
	private bool _preview;
	private bool _isDoor;
	private bool _isSnappable;
	private bool _registered;

	// Fica ligada entre colocações: é uma preferência do jogador, não um ajuste
	// pontual de uma peça. Começa ligada porque é isso que o modo de construção quer.
	private bool _gridSnapEnabled = true;

	// Opções de construção (todas alternáveis por tecla durante a colocação).
	// O alinhamento ao terreno começa DESLIGADO: peças direitas por omissão.
	private bool _terrainAlignEnabled;
	private bool _objectSnapEnabled = true;
	private bool _buildRulesEnabled = true;
	private bool _buildFeedbackEnabled = true;

	private readonly EventHandler _tickHandler;
	private readonly EventHandler _abortHandler;

	// Carregamento do modelo, repartido por frames.
	private Model _model;
	private uint _modelHash;
	private bool _loadingModel;
	private DateTime _modelDeadline;

	private Scaleform _instructionalScaleform;
	private float _manualYawDegrees;

	/// <summary>Meia-extensão da própria peça ao longo do eixo da fila (ver GetRowAxis).</summary>
	private float _ownHalfLength;

	/// <summary>Deslocamento do fundo do modelo face à origem (ver GetModelMetrics).</summary>
	private float _ownBottomOffset;

	// --- transições de estado ----------------------------------------------------
	// Só se escreve quando muda, nunca por frame: senão o log enche-se com uma linha
	// por cada movimento da câmara.

	private bool _loggedSnap;
	private bool _loggedBlocked;

	public bool PreviewComplete { get; private set; }

	/// <summary>Como terminou o último ciclo. <see cref="PreviewResult.None"/> enquanto corre.</summary>
	public PreviewResult Result { get; private set; } = PreviewResult.None;

	/// <summary>Resultado de <see cref="TrySnapToNearbyProp"/>; fecha a passagem por SafeExecutor.</summary>
	private readonly struct SnapResult
	{
		public readonly bool Snapped;
		public readonly Vector3 Position;
		public readonly Vector3 Rotation;

		public SnapResult(bool snapped, Vector3 position, Vector3 rotation)
		{
			Snapped = snapped;
			Position = position;
			Rotation = rotation;
		}
	}

	public ItemPreview()
	{
		_tickHandler = OnTick;
		_abortHandler = delegate
		{
			Abort();
		};

		ScriptEventHandler.Instance.RegisterScript(_tickHandler);
		_registered = true;
		((Script)ScriptEventHandler.Instance).Aborted += _abortHandler;
	}

	public void OnTick(object sender, EventArgs eventArgs)
	{
		if (_preview)
		{
			CreateItemPreview();
		}
	}

	public Prop GetResult()
	{
		return _resultProp;
	}

	public void StartPreview(string propHash, Vector3 offset, bool isDoor, bool isSnappable = false)
	{
		if (_preview)
		{
			ModLogger.Warn(
				"ItemPreview.StartPreview",
				string.Format("Pedido ignorado: já existe um preview activo ('{0}') para '{1}'.", _currentPropHash, propHash));
			return;
		}

		_preview = true;
		_currentPropHash = propHash;
		_isDoor = isDoor;
		_isSnappable = isSnappable;

		// O offset do item serve de base e de reset: as setas acumulam por cima, e um
		// novo preview volta ao valor do catálogo em vez de herdar o anterior.
		_initialOffset = offset;
		_currentOffset = offset;
		_manualYawDegrees = 0f;
		_ownHalfLength = 0f;
		_loggedSnap = false;
		_loggedBlocked = false;
		_resultProp = null;
		Result = PreviewResult.None;
		PreviewComplete = false;

		if (!_registered)
		{
			ScriptEventHandler.Instance.RegisterScript(_tickHandler);
			_registered = true;
		}

		ModLogger.Info("ItemPreview", string.Format("A iniciar colocação de '{0}'.", propHash));
	}

	public void Abort()
	{
		if (_currentPreview != null)
		{
			_currentPreview.Delete();
			_currentPreview = null;
		}

		_resultProp = null;

		// Sem isto, quem estiver em «while (!preview.PreviewComplete) Script.Yield();»
		// fica à espera para sempre de um resultado que nunca chega.
		Finish(PreviewResult.Cancelled);
	}

	// ------------------------------------------------------------------------------
	// Ciclo de vida
	// ------------------------------------------------------------------------------

	private void CreateItemPreview()
	{
		if (_currentPreview == null)
		{
			if (!_loadingModel)
			{
				BeginModelLoad();
				return;
			}

			// O modelo ainda não chegou, mas o jogador tem de poder sair — sem isto,
			// um modelo lento deixa-o 8 s sem preview e sem cancelar. (No original o
			// carregamento era síncrono, por isso este estado não existia.)
			Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.Aim);

			if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, CancelKey))
			{
				CancelPreview();
				return;
			}

			if (!TryFinishModelLoad())
			{
				return;
			}
		}

		UpdatePreview();
	}

	private void BeginModelLoad()
	{
		PreviewComplete = false;
		_currentOffset = _initialOffset;
		_manualYawDegrees = 0f;

		// Suporte a hash manual: se o valor do JSON parecer um hash
		// ("0x..." hexadecimal ou só dígitos decimais), usa-se directamente.
		// Caso contrário, calcula-se o hash a partir do nome do modelo.
		if (TryParsePropHash(_currentPropHash, out uint directHash))
		{
			_modelHash = directHash;
		}
		else
		{
			_modelHash = Function.Call<uint>(Hash.GET_HASH_KEY, _currentPropHash);
		}

		_model = new Model((int)_modelHash);

		if (!_model.IsValid)
		{
			FailLoad(string.Format("hash inválido para '{0}'.", _currentPropHash));
			return;
		}

		_model.Request();
		_loadingModel = true;
		_modelDeadline = DateTime.UtcNow + ModelLoadTimeout;
	}

	/// <summary>
	/// Tenta interpretar o valor do campo "model" como um hash directo.
	/// Aceita "0x..." (hex) ou uma cadeia de dígitos (decimal). Devolve
	/// false para nomes de modelo normais, que têm de passar por GET_HASH_KEY.
	/// </summary>
	private static bool TryParsePropHash(string value, out uint hash)
	{
		hash = 0u;
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}

		// Hexadecimal com prefixo "0x" / "0X"
		if (value.Length > 2 && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
			uint.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out hash))
		{
			return true;
		}

		// Decimal (só dígitos)
		if (uint.TryParse(value, out hash))
		{
			return true;
		}

		return false;
	}

	private bool TryFinishModelLoad()
	{
		if (!_model.IsLoaded)
		{
			if (DateTime.UtcNow < _modelDeadline)
			{
				return false;
			}

			FailLoad(string.Format("o modelo '{0}' não carregou em {1} s.", _currentPropHash, (int)ModelLoadTimeout.TotalSeconds));
			return false;
		}

		Prop prop = Prop.Create(_model, Vector3.Zero, Vector3.Zero, dynamic: false, placeOnGround: false);

		if (prop == null || !prop.Exists())
		{
			FailLoad(string.Format("a engine devolveu uma prop inválida para '{0}'.", _currentPropHash));
			return false;
		}

		prop.IsCollisionEnabled = false;
		prop.Opacity = BuildFeedback.DefaultGhostOpacity;
		_currentPreview = prop;
		_resultProp = null;

		Database.PlayerPed.Weapons.Select(WeaponHash.Unarmed, true);

		// Comprimento (para o encaixe) e fundo do modelo (para a regra de apoio) saem
		// da mesma chamada nativa de GET_MODEL_DIMENSIONS.
		_ownHalfLength = 0f;
		_ownBottomOffset = 0f;
		SafeExecutor.Run("ItemPreview.GetModelMetrics", () =>
		{
			GetModelMetrics(_modelHash, out float halfLength, out float bottomOffset);
			_ownHalfLength = _isSnappable ? halfLength : 0f;
			_ownBottomOffset = bottomOffset;
		});

		_model.MarkAsNoLongerNeeded();
		_model = null;
		_loadingModel = false;

		return true;
	}

	private void FailLoad(string reason)
	{
		ModLogger.Warn("ItemPreview.FailLoad", reason);
		UI.Notification.Show(Lang.T("notify.place_failed", _currentPropHash), false);

		if (_currentPreview != null)
		{
			_currentPreview.Delete();
			_currentPreview = null;
		}

		_resultProp = null;
		Finish(PreviewResult.Cancelled);
	}

	// ------------------------------------------------------------------------------
	// Actualização por frame
	// ------------------------------------------------------------------------------

	private void UpdatePreview()
	{
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.Aim);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.Attack);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)257);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)44);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)27);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.PhoneUp);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.PhoneDown);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, GridToggleKey);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, TerrainToggleKey);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ObjectSnapToggleKey);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, RulesToggleKey);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, FeedbackToggleKey);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.Sprint);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.WeaponWheelNext);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, ControlAction.WeaponWheelPrev);
		GameExtended.DisableWeaponWheel();

		// JustPressed e não Pressed: com Pressed, um clique longo liquidava o preview
		// antes de o jogador conseguir posicionar nada.
		if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, CancelKey))
		{
			CancelPreview();
			return;
		}

		Vector3 startPosition = GameplayCamera.Position;
		Vector3 direction = GameplayCamera.Direction;

		// Map|Objects|Foliage em vez de «tudo»: com todos os flags o raycast grudava na
		// primeira coisa que passasse — um ped ou um carro — e a pré-visualização
		// seguia a pessoa pelo mapa.
		RaycastResult hit = World.Raycast(
			startPosition,
			startPosition + direction * RaycastRange,
			IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Foliage,
			Database.PlayerPed);

		Vector3 hitCoords = hit.HitPosition;
		bool noHit = hitCoords == Vector3.Zero;
		bool tooClose = !noHit && hitCoords.DistanceTo(Database.PlayerPosition) <= MinPlacementDistance;

		if (noHit || tooClose)
		{
			_currentPreview.IsVisible = false;
			DisplayHelpText(noHit ? Lang.T("help.aim_ground") : Lang.T("help.too_close"));
			return;
		}

		_currentPreview.IsVisible = true;
		SafeExecutor.Run("ItemPreview.Scaleform", EnsureScaleform);

		ApplyInput();

		Vector3 targetPosition = hitCoords + _currentOffset;

		// Orientação ao terreno é opcional: por omissão a peça fica direita, sem se
		// inclinar com o declive. Só com a opção ligada é que acompanha a superfície.
		Vector3 rotation = _terrainAlignEnabled
			? CalculateSurfaceAlignedRotation(hit.SurfaceNormal, _manualYawDegrees)
			: new Vector3(0f, 0f, _manualYawDegrees);

		bool snapped = false;
		if (_isSnappable && _objectSnapEnabled)
		{
			SnapResult snap = SafeExecutor.Run(
				"ItemPreview.Snap",
				() => TrySnapToNearbyProp(targetPosition, rotation),
				default(SnapResult));

			if (snap.Snapped)
			{
				snapped = true;
				targetPosition = snap.Position;
				rotation = snap.Rotation;
			}
		}

		// A grelha só entra se o encaixe a uma peça não tiver pegado: encaixar é mais
		// preciso e manda. Aplicada a X e Y apenas (ver SnapToGrid).
		if (!snapped && _gridSnapEnabled)
		{
			targetPosition = SnapToGrid(targetPosition);
		}

		bool blocked = SafeExecutor.Run("ItemPreview.IsBlocked", () => IsPositionBlocked(targetPosition), false);

		// Regra «não flutuar»: sem apoio por baixo, não entra. Encaixar a uma peça
		// vizinha já implica apoio, mas a regra só olha para o caso não-encaixado para
		// não recusar uma peça encostada que até tem o pé no ar.
		bool unsupported = _buildRulesEnabled
			&& !SafeExecutor.Run("ItemPreview.HasSupport", () => HasSupport(targetPosition), true);

		bool invalid = blocked || unsupported;

		// Regras de colocação: encaixar ganha ao bloqueio (a peça fica a fazer parte
		// da estrutura), logo só fica «bloqueado» quando não encaixou em nada.
		PlacementStatus status = snapped
			? PlacementStatus.Snapped
			: (invalid ? PlacementStatus.Blocked : PlacementStatus.Valid);

		if (status == PlacementStatus.Blocked)
		{
			if (!_loggedBlocked)
			{
				_loggedBlocked = true;
				ModLogger.Warn(
					"ItemPreview",
					string.Format(
						"Colocação recusada em {0}: {1}.",
						Describe(targetPosition),
						unsupported ? "sem apoio" : "espaço ocupado"));
			}

			// Mantém-se visível, a piscar, para o jogador ver onde a peça iria parar —
			// esconder a ghost dá a ideia de que o modo avariou.
			_currentPreview.Position = targetPosition;
			_currentPreview.Rotation = rotation;
			ApplyGhostAppearance(status);
			DisplayHelpText(Lang.T(unsupported ? "help.needs_support" : "help.space_occupied"));
			ApplyPlaceOrRefuse();
			return;
		}

		_loggedBlocked = false;

		if (snapped && !_loggedSnap)
		{
			_loggedSnap = true;
			ModLogger.Info("ItemPreview", string.Format("Encaixe activo em {0}.", Describe(targetPosition)));
		}
		else if (!snapped)
		{
			_loggedSnap = false;
		}

		// Um só DisplayHelpText por frame: o caminho bloqueado já saiu acima, por isso
		// aqui só pode ser o texto de controlos.
		DisplayHelpText(BuildControlsHelp());

		_currentPreview.Position = targetPosition;
		_currentPreview.Rotation = rotation;

		// Depois da posição e da rotação estarem aplicadas: a aparência do ghost (pisca
		// quando bloqueado) só faz sentido com o transform já correto.
		ApplyGhostAppearance(status);

		if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, PlaceKey))
		{
			PlaceItem(snapped);
		}
	}

	/// <summary>
	/// Quando o alvo está bloqueado, confirmação não coloca — só cancela. Assim o
	/// jogador não «fura» objetos acidentalmente premindo o botão à espera de feedback.
	/// </summary>
	private void ApplyPlaceOrRefuse()
	{
		if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, PlaceKey))
		{
			ModLogger.Warn("ItemPreview", "Confirmação ignorada: o alvo está bloqueado.");
			UI.Notification.Show(Lang.T("notify.cannot_place"), false);

			if (_buildFeedbackEnabled)
			{
				SafeExecutor.Run("ItemPreview.RefuseSound", BuildFeedback.PlayRefuseSound);
			}
		}
	}

	private void ApplyInput()
	{
		// Opções de construção: alternam por toque e ficam no sítio, ao contrário dos
		// outros controlos, que são «enquanto premido».
		ToggleIfPressed(GridToggleKey, ref _gridSnapEnabled, "Snap em grelha ligado.", "Snap em grelha desligado.");
		ToggleIfPressed(TerrainToggleKey, ref _terrainAlignEnabled, "Orientação ao terreno ligada.", "Orientação ao terreno desligada.");
		ToggleIfPressed(ObjectSnapToggleKey, ref _objectSnapEnabled, "Snap entre objetos ligado.", "Snap entre objetos desligado.");
		ToggleIfPressed(RulesToggleKey, ref _buildRulesEnabled, "Regras de construção ligadas.", "Regras de construção desligadas.");
		ToggleIfPressed(FeedbackToggleKey, ref _buildFeedbackEnabled, "Feedback de construção ligado.", "Feedback de construção desligado.");

		bool fine = Controls.IsDisabledControlPressed(ControlType.PlayerControl, FastKey);
		float multiplier = fine ? FastMultiplier : 1f;

		// Rotação por passos: cada toque roda um incremento fixo (45°, ou 15° com o
		// modificador em baixo), para as peças ficarem a direito. Já não há rotação
		// contínua — era o que deixava tudo ligeiramente torto.
		float step = fine ? FineRotationStepDegrees : RotationStepDegrees;

		if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, RotateCwKey))
		{
			_manualYawDegrees += step;
		}
		else if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, RotateCcwKey))
		{
			_manualYawDegrees -= step;
		}

		// Sem isto o yaw cresce sem limite e, depois de horas de jogo, a precisão do
		// float começa a tremer. O ângulo é módulo 360, logo é invisível.
		if (_manualYawDegrees > 360f)
		{
			_manualYawDegrees -= 360f;
		}
		else if (_manualYawDegrees < -360f)
		{
			_manualYawDegrees += 360f;
		}

		if (Controls.IsDisabledControlPressed(ControlType.PlayerControl, UpKey))
		{
			_currentOffset.Z += Game.LastFrameTime * multiplier;
		}
		else if (Controls.IsDisabledControlPressed(ControlType.PlayerControl, DownKey))
		{
			_currentOffset.Z -= Game.LastFrameTime * multiplier;
		}
	}

	/// <summary>
	/// Alterna uma opção de construção quando a tecla é tocada. Isolado porque são
	/// cinco opções com o mesmo padrão; o log regista sempre a transição.
	/// </summary>
	private static void ToggleIfPressed(ControlAction key, ref bool flag, string onMessage, string offMessage)
	{
		if (Controls.IsDisabledControlJustPressed(ControlType.PlayerControl, key))
		{
			flag = !flag;
			ModLogger.Info("ItemPreview", flag ? onMessage : offMessage);
		}
	}

	// ------------------------------------------------------------------------------
	// Geometria
	// ------------------------------------------------------------------------------

	/// <summary>
	/// Prende X e Y à grelha mundial. O Z fica intocado de propósito: vem do raycast,
	/// e arredondá-lo punha as peças a flutuar (ou enterradas) sempre que o chão não
	/// calhasse num múltiplo da grelha. Alinhar só o plano horizontal é o que faz duas
	/// paredes da mesma célula encostarem sem ficarem desencontradas.
	/// </summary>
	private static Vector3 SnapToGrid(Vector3 position)
	{
		return new Vector3(
			(float)Math.Round(position.X / GridSize) * GridSize,
			(float)Math.Round(position.Y / GridSize) * GridSize,
			position.Z);
	}

	/// <summary>
	/// Regra «não flutuar»: procura apoio mesmo por baixo da base da peça. Ignora a
	/// própria ghost, senão ela dava apoio a si mesma (está sempre no alvo).
	/// </summary>
	private bool HasSupport(Vector3 position)
	{
		float baseZ = position.Z + _ownBottomOffset;
		Vector3 start = new Vector3(position.X, position.Y, baseZ + SupportProbeUp);
		Vector3 end = new Vector3(position.X, position.Y, baseZ - SupportProbeDown);

		RaycastResult support = World.Raycast(
			start,
			end,
			IntersectFlags.Map | IntersectFlags.Objects,
			_currentPreview);

		return support.DidHit;
	}

	/// <summary>
	/// Inclina a prop para acompanhar a superfície (pitch/roll a partir da normal do
	/// raycast) e depois aplica o giro manual só em Z, para a inclinação nunca
	/// «contaminar» a rotação que o jogador está a pedir com o scroll.
	/// </summary>
	private static Vector3 CalculateSurfaceAlignedRotation(Vector3 surfaceNormal, float yawDegrees)
	{
		// Com a normal quase vertical o produto vectorial degenera, por isso nesse caso
		// fica na horizontal — que é o que se espera ao pôr algo no chão.
		Vector3 referenceUp = Math.Abs(surfaceNormal.Z) < 0.1f
			? Vector3.WorldUp
			: surfaceNormal;
		Vector3 right = Vector3.Cross(Vector3.WorldNorth, referenceUp);

		if (right.LengthSquared() < 1E-06f)
		{
			return new Vector3(0f, 0f, yawDegrees);
		}

		right.Normalize();
		Vector3 forward = Vector3.Cross(referenceUp, right);

		// clamp manual: não há MathHelper neste namespace.
		float pitch = (float)(Math.Asin(Clamp(forward.Z)) * (180.0 / Math.PI));
		float roll = (float)(Math.Asin(Clamp(right.Z)) * (180.0 / Math.PI));

		return new Vector3(pitch, roll, yawDegrees);
	}

	/// <summary>
	/// Procura a peça engatável mais próxima e cola a nova à borda dela, herdando a
	/// rotação — é isto que faz uma fila de paredes ficar direita em vez de em escada.
	///
	/// Entre várias candidatas fica a que deixa a peça mais perto de onde o jogador
	/// apontou. Devolve o alvo inalterado quando não há nada para onde colar.
	/// </summary>
	private SnapResult TrySnapToNearbyProp(Vector3 targetPosition, Vector3 currentRotation)
	{
		Prop[] nearbyProps = World.GetNearbyProps(targetPosition, WallSearchRadius);

		bool found = false;
		float bestDistance = float.MaxValue;
		Vector3 bestPosition = Vector3.Zero;
		Vector3 bestRotation = Vector3.Zero;

		foreach (Prop neighbor in nearbyProps)
		{
			if
			(
				neighbor == null
				|| !neighbor.Exists()
				|| !SnappableProps.Contains(neighbor)
				|| neighbor.Equals(_currentPreview)
			)
			{
				continue;
			}

			// Só se cola a uma peça virada «no mesmo sentido»: se o jogador a rodou
			// ~90°, está a fazer um canto, e encostar pela borda longa ficava errado.
			if (AngleDifference180(currentRotation.Z, neighbor.Rotation.Z) > MaxSnapAngleDegrees)
			{
				continue;
			}

			Vector3 axis;
			float neighborHalf;
			GetRowAxis(neighbor, out axis, out neighborHalf);

			Vector3 toTarget = targetPosition - neighbor.Position;
			toTarget.Z = 0f;
			float side = Vector3.Dot(toTarget, axis) < 0f ? -1f : 1f;

			float newHalf = Math.Max(0.25f, _ownHalfLength);
			Vector3 candidate = neighbor.Position + axis * side * (neighborHalf + newHalf);

			// O ajuste manual de altura (setas) sobrevive ao encaixe: sem isto o encaixe
			// repunha o Z da vizinha e as setas não faziam nada.
			candidate.Z = neighbor.Position.Z + (_currentOffset.Z - _initialOffset.Z);

			float distance = candidate.DistanceTo(targetPosition);

			// Longe do alvo = o jogador não está a apontar para esta junta.
			if (distance > MaxSnapDistance)
			{
				continue;
			}

			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestPosition = candidate;
				bestRotation = neighbor.Rotation;
				found = true;
			}
		}

		return found
			? new SnapResult(true, bestPosition, bestRotation)
			: new SnapResult(false, targetPosition, currentRotation);
	}

	/// <summary>
	/// Eixo ao longo do qual a peça «corre» (o comprimento) e a sua meia-extensão.
	/// Escolhe-se entre os eixos locais X (RightVector) e Y (ForwardVector) pelo que
	/// for maior no modelo — assim funciona para qualquer prop, sem depender de a
	/// convenção do modelo ser comprida em X ou em Y.
	///
	/// Achatado no plano horizontal: a fila assenta no chão, e a inclinação de uma
	/// peça (por estar numa rampa) não deve empurrar a vizinha para cima ou para baixo.
	/// </summary>
	private static void GetRowAxis(Prop prop, out Vector3 axis, out float halfLength)
	{
		OutputArgument min = new OutputArgument();
		OutputArgument max = new OutputArgument();
		Function.Call(Hash.GET_MODEL_DIMENSIONS, prop.Model.Hash, min, max);

		Vector3 minValue = min.GetResult<Vector3>();
		Vector3 maxValue = max.GetResult<Vector3>();
		float extX = Math.Abs(maxValue.X - minValue.X);
		float extY = Math.Abs(maxValue.Y - minValue.Y);

		Vector3 raw = extX >= extY ? prop.RightVector : prop.ForwardVector;
		halfLength = Math.Max(extX, extY) * 0.5f;

		raw.Z = 0f;
		if (raw.LengthSquared() < 1E-06f)
		{
			axis = new Vector3(1f, 0f, 0f);
			return;
		}

		raw.Normalize();
		axis = raw;
	}

	/// <summary>Diferença angular mais curta entre dois ângulos, entre 0° e 180°.</summary>
	private static float AngleDifference180(float a, float b)
	{
		float difference = Math.Abs(WrapDegrees(a - b));
		return difference > 180f ? 360f - difference : difference;
	}

	/// <summary>Normaliza um ângulo para [-180, 180].</summary>
	private static float WrapDegrees(float degrees)
	{
		degrees %= 360f;

		if (degrees > 180f)
		{
			degrees -= 360f;
		}
		else if (degrees < -180f)
		{
			degrees += 360f;
		}

		return degrees;
	}

	/// <summary>
	/// Métricas do modelo via GET_MODEL_DIMENSIONS: a meia-extensão ao longo do eixo
	/// mais comprido (o eixo da fila) e o deslocamento do fundo face à origem (para a
	/// regra de apoio). Precisa do modelo carregado.
	/// </summary>
	private static void GetModelMetrics(uint modelHash, out float halfLength, out float bottomOffset)
	{
		OutputArgument min = new OutputArgument();
		OutputArgument max = new OutputArgument();
		Function.Call(Hash.GET_MODEL_DIMENSIONS, modelHash, min, max);

		Vector3 minValue = min.GetResult<Vector3>();
		Vector3 maxValue = max.GetResult<Vector3>();
		float extX = Math.Abs(maxValue.X - minValue.X);
		float extY = Math.Abs(maxValue.Y - minValue.Y);

		halfLength = Math.Max(extX, extY) * 0.5f;
		bottomOffset = minValue.Z;
	}

	/// <summary>
	/// Verifica se o alvo já está ocupado por prop, veículo ou ped.
	///
	/// A pré-visualização fica de fora, e é isto que faz o sistema funcionar: ela é
	/// posicionada no alvo do frame anterior, por isso sem esta exclusão a verificação
	/// encontrava a si própria e respondia «bloqueado» quase sempre. O sintoma era um
	/// preview a piscar a ~30 Hz — visível um frame, invisível no seguinte — e ser
	/// impossível posicionar qualquer coisa.
	/// </summary>
	private bool IsPositionBlocked(Vector3 targetPosition)
	{
		Prop[] nearbyProps = World.GetNearbyProps(targetPosition, CollisionCheckRadius);

		foreach (Prop prop in nearbyProps)
		{
			// Comparação por handle: a prop foi criada por nós e continua no pool mesmo
			// com a colisão desligada e invisível.
			if (prop == null || !prop.Exists() || prop.Handle == _currentPreview.Handle)
			{
				continue;
			}

			if (prop.Position.DistanceTo(targetPosition) < CollisionCheckRadius)
			{
				return true;
			}
		}

		Vehicle[] nearbyVehicles = World.GetNearbyVehicles(targetPosition, CollisionCheckRadius);

		foreach (Vehicle vehicle in nearbyVehicles)
		{
			if (vehicle == null || !vehicle.Exists())
			{
				continue;
			}

			if (vehicle.Position.DistanceTo(targetPosition) < CollisionCheckRadius)
			{
				return true;
			}
		}

		Ped[] nearbyPeds = World.GetNearbyPeds(targetPosition, CollisionCheckRadius);

		foreach (Ped ped in nearbyPeds)
		{
			if (ped == null || !ped.Exists() || ped == Database.PlayerPed)
			{
				continue;
			}

			if (ped.Position.DistanceTo(targetPosition) < CollisionCheckRadius)
			{
				return true;
			}
		}

		return false;
	}

	// ------------------------------------------------------------------------------
	// Saída
	// ------------------------------------------------------------------------------

	/// <summary>
	/// Regista uma prop como engatável. É chamado ao colocar e também ao carregar o
	/// mapa, para que as peças de sessões anteriores continuem a encaixar.
	///
	/// Poda handles mortos primeiro: sem isso a lista acumulava entradas das peças
	/// antigas já destruídas a cada recarregamento do mapa.
	/// </summary>
	public static void RegisterSnappable(Prop prop)
	{
		if (prop == null || !prop.Exists())
		{
			return;
		}

		SnappableProps.RemoveAll(existing => ReferenceEquals(existing, null) || !existing.Exists());

		if (!SnappableProps.Contains(prop))
		{
			SnappableProps.Add(prop);
		}
	}

	private void PlaceItem(bool snapped)
	{
		if (_currentPreview == null)
		{
			return;
		}

		// Capturados antes do Finish, que limpa hash e estado.
		string itemName = _currentPropHash;
		Vector3 itemPosition = _currentPreview.Position;

		_currentPreview.ResetOpacity();
		_resultProp = _currentPreview;
		_resultProp.IsCollisionEnabled = true;
		_resultProp.IsPositionFrozen = !_isDoor;

		bool addToSnaps = _isSnappable;
		_currentPreview = null;

		Finish(PreviewResult.Placed);

		if (addToSnaps)
		{
			RegisterSnappable(_resultProp);
		}

		ModLogger.Info(
			"ItemPreview",
			string.Format("Colocada '{0}' em {1}{2}.", itemName, Describe(itemPosition), snapped ? " (encaixada)" : string.Empty));

		if (_buildFeedbackEnabled)
		{
			SafeExecutor.Run("ItemPreview.PlaceSound", BuildFeedback.PlayPlaceSound);
		}
	}

	private void CancelPreview()
	{
		if (_currentPreview != null)
		{
			_currentPreview.Delete();
			_currentPreview = null;
		}

		_resultProp = null;
		Finish(PreviewResult.Cancelled);

		ModLogger.Info("ItemPreview", "Colocação cancelada pelo jogador.");
	}

	/// <summary>
	/// Fecha o ciclo. É o único sítio que limpa registo, scaleform e modelo — todos os
	/// caminhos de saída passam por aqui para não deixar nada agarrado.
	/// </summary>
	private void Finish(PreviewResult result)
	{
		_preview = false;
		_loadingModel = false;
		_resultProp = result == PreviewResult.Placed ? _resultProp : null;
		_currentPropHash = string.Empty;
		_isDoor = false;
		_isSnappable = false;
		_ownHalfLength = 0f;
		_manualYawDegrees = 0f;

		if (_model != null)
		{
			_model.MarkAsNoLongerNeeded();
			_model = null;
		}

		SafeExecutor.Run("ItemPreview.DisposeScaleform", DisposeScaleform);
		Unregister();

		Result = result;
		PreviewComplete = true;
	}

	private void Unregister()
	{
		if (!_registered)
		{
			return;
		}

		_registered = false;

		ScriptEventHandler handler = ScriptEventHandler.Instance;
		if (handler == null)
		{
			return;
		}

		handler.UnregisterScript(_tickHandler);
		((Script)handler).Aborted -= _abortHandler;
	}

	// ------------------------------------------------------------------------------
	// Interface
	// ------------------------------------------------------------------------------

	private void EnsureScaleform()
	{
		if (_instructionalScaleform == null)
		{
			// RequestMovie é uma factory: devolve null enquanto o native não devolver
			// handle. Fica null e volta a tentar no frame seguinte.
			_instructionalScaleform = Scaleform.RequestMovie("instructional_buttons");
		}

		if (_instructionalScaleform == null || !_instructionalScaleform.IsLoaded)
		{
			return;
		}

		DrawScaleform();
	}

	/// <summary>
	/// Um só Scaleform, reutilizado. O original pedia um <c>new Scaleform()</c> por
	/// frame e nunca o descartava — era fuga de handles à taxa de 60/s.
	/// </summary>
	private void DrawScaleform()
	{
		Scaleform scaleform = _instructionalScaleform;

		scaleform.CallFunction("CLEAR_ALL");
		scaleform.CallFunction("TOGGLE_MOUSE_BUTTONS", 0);
		scaleform.CallFunction("CREATE_CONTAINER");

		scaleform.CallFunction("SET_DATA_SLOT", 0, "~INPUT_WEAPON_WHEEL_PREV~ ~INPUT_WEAPON_WHEEL_NEXT~", Lang.T("help.rotate_preview"));
		scaleform.CallFunction("SET_DATA_SLOT", 1,
			Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTONS_STRING, 2, (int)ControlAction.PhoneUp, 0), Lang.T("help.raise_preview"));
		scaleform.CallFunction("SET_DATA_SLOT", 2,
			Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTONS_STRING, 2, (int)ControlAction.PhoneDown, 0), Lang.T("help.lower_preview"));
		scaleform.CallFunction("SET_DATA_SLOT", 3,
			Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTONS_STRING, 2, (int)ControlAction.Sprint, 0), Lang.T("help.fast_preview"));

		// Opções de construção: o botão mostra sempre o estado actual, para o jogador
		// saber o que está ligado sem abrir nada.
		AddToggleSlot(scaleform, 4, GridToggleKey, _gridSnapEnabled, "help.grid_on", "help.grid_off");
		AddToggleSlot(scaleform, 5, TerrainToggleKey, _terrainAlignEnabled, "help.terrain_on", "help.terrain_off");
		AddToggleSlot(scaleform, 6, ObjectSnapToggleKey, _objectSnapEnabled, "help.snap_on", "help.snap_off");
		AddToggleSlot(scaleform, 7, RulesToggleKey, _buildRulesEnabled, "help.rules_on", "help.rules_off");
		AddToggleSlot(scaleform, 8, FeedbackToggleKey, _buildFeedbackEnabled, "help.feedback_on", "help.feedback_off");

		scaleform.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", -1);
		scaleform.Render2D();
	}

	/// <summary>
	/// Adiciona um botão de opção à scaleform: glyph da tecla + estado actual. Isolado
	/// porque são cinco opções com o mesmo formato.
	/// </summary>
	private static void AddToggleSlot(Scaleform scaleform, int slot, ControlAction key, bool enabled, string onKey, string offKey)
	{
		scaleform.CallFunction(
			"SET_DATA_SLOT",
			slot,
			Function.Call<string>(Hash.GET_CONTROL_INSTRUCTIONAL_BUTTONS_STRING, 2, (int)key, 0),
			Lang.T(enabled ? onKey : offKey));
	}

	private void DisposeScaleform()
	{
		if (_instructionalScaleform == null)
		{
			return;
		}

		_instructionalScaleform.Dispose();
		_instructionalScaleform = null;
	}

	/// <summary>
	/// Aplica ao ghost a aparência do estado de colocação (pisca quando bloqueado).
	/// Não desenha nada por cima do objecto. Embrulhada em SafeExecutor: falhar a
	/// actualização não pode custar o frame nem a colocação.
	/// </summary>
	private void ApplyGhostAppearance(PlacementStatus status)
	{
		if (_currentPreview == null)
		{
			return;
		}

		// Com o feedback desligado a ghost fica sempre neutra: sem pisca nem realce.
		PlacementStatus effective = _buildFeedbackEnabled ? status : PlacementStatus.Valid;
		SafeExecutor.Run(
			"ItemPreview.GhostAppearance",
			() => BuildFeedback.ApplyGhost(_currentPreview, effective));
	}

	private static string BuildControlsHelp()
	{
		return Lang.T("help.preview_controls");
	}

	private void DisplayHelpText(string helpText)
	{
		UiExtended.DisplayHelpTextThisFrame(helpText, ignoreMenus: true);
	}

	private static float Clamp(float value)
	{
		return value < -1f ? -1f : (value > 1f ? 1f : value);
	}

	private static string Describe(Vector3 position)
	{
		return string.Format("({0:0.0}, {1:0.0}, {2:0.0})", position.X, position.Y, position.Z);
	}
}
