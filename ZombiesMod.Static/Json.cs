using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZombiesMod.Static;

/// <summary>
/// Parser JSON "plano" (sem objetos nem arrays aninhados) com suporte a comentários.
/// Escrito de raiz para net48 — o projeto não tem e não precisa de nenhum pacote NuGet.
///
/// Formato aceite:
/// <code>
/// // comentário de linha
/// /* comentário de bloco *&#47;
/// {
///   "menu.inventory": "Inventário",
///   "msg.items":       "Você tem {0} itens"
/// }
/// </code>
///
/// Assegura que qualquer erro inclui ficheiro, linha e coluna, para quem
/// editar os ficheiros de idioma pela mão conseguir corrigir sem adivinhar.
/// </summary>
internal static class Json
{
	/// <summary>
	/// Analisa um objeto JSON plano e devolve as pares chave/valor.
	/// </summary>
	/// <param name="text">Conteúdo do ficheiro.</param>
	/// <param name="source">Nome do ficheiro, usado apenas nas mensagens de erro.</param>
	/// <exception cref="FormatException">Mensagem no formato "fonte: linha N, coluna M: motivo".</exception>
	public static Dictionary<string, string> ParseFlatObject(string text, string source)
	{
		if (text == null)
		{
			throw new FormatException(source + ": conteúdo nulo.");
		}

		var reader = new Reader(text, source);
		var result = new Dictionary<string, string>(StringComparer.Ordinal);

		reader.SkipTrivia();
		reader.Expect('{');
		reader.SkipTrivia();

		if (!reader.AtEnd && reader.Peek() == '}')
		{
			reader.Advance();
			reader.SkipTrivia();
			reader.ExpectEnd();
			return result;
		}

		while (true)
		{
			reader.SkipTrivia();
			string key = reader.ReadString();

			reader.SkipTrivia();
			reader.Expect(':');
			reader.SkipTrivia();

			result[key] = reader.ReadValue(key);

			reader.SkipTrivia();
			char next = reader.Peek();
			if (next == ',')
			{
				reader.Advance();
				reader.SkipTrivia();
				continue;
			}

			if (next == '}')
			{
				reader.Advance();
				break;
			}

			throw reader.Error("esperado ',' ou '}' depois do valor da chave \"" + key + "\".");
		}

		reader.SkipTrivia();
		reader.ExpectEnd();
		return result;
	}

	/// <summary>Leitor com posição precisa (linha/coluna) para mensagens de erro acionáveis.</summary>
	private sealed class Reader
	{
		private readonly string _text;
		private readonly string _source;
		private int _index;
		private int _line = 1;
		private int _column = 1;

		public Reader(string text, string source)
		{
			_text = text;
			_source = source;
		}

		public bool AtEnd => _index >= _text.Length;

		public char Peek()
		{
			if (AtEnd)
			{
				throw Error("o ficheiro terminou de forma inesperada.");
			}
			return _text[_index];
		}

		public char PeekNext()
		{
			return _index + 1 < _text.Length ? _text[_index + 1] : '\0';
		}

		public void Advance()
		{
			if (AtEnd)
			{
				return;
			}
			if (_text[_index] == '\n')
			{
				_line++;
				_column = 1;
			}
			else
			{
				_column++;
			}
			_index++;
		}

		public FormatException Error(string message)
		{
			return new FormatException(string.Format(
				CultureInfo.InvariantCulture,
				"{0}: linha {1}, coluna {2}: {3}",
				_source, _line, _column, message));
		}

		public void Expect(char expected)
		{
			if (AtEnd)
			{
				throw Error("esperado '" + expected + "', mas o ficheiro terminou.");
			}
			char actual = Peek();
			if (actual != expected)
			{
				throw Error("esperado '" + expected + "', encontrado '" + actual + "'.");
			}
			Advance();
		}

		public void ExpectEnd()
		{
			if (!AtEnd)
			{
				throw Error("conteúdo extra depois do fim do objeto: '" + Peek() + "'.");
			}
		}

