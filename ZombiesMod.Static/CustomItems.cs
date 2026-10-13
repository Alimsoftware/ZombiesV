using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GTA;
using GTA.Math;
using UI = GTA.UI;

namespace ZombiesMod.Static;

/// <summary>
/// Catálogo de itens definido pelo jogador.
///
/// Existe para o mod não ficar limitado aos objetos escritos no código: quem joga
/// acrescenta recursos e objetos construíveis num ficheiro JSON (o mesmo formato
/// plano do <c>pt.json</c>, comentários incluídos) e eles aparecem no inventário,
/// prontos a construir, sem recompilar nada.
///
/// Regras que este sistema garante:
/// <list type="bullet">
/// <item><description><b>Nunca duplica itens.</b> Se o Id já existir (embutido ou já
/// gravado no save), a definição é ignorada.</description></item>
/// <item><description><b>Uma receita só usa recursos existentes.</b> Como a construção
/// consome <see cref="Inventory.Resources"/>, um ingrediente que não seja recurso é
/// recusado em vez de dar um item de graça.</description></item>
/// <item><description><b>Um erro não trava o jogo.</b> O ficheiro inteiro é validado
/// antes de tocar no inventário; um item mal formado é saltado e registado no log.</description></item>
/// </list>
/// </summary>
public static class CustomItems
{
	/// <summary>Nome do ficheiro, criado ao lado dos restantes dados do mod.</summary>
	public const string FileName = "ZombiesMod_CustomItems.json";

	/// <summary>Prefixo das chaves: <c>"item.&lt;Chave&gt;.&lt;campo&gt;"</c>.</summary>
	private const string Prefix = "item.";

	/// <summary>Caminho completo do ficheiro do jogador.</summary>
	public static string FilePath => Path.Combine(Config.ScriptFilePath, FileName);

	/// <summary>
	/// Aplica o ficheiro do jogador a um inventário. Devolve quantos itens foram
	/// criados (0 quando o ficheiro não existe ou está vazio). Nunca lança.
	/// </summary>
	public static int Apply(Inventory inventory)
	{
		if (inventory == null)
		{
			return 0;
		}

		try
		{
			if (!File.Exists(FilePath))
			{
				WriteTemplate(FilePath);
				return 0;
			}
		}
		catch (Exception ex)
		{
			ModLogger.Error("CustomItems.Verificar", ex);
			return 0;
		}

		Dictionary<string, string> entries;
		try
		{
			entries = Json.ParseFlatObject(File.ReadAllText(FilePath, Encoding.UTF8), FileName);
		}
		catch (Exception ex)
		{
			ModLogger.Error("CustomItems.Ler", ex);
			SafeNotify(Lang.T("notify.customitems_error"));
			return 0;
		}

		List<CustomItemDefinition> definitions = GroupDefinitions(entries);
		var created = new List<KeyValuePair<CustomItemDefinition, InventoryItemBase>>();

		foreach (CustomItemDefinition definition in definitions)
		{
			// O nome/descrição aplicam-se SEMPRE — também a itens que já vinham do save,
			// senão a partir da segunda execução o item voltava a mostrar o Id.
			Lang.RegisterItemText(definition.Key, definition.Name, definition.Description);

			if (FindById(inventory, definition.Key) != null)
			{
				// Já existe (embutido ou já no save). Nunca duplicar; editar um item já
				// existente exige apagar o Inventory.dat (o bump de versão já o faz).
				continue;
			}

			InventoryItemBase item = CreateItem(definition);
			if (item == null)
			{
				continue;
			}

			(definition.IsResource ? inventory.Resources : inventory.Items).Add(item);
			created.Add(new KeyValuePair<CustomItemDefinition, InventoryItemBase>(definition, item));
		}

		// Só depois de tudo criado é que as receitas se resolvem: assim uma receita
		// pode apontar para outro item do mesmo ficheiro.
		foreach (KeyValuePair<CustomItemDefinition, InventoryItemBase> pair in created)
		{
			ResolveRecipe(pair.Key, pair.Value, inventory);
		}

		if (created.Count > 0)
		{
			ModLogger.Info("CustomItems", created.Count + " item(ns) personalizado(s) carregado(s) de " + FileName + ".");
			SafeNotify(Lang.T("notify.customitems_loaded", created.Count));
		}

		return created.Count;
	}

