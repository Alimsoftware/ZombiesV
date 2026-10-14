using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using GTA;

namespace ZombiesMod.Static;

/// <summary>
/// Localização (l10n) do mod.
///
/// Regras que este sistema existe para garantir:
/// <list type="bullet">
/// <item><description><b>Nunca mistura dois idiomas no mesmo ecrã.</b> Escolhe-se <i>um</i>;
/// só se cai para outro idioma quando uma chave concreta não existe.</description></item>
/// <item><description><b>Sem I/O por frame.</b> Os ficheiros são lidos uma vez e ficam num
/// dicionário em memória; <see cref="T(string)"/> é um lookup, não uma leitura de disco.</description></item>
/// <item><description><b>Uma única advertência por chave em falta.</b> Um tradutor esquecido
/// não consegue estrangular o log (nem o frame) com repetições.</description></item>
/// <item><description><b>Funciona mesmo se os ficheiros externos desaparecerem.</b> O
/// <c>pt</c> vai embutido no DLL, portanto o pior caso é ver português — nunca chaves cruas.</description></item>
/// </list>
///
/// Formato dos ficheiros: JSON plano com chaves por ponto (<c>"menu.inventory": "..."</c>),
/// comentários <c>//</c> e <c>/* */</c> permitidos, UTF-8.
/// </summary>
public static class Lang
{
	/// <summary>Idioma base. É o que vai embutido no DLL e o último recurso antes da chave.</summary>
	public const string DefaultLanguage = "pt";

	/// <summary>Idioma de recurso: se a chave não existir no idioma ativo, tenta-se este.</summary>
	public const string FallbackLanguage = "en";

	/// <summary>Diretório dos ficheiros <c>.json</c>, relativo à pasta do DLL (<c>scripts</c>).</summary>
	public const string LanguagesFolderName = "ZombiesMod/lang";

	private static readonly object Sync = new object();

	/// <summary>Chaves já reportadas, para não repetir o mesmo aviso.</summary>
	private static readonly HashSet<string> WarnedKeys = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>Sempre presente: a cópia embutida dentro do DLL.</summary>
	private static Dictionary<string, string> _embedded = Empty();

	/// <summary>Tabela ativa — é a que serve todas as chamadas.</summary>
	private static Dictionary<string, string> _active = Empty();

	/// <summary>Cadeia de recurso, da mais próxima para a mais longínqua (idioma base, en, embutido).</summary>
	private static Dictionary<string, string>[] _chain = new Dictionary<string, string>[0];

	/// <summary>
	/// Traduções registadas em tempo de execução (itens do catálogo do jogador). São
	/// consultadas antes de qualquer ficheiro de idioma, para um item custom nunca
	/// depender de editar o <c>pt.json</c> à mão.
	/// </summary>
	private static readonly Dictionary<string, string> _overrides = new Dictionary<string, string>(StringComparer.Ordinal);

	private static string _current = DefaultLanguage;
	private static bool _initialized;

	/// <summary>Código do idioma atualmente em uso (ex.: <c>pt</c>, <c>en</c>).</summary>
	public static string CurrentLanguage
	{
		get
		{
			EnsureInitialized();
			return _current;
		}
	}