		/// <summary>Espaço em branco, BOM, comentários // e /* */ (JSONC).</summary>
		public void SkipTrivia()
		{
			while (!AtEnd)
			{
				char c = Peek();
				if (char.IsWhiteSpace(c) || c == '﻿')
				{
					Advance();
					continue;
				}

				if (c == '/' && PeekNext() == '/')
				{
					while (!AtEnd && Peek() != '\n')
					{
						Advance();
					}
					continue;
				}

				if (c == '/' && PeekNext() == '*')
				{
					Advance();
					Advance();
					while (true)
					{
						if (AtEnd)
						{
							throw Error("comentário /* por fechar.");
						}
						if (Peek() == '*' && PeekNext() == '/')
						{
							Advance();
							Advance();
							break;
						}
						Advance();
					}
					continue;
				}

				break;
			}
		}

		public string ReadValue(string key)
		{
			if (AtEnd)
			{
				throw Error("esperado um valor para a chave \"" + key + "\".");
			}

			char c = Peek();
			if (c == '"')
			{
				return ReadString();
			}

			if (c == '{' || c == '[')
			{
				throw Error(
					"só são aceites valores texto; objetos e arrays não são suportados. " +
					"Usa chaves planas — por exemplo \"secao.chave\": \"texto\".");
			}

			return ReadScalar(key);
		}

		/// <summary>Lê true/false/null ou um número; devolve a forma textual.</summary>
		private string ReadScalar(string key)
		{
			int start = _index;
			while (!AtEnd)
			{
				char c = Peek();
				if (c == ',' || c == '}' || c == ']' || char.IsWhiteSpace(c))
				{
					break;
				}
				Advance();
			}

			if (_index == start)
			{
				throw Error("esperado um valor para a chave \"" + key + "\".");
			}

			return _text.Substring(start, _index - start);
		}

		public string ReadString()
		{
			Expect('"');
			var sb = new StringBuilder();

			while (true)
			{
				if (AtEnd)
				{
					throw Error("cadeia de texto por fechar.");
				}

				char c = Peek();
				if (c == '"')
				{
					Advance();
					return sb.ToString();
				}

				if (c == '\n')
				{
					throw Error("quebra de linha dentro de uma cadeia de texto — usa \\n.");
				}

				if (c != '\\')
				{
					sb.Append(c);
					Advance();
					continue;
				}

				Advance();
				if (AtEnd)
				{
					throw Error("escape incompleto no fim da cadeia.");
				}

				char escape = Peek();
				Advance();
				switch (escape)
				{
					case '"':
						sb.Append('"');
						break;
					case '\\':
						sb.Append('\\');
						break;
					case '/':
						sb.Append('/');
						break;
					case 'b':
						sb.Append('\b');
						break;
					case 'f':
						sb.Append('\f');
						break;
					case 'n':
						sb.Append('\n');
						break;
					case 'r':
						sb.Append('\r');
						break;
					case 't':
						sb.Append('\t');
						break;
					case 'u':
						sb.Append(ReadUnicodeEscape());
						break;
					default:
						throw Error("escape inválido '\\" + escape + "'.");
				}
			}
		}

		private char ReadUnicodeEscape()
		{
			int value = 0;
			for (int i = 0; i < 4; i++)
			{
				if (AtEnd)
				{
					throw Error("escape \\u incompleto.");
				}

				char c = Peek();
				Advance();

				int digit;
				if (c >= '0' && c <= '9')
				{
					digit = c - '0';
				}
				else if (c >= 'a' && c <= 'f')
				{
					digit = c - 'a' + 10;
				}
				else if (c >= 'A' && c <= 'F')
				{
					digit = c - 'A' + 10;
				}
				else
				{
					throw Error("dígito hexadecimal inválido em \\u: '" + c + "'.");
				}

				value = (value << 4) | digit;
			}

			return (char)value;
		}
	}
}