	// ------------------------------------------------------------------- criação

	private static InventoryItemBase CreateItem(CustomItemDefinition definition)
	{
		string description = definition.Description ?? string.Empty;

		if (definition.IsResource)
		{
			return new InventoryItemBase(definition.Amount ?? 0, definition.Max ?? 25, definition.Key, description);
		}

		if (!string.IsNullOrEmpty(definition.Type) && !string.Equals(definition.Type, "buildable", StringComparison.OrdinalIgnoreCase))
		{
			ModLogger.Warn("CustomItems", "tipo \"" + definition.Type + "\" desconhecido em \"" + definition.Key + "\" (usa \"resource\" ou \"buildable\") — ignorado.");
			return null;
		}

		if (string.IsNullOrEmpty(definition.Model))
		{
			ModLogger.Warn("CustomItems", "o construível \"" + definition.Key + "\" não tem \"model\" — ignorado.");
			return null;
		}

		return new BuildableInventoryItem(
			definition.Amount ?? 0,
			definition.Max ?? 10,
			definition.Key,
			description,
			definition.Model,
			(BlipSprite)(definition.Blip ?? 1),
			(BlipColor)(definition.BlipColor ?? 0),
			definition.Offset ?? Vector3.Zero,
			interactable: definition.Interact ?? false,
			isDoor: definition.Door ?? false,
			canBePickedUp: definition.Pickup ?? true,
			isSnappable: definition.Snap ?? false);
	}

	private static void ResolveRecipe(CustomItemDefinition definition, InventoryItemBase item, Inventory inventory)
	{
		if (!(item is ICraftable craftable))
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(definition.Recipe))
		{
			// Sem receita: constrói-se de graça (lista vazia, não nula, para o menu dar o visto).
			craftable.RequiredComponents = new CraftableItemComponent[0];
			return;
		}