	/// <summary>Caminho absoluto do diretório de idiomas.</summary>
	public static string LanguagesDirectory
	{
		get
		{
			string baseDirectory = null;
			try
			{
				string location = Assembly.GetExecutingAssembly().Location;
				if (!string.IsNullOrEmpty(location))
				{
					baseDirectory = Path.GetDirectoryName(location);
				}
			}
			catch (Exception)
			{
				// Sem localização da assembly (carga por bytes) — cai para o diretório base.
			}

			if (string.IsNullOrEmpty(baseDirectory))
			{
				baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "scripts");
			}

			return Path.Combine(baseDirectory, LanguagesFolderName);
		}
	}

	/// <summary>
	/// Traduz uma chave. Nunca devolve <c>null</c>.
	/// Ordem de recurso: idioma atual → idioma base → <c>en</c> → português embutido → a própria chave.
	/// </summary>
	public static string T(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return string.Empty;
		}

		EnsureInitialized();

		if (_overrides.TryGetValue(key, out string overridden))
		{
			return overridden;
		}

		for (int i = 0; i < _chain.Length; i++)
		{
			Dictionary<string, string> table = _chain[i];
			if (table != null && table.TryGetValue(key, out string value))
			{
				return value;
			}
		}

		WarnMissingOnce(key);
		return key;
	}

	/// <summary>
	/// Traduz uma chave e substitui os marcadores com <see cref="string.Format(IFormatProvider, string, object[])"/>.
	/// Ex.: <c>Lang.T("msg.items", 7)</c> com <c>"Você tem {0} itens"</c>.
	/// </summary>
	public static string T(string key, params object[] args)
	{
		string format = T(key);
		if (args == null || args.Length == 0)
		{
			return format;
		}

		try
		{
			return string.Format(CultureInfo.CurrentCulture, format, args);
		}
		catch (FormatException ex)
		{
			ModLogger.Warn("Lang.T", "marcadores inválidos na chave \"" + key + "\": " + ex.Message);
			return format;
		}
	}

	/// <summary>
	/// Troca o idioma em uso. Devolve <c>false</c> se o idioma não estiver disponível
	/// (não existe ficheiro nem é o idioma base), deixando o idioma anterior intacto.
	/// </summary>
	public static bool SetLanguage(string code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return false;
		}

		EnsureInitialized();

		string requested = code.Trim();
		try
		{
			Dictionary<string, string> table = LoadExternalTable(requested);
			if (table == null)
			{
				if (!string.Equals(requested, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
				{
					ModLogger.Warn("Lang", "idioma \"" + requested + "\" indisponível em " + LanguagesDirectory + ".");
					return false;
				}

				table = _embedded;
			}

			_current = requested;
			_active = table;
			RebuildChain();
			return true;
		}
		catch (Exception ex)
		{
			ModLogger.Error("Lang.SetLanguage", ex);
			return false;
		}
	}

	/// <summary>
	/// Força a leitura dos ficheiros de idioma. Opcional — <see cref="T(string)"/> chama
	/// isto sozinho na primeira utilização — mas ter um ponto explícito ajuda a isolar erros
	/// de arranque nos logs.
	/// </summary>
	public static void Initialize()
	{
		EnsureInitialized();
	}

	/// <summary>Lista os idiomas disponíveis (sem o idioma embutido, que não tem ficheiro).</summary>
	public static string[] AvailableLanguages()
	{
		try
		{
			string directory = LanguagesDirectory;
			if (!Directory.Exists(directory))
			{
				return Array.Empty<string>();
			}

			string[] files = Directory.GetFiles(directory, "*.json");
			var codes = new List<string>(files.Length);
			foreach (string file in files)
			{
				codes.Add(Path.GetFileNameWithoutExtension(file));
			}

			codes.Sort(StringComparer.OrdinalIgnoreCase);
			return codes.ToArray();
		}
		catch (Exception ex)
		{
			ModLogger.Warn("Lang.AvailableLanguages", ex.Message);
			return Array.Empty<string>();
		}
	}

	/// <summary>
	/// Compara as chaves de cada ficheiro de idioma com as do idioma base e devolve
	/// uma descrição dos problemas (chaves em falta ou a mais). Devolve uma lista vazia
	/// quando todos batem certo.
	/// </summary>
	/// <remarks>Feita para ser chamada por uma ferramenta ou em modo de depuração — não a
	/// executes a cada arranque normal.</remarks>
	public static List<string> ValidateKeyParity()
	{
		EnsureInitialized();

		var problems = new List<string>();
		Dictionary<string, string> reference = _embedded.Count > 0 ? _embedded : _active;

		foreach (string code in AvailableLanguages())
		{
			if (string.Equals(code, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			Dictionary<string, string> table;
			try
			{
				table = LoadExternalTable(code);
			}
			catch (Exception ex)
			{
				problems.Add(code + ": ilegível — " + ex.Message);
				continue;
			}

			if (table == null)
			{
				continue;
			}

			foreach (string key in reference.Keys)
			{
				if (!table.ContainsKey(key))
				{
					problems.Add(code + ": falta \"" + key + "\"");
				}
			}

			foreach (string key in table.Keys)
			{
				if (!reference.ContainsKey(key))
				{
					problems.Add(code + ": sobra \"" + key + "\" (não existe no idioma base)");
				}
			}
		}

		return problems;
	}

	/// <summary>
	/// Converte um identificador estável numa secção de chave: <c>"Raw Meat"</c> → <c>raw_meat</c>.
	/// Usado só para derivar chaves dos Ids de item, que por regra nunca são traduzidos.
	/// </summary>
	private static string Slug(string id)
	{
		var sb = new StringBuilder(id.Length + 8);
		foreach (char c in id)
		{
			if (char.IsLetterOrDigit(c))
			{
				sb.Append(char.ToLowerInvariant(c));
			}
			else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
			{
				sb.Append('_');
			}
		}

		return sb.ToString().TrimEnd('_');
	}

	/// <summary>
	/// Nome visível de um item.
	/// </summary>
	/// <remarks>
	/// O <c>Id</c> é identidade: vai serializado nos saves e é usado para comparações, portanto
	/// traduzir o próprio Id corromperia inventários guardados e partiria os lookup por nome.
	/// Traduz-se sim o nome derivado dele (<c>item.raw_meat.name</c>). Sem tradução, fica o Id.
	/// </remarks>
	public static string ItemName(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return id ?? string.Empty;
		}

		return LookupOrDefault("item." + Slug(id) + ".name", id);
	}

	/// <summary>
	/// Descrição visível de um item, usando a descrição gravada no objeto como recurso final.
	/// </summary>
	/// <remarks>
	/// A descrição está serializada nos saves — ler o ficheiro seria lento e nunca fonte de
	/// verdade para o idioma. Daí o texto renderizado vir sempre da tradução, e a descrição
	/// guardada servir apenas de rede.
	/// </remarks>
	public static string ItemDescription(string id, string storedDescription)
	{
		if (string.IsNullOrEmpty(id))
		{
			return storedDescription ?? string.Empty;
		}

		return LookupOrDefault("item." + Slug(id) + ".desc", storedDescription);
	}

	/// <summary>
	/// Igual a <see cref="T(string)"/> mas sem advertência: quem passa um recurso deliberado
	/// não precisa de ser avisado de que existe um caminho alternativo.
	/// </summary>
	private static string LookupOrDefault(string key, string fallback)
	{
		EnsureInitialized();

		if (_overrides.TryGetValue(key, out string overridden))
		{
			return overridden;
		}

		for (int i = 0; i < _chain.Length; i++)
		{
			Dictionary<string, string> table = _chain[i];
			if (table != null && table.TryGetValue(key, out string value))
			{
				return value;
			}
		}

		return fallback ?? key;
	}

	/// <summary>
	/// Registra em tempo de execução o nome e a descrição visíveis de um item. Existe para
	/// o catálogo de itens do jogador: um item custom aparece traduzido sem obrigar a
	/// editar os ficheiros de idioma. Valores nulos são ignorados (mantém-se a queda para
	/// o Id / para a descrição gravada no item).
	/// </summary>
	public static void RegisterItemText(string id, string name, string description)
	{
		if (string.IsNullOrEmpty(id))
		{
			return;
		}

		EnsureInitialized();

		lock (Sync)
		{
			if (!string.IsNullOrEmpty(name))
			{
				_overrides["item." + Slug(id) + ".name"] = name;
			}

			if (!string.IsNullOrEmpty(description))
			{
				_overrides["item." + Slug(id) + ".desc"] = description;
			}
		}
	}

	// ----------------------------------------------------------------- arranque

	private static void EnsureInitialized()
	{
		if (_initialized)
		{
			return;
		}

		lock (Sync)
		{
			if (_initialized)
			{
				return;
			}

			// Cada etapa é isolada: um ficheiro avariado não pode impedir o resto de funcionar.
			try
			{
				_embedded = LoadEmbeddedTable() ?? Empty();
			}
			catch (Exception ex)
			{
				ModLogger.Error("Lang.Init(pt embutido)", ex);
				_embedded = Empty();
			}

			_active = _embedded;
			_current = DefaultLanguage;

			RebuildChain();

			try
			{
				string configured = ReadConfiguredLanguage();
				if (!string.Equals(configured, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
				{
					SetLanguage(configured);
				}
			}
			catch (Exception ex)
			{
				ModLogger.Warn("Lang.Init(configuração)", ex.Message);
			}

			_initialized = true;
		}
	}

	/// <summary>
	/// Constrói a cadeia de recurso: idioma atual, depois a sua base regional (se for o caso),
	/// depois inglês, e por fim o português embutido. Cada tabela aparece uma única vez.
	/// </summary>
	private static void RebuildChain()
	{
		var chain = new List<Dictionary<string, string>>(4);
		var seen = new List<Dictionary<string, string>>(4);

		void Add(Dictionary<string, string> table)
		{
			if (table == null || table.Count == 0)
			{
				return;
			}

			for (int i = 0; i < seen.Count; i++)
			{
				if (ReferenceEquals(seen[i], table))
				{
					return;
				}
			}

			seen.Add(table);
			chain.Add(table);
		}

		Add(_active);

		// "pt-BR" cai em "pt" antes de saltar para inglês — é o que um tradutor espera.
		int dash = _current.IndexOf('-');
		if (dash > 0 && dash < _current.Length - 1)
		{
			string baseCode = _current.Substring(0, dash);
			if (!string.Equals(baseCode, _current, StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					Add(LoadExternalTable(baseCode));
				}
				catch (Exception ex)
				{
					ModLogger.Warn("Lang", "falha ao ler a base \"" + baseCode + "\": " + ex.Message);
				}
			}
		}

		if (!string.Equals(_current, FallbackLanguage, StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				Add(LoadExternalTable(FallbackLanguage));
			}
			catch (Exception ex)
			{
				ModLogger.Warn("Lang", "falha ao ler \"" + FallbackLanguage + "\": " + ex.Message);
			}
		}

		Add(_embedded);

		_chain = chain.ToArray();
	}

	private static string ReadConfiguredLanguage()
	{
		try
		{
			ScriptSettings ini = ScriptSettings.Load(Config.IniFilePath);
			string value = ini.GetValue("mod", "language", DefaultLanguage);
			return string.IsNullOrWhiteSpace(value) ? DefaultLanguage : value.Trim();
		}
		catch (Exception ex)
		{
			ModLogger.Warn("Lang", "não foi possível ler \"language\" do INI: " + ex.Message);
			return DefaultLanguage;
		}
	}

	// ----------------------------------------------------------------- leitura

	/// <summary>Lê <c>{LanguagesDirectory}\{code}.json</c>; devolve <c>null</c> se não existir.</summary>
	private static Dictionary<string, string> LoadExternalTable(string code)
	{
		string path = Path.Combine(LanguagesDirectory, code + ".json");
		if (!File.Exists(path))
		{
			return null;
		}

		// StreamReader lê e remove o BOM se existir; o parser também ignora U+FEFF.
		using (var stream = File.OpenRead(path))
		using (var reader = new StreamReader(stream, Encoding.UTF8))
		{
			return Json.ParseFlatObject(reader.ReadToEnd(), Path.GetFileName(path));
		}
	}

	/// <summary>Lê o <c>pt</c> embutido no DLL. Devolve <c>null</c> se o recurso não existir.</summary>
	private static Dictionary<string, string> LoadEmbeddedTable()
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		string resource = null;

		foreach (string name in assembly.GetManifestResourceNames())
		{
			if (name.EndsWith(DefaultLanguage + ".json", StringComparison.OrdinalIgnoreCase))
			{
				resource = name;
				break;
			}
		}

		if (resource == null)
		{
			return null;
		}

		using (Stream stream = assembly.GetManifestResourceStream(resource))
		{
			if (stream == null)
			{
				return null;
			}

			using (var reader = new StreamReader(stream, Encoding.UTF8))
			{
				return Json.ParseFlatObject(reader.ReadToEnd(), resource);
			}
		}
	}

	// ----------------------------------------------------------------- suporte

	private static void WarnMissingOnce(string key)
	{
		lock (Sync)
		{
			if (!WarnedKeys.Add(key))
			{
				return;
			}
		}

		// Fora do lock: escrever no disco não deve reter o lock partilhado.
		ModLogger.Warn(
			"Lang",
			"chave \"" + key + "\" ausente de \"" + _current + "\", do fallback e do idioma embutido — a mostrar a chave.");
	}

	private static Dictionary<string, string> Empty()
	{
		return new Dictionary<string, string>(StringComparer.Ordinal);
	}
}