		var components = new List<CraftableItemComponent>();
		foreach (string raw in definition.Recipe.Split(';', ','))
		{
			string part = raw.Trim();
			if (part.Length == 0)
			{
				continue;
			}

			int colon = part.LastIndexOf(':');
			string id = (colon > 0 ? part.Substring(0, colon) : part).Trim();
			int quantity = 1;
			if (colon > 0 && !int.TryParse(part.Substring(colon + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity))
			{
				ModLogger.Warn("CustomItems", "quantidade inválida em \"" + part + "\" (item \"" + definition.Key + "\").");
				continue;
			}

			if (quantity <= 0)
			{
				continue;
			}

			InventoryItemBase resource = inventory.Resources.Find(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
			if (resource == null)
			{
				// A craft consome sempre Resources; um Id fora dessa lista deixaria o item
				// preso no menu a pedir algo que nunca é gasto.
				ModLogger.Warn("CustomItems", "recurso \"" + id + "\" (receita de \"" + definition.Key + "\") não existe — componente ignorado.");
				continue;
			}

			components.Add(new CraftableItemComponent(resource, quantity));
		}

		if (components.Count == 0)
		{
			// Uma receita escrita mas sem nada válido fica bloqueada, em vez de dar o item
			// de graça por causa de um erro de escrita.
			ModLogger.Warn("CustomItems", "receita de \"" + definition.Key + "\" sem componentes válidos — item fica não construível.");
			craftable.RequiredComponents = null;
			return;
		}

		craftable.RequiredComponents = components.ToArray();
	}

	// ------------------------------------------------------------------ leitura

	/// <summary>
	/// Agrupa as chaves planas (<c>item.&lt;Chave&gt;.&lt;campo&gt;</c>) em definições,
	/// preservando a ordem em que aparecem no ficheiro.
	/// </summary>
	private static List<CustomItemDefinition> GroupDefinitions(Dictionary<string, string> entries)
	{
		var order = new List<string>();
		var map = new Dictionary<string, CustomItemDefinition>(StringComparer.OrdinalIgnoreCase);

		foreach (KeyValuePair<string, string> entry in entries)
		{
			if (!entry.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string rest = entry.Key.Substring(Prefix.Length);
			int separator = rest.IndexOf('.');
			if (separator <= 0 || separator == rest.Length - 1)
			{
				ModLogger.Warn("CustomItems", "chave ignorada (esperado \"item.<Chave>.<campo>\"): \"" + entry.Key + "\".");
				continue;
			}

			string key = rest.Substring(0, separator).Trim();
			string field = rest.Substring(separator + 1).Trim().ToLowerInvariant();
			if (key.Length == 0)
			{
				continue;
			}

			if (!map.TryGetValue(key, out CustomItemDefinition definition))
			{
				definition = new CustomItemDefinition(key);
				map.Add(key, definition);
				order.Add(key);
			}

			definition.Set(field, entry.Value);
		}

		var result = new List<CustomItemDefinition>(order.Count);
		foreach (string key in order)
		{
			result.Add(map[key]);
		}

		return result;
	}

	private static InventoryItemBase FindById(Inventory inventory, string id)
	{
		InventoryItemBase item = inventory.Items.Find(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
		if (item != null)
		{
			return item;
		}

		return inventory.Resources.Find(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	private static int? ParseInt(string value)
	{
		if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
		{
			return parsed;
		}

		ModLogger.Warn("CustomItems", "número inválido: \"" + value + "\".");
		return null;
	}

	private static bool? ParseBool(string value)
	{
		string trimmed = value.Trim();
		if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) || trimmed == "1")
		{
			return true;
		}

		if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) || trimmed == "0")
		{
			return false;
		}

		ModLogger.Warn("CustomItems", "booleano inválido: \"" + value + "\" (usa true/false).");
		return null;
	}

	private static Vector3? ParseVector(string value)
	{
		string[] parts = value.Split(',');
		if (parts.Length != 3)
		{
			ModLogger.Warn("CustomItems", "offset inválido: \"" + value + "\" (usa \"x,y,z\").");
			return null;
		}

		var numbers = new float[3];
		for (int i = 0; i < 3; i++)
		{
			if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
			{
				ModLogger.Warn("CustomItems", "offset inválido: \"" + value + "\" (usa \"x,y,z\").");
				return null;
			}
		}

		return new Vector3(numbers[0], numbers[1], numbers[2]);
	}

	private static void SafeNotify(string message)
	{
		try
		{
			UI.Notification.Show(message, false);
		}
		catch (Exception ex)
		{
			ModLogger.Error("CustomItems.Notify", ex);
		}
	}

	private static void WriteTemplate(string path)
	{
		try
		{
			File.WriteAllText(path, Template, new UTF8Encoding(false));
			ModLogger.Info("CustomItems", "criado o modelo de exemplo " + FileName + ".");
		}
		catch (Exception ex)
		{
			ModLogger.Error("CustomItems.Modelo", ex);
		}
	}

	// ------------------------------------------------------------------- modelo

	/// <summary>Definição de um item, montada campo a campo a partir das chaves planas.</summary>
	private sealed class CustomItemDefinition
	{
		public CustomItemDefinition(string key)
		{
			Key = key;
		}

		public string Key { get; }

		public string Type { get; private set; }

		public string Name { get; private set; }

		public string Description { get; private set; }

		public string Model { get; private set; }

		public string Recipe { get; private set; }

		public int? Max { get; private set; }

		public int? Amount { get; private set; }

		public bool? Snap { get; private set; }

		public bool? Door { get; private set; }

		public bool? Interact { get; private set; }

		public bool? Pickup { get; private set; }

		public int? Blip { get; private set; }

		public int? BlipColor { get; private set; }

		public Vector3? Offset { get; private set; }

		public bool IsResource => string.Equals(Type, "resource", StringComparison.OrdinalIgnoreCase);

		public void Set(string field, string value)
		{
			switch (field)
			{
				case "type":
					Type = value.Trim();
					break;
				case "name":
					Name = value;
					break;
				case "desc":
				case "description":
					Description = value;
					break;
				case "model":
					Model = value.Trim();
					break;
				case "recipe":
					Recipe = value;
					break;
				case "max":
					Max = ParseInt(value);
					break;
				case "amount":
				case "start":
					Amount = ParseInt(value);
					break;
				case "snap":
					Snap = ParseBool(value);
					break;
				case "door":
					Door = ParseBool(value);
					break;
				case "interact":
				case "interactable":
					Interact = ParseBool(value);
					break;
				case "pickup":
					Pickup = ParseBool(value);
					break;
				case "blip":
					Blip = ParseInt(value);
					break;
				case "blipcolor":
					BlipColor = ParseInt(value);
					break;
				case "offset":
					Offset = ParseVector(value);
					break;
				default:
					// Campo desconhecido: ignorado de propósito, para o ficheiro poder
					// evoluir sem quebrar versões antigas.
					break;
			}
		}
	}

	private const string Template = @"// =============================================================================
// ZombiesMod — catálogo de itens do jogador
//
// Tudo o que escreveres neste ficheiro passa a existir no mod, sem mexer no código.
// Cada item é descrito por várias chaves com o formato:
//
//     ""item.<Chave>.<campo>"": ""<valor>""
//
// <Chave> é o Id do item — o nome que aparece no inventário e que fica gravado no
// save. Escolhe uma Chave única e sem pontos. Todos os campos são opcionais, exceto
// ""model"" nos objetos construíveis:
//
//   type       ""resource""  -> ingrediente/recurso (aparece no separador Recursos)
//              ""buildable"" -> objeto que se coloca no mundo (por omissão)
//   name       Nome visível. Sem isto, mostra-se a própria Chave.
//   desc       Descrição mostrada no inventário.
//   max        Quantidade máxima (por omissão 10; recursos 25).
//   amount     Quantidade com que começas (por omissão 0).
//   model      (construível) Nome do modelo/prop do GTA V. OBRIGATÓRIO.
//   snap       (construível) ""true"" para encaixar borda a borda com outras peças.
//   door       (construível) ""true"" se for uma porta.
//   interact   (construível) ""true"" para abrir o menu ao interagir com a peça.
//   pickup     (construível) ""true"" (por omissão) para poder recolher a peça.
//   blip       (construível) Sprite do blip no mapa (1 = sem blip).
//   blipcolor  (construível) Cor do blip.
//   offset     (construível) Deslocamento ao assentar no chão, formato ""x,y,z"".
//   recipe     Ingredientes, formato ""Recurso:qtd; Recurso:qtd"". Os recursos têm
//              de existir já (Wood, Metal, Binding, Cloth, Battery, Plastic...).
//              Sem recipe, o objeto constrói-se de graça.
//
// Os nomes de modelos existem no ObjectList.ini (dump de objetos do GTA V).
// =============================================================================
{
  // Exemplo 1 — um recurso novo (descomenta para usar)
  // ""item.Nail.type"": ""resource"",
  // ""item.Nail.name"": ""Prego"",
  // ""item.Nail.desc"": ""Um prego de aço."",
  // ""item.Nail.max"": ""50"",

  // Exemplo 2 — um objeto construível novo (descomenta para usar)
  // ""item.Wood Fence.type"": ""buildable"",
  // ""item.Wood Fence.name"": ""Cerca de Madeira"",
  // ""item.Wood Fence.desc"": ""Uma cerca que bloqueia a passagem."",
  // ""item.Wood Fence.model"": ""prop_fncwood_02b"",
  // ""item.Wood Fence.max"": ""20"",
  // ""item.Wood Fence.snap"": ""true"",
  // ""item.Wood Fence.recipe"": ""Wood:4; Binding:2""
}";
}
