using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ZombiesMod.Static;

/// <summary>
/// Registo de erros do mod.
///
/// O ScriptHookVDotNet3 já tem o seu próprio log (ScriptHookVDotNet.log), mas a
/// ScriptDomain captura a excepção, escreve lá e depois ABORTA o script — a primeira
/// excepção não tratada mata o mod até reiniciar o jogo.
///
/// Este ficheiro serve para o caminho oposto: fica registado e o mod continua.
/// </summary>
public static class ModLogger
{
    /// <summary>Tamanho máximo do ficheiro antes de rodar: 5 MB.</summary>
    public const long MaxLogBytes = 5L * 1024 * 1024;

    /// <summary>
    /// Toda a escrita passa por aqui. O SHVDN3 pode correr scripts em threads diferentes
    /// da principal, e sem o lock a rotação podia acontecer a meio de uma escrita.
    /// </summary>
    private static readonly object Sync = new object();

    private static readonly string _logDirectory;
    private static readonly string _logPath;

    static ModLogger()
    {
        // AppDomain.CurrentDomain.BaseDirectory é a pasta do GTA5.exe, ou seja a raiz do
        // jogo. Calcula-se uma única vez: o AppDomain não muda durante a execução.
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Rede de segurança: se algum dia a assembly for carregada a partir da pasta
        // scripts\, não queremos acabar com .\scripts\scripts\ZombiesMod_logs\.
        string folder = Path.GetFileName(
            baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        _logDirectory = string.Equals(folder, "scripts", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(baseDirectory, "ZombiesMod_logs")
            : Path.Combine(baseDirectory, "scripts", "ZombiesMod_logs");

        _logPath = Path.Combine(_logDirectory, "errors.log");
    }

    /// <summary>Pasta onde ficam os ficheiros de log.</summary>
    public static string LogDirectory => _logDirectory;

    /// <summary>Ficheiro de erros activo.</summary>
    public static string LogPath => _logPath;

    /// <summary>
    /// Regista uma excepção com o stacktrace completo. Aceita <c>null</c> sem rebentar,
    /// porque um <c>catch (Exception ex)</c> sem <c>ex != null</c> já apanhou muita gente.
    /// </summary>
    public static void Error(string context, Exception exception)
    {
        if (exception == null)
        {
            Error(context, "Excepção recebida como nula.");
            return;
        }

        // exception.ToString() ja comeca com "Tipo: Mensagem" e continua com o stacktrace
        // completo em linhas seguintes. Mete-lo inteiro como mensagem evita repetir o
        // resumo duas vezes, que e o que acontece se passarmos resumo E ToString().
        // O TrimEnd garante que nao fica uma linha em branco no fim por causa do \r\n
        // que o proprio ToString() pode trazer.
        //
        // Avaliado DENTRO do try do Write: se ToString() falhar numa excepção maluca,
        // perdemos uma linha em vez de a excepção subir pelo SafeExecutor — que era
        // exactamente o que este logger existe para evitar.
        Exception captured = exception;
        Write(() => Build(context, captured.ToString().TrimEnd('\r', '\n'), null));
    }

    /// <summary>Regista uma mensagem de erro que não veio de uma excepção.</summary>
    public static void Error(string context, string message)
    {
        Write(() => Build(context, message, null));
    }

    /// <summary>Diagnóstico normal — para perceber em jogo o que o mod andou a fazer.</summary>
    public static void Info(string context, string message)
    {
        Write(() => Build(context, message, null));
    }

    /// <summary>
    /// Situação anómala que não mata nada: um valor nulo onde se esperava dado, uma
    /// colocação recusada, um modelo que não carregou. Fica registado e o mod segue.
    ///
    /// Vai como prefixo da mensagem e não como campo extra, para o formato
    /// [TIMESTAMP] [THREAD] [CONTEXT] MESSAGE continuar estável — foi assim que se
    /// extraiu o contexto nas análises anteriores e quebrar isso custa mais do que
    /// ganha.
    /// </summary>
    public static void Warn(string context, string message)
    {
        Write(() => Build(context, "AVISO: " + message, null));
    }

    /// <summary>[TIMESTAMP] [THREAD] [CONTEXT] MESSAGE, mais o stacktrace em linhas seguintes.</summary>
    private static string Build(string context, string message, string stackTrace)
    {
        StringBuilder builder = new StringBuilder(256);

        builder.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(']');
        builder.Append(" [").Append(CurrentThreadName()).Append(']');
        builder.Append(" [").Append(string.IsNullOrEmpty(context) ? "?" : context).Append("] ");
        builder.Append(message ?? "(sem mensagem)");
        builder.AppendLine();

        if (!string.IsNullOrEmpty(stackTrace))
        {
            builder.AppendLine(stackTrace);
        }

        return builder.ToString();
    }

    /// <summary>
    /// O SHVDN3 não nomeia as threads dos scripts, por isso cai para o número gerido —
    /// suficiente para saber se um erro veio do tick principal ou de outro sítio.
    /// </summary>
    private static string CurrentThreadName()
    {
        Thread current = Thread.CurrentThread;

        return string.IsNullOrEmpty(current.Name)
            ? "T" + current.ManagedThreadId
            : current.Name;
    }

    /// <summary>
    /// Constrói e grava dentro do MESMO try. Aceita um delegado e não a linha já
    /// construída de propósito: assim a montagem da mensagem também fica coberta, e
    /// este método passa a ser o único sítio de onde nada pode escapar.
    /// </summary>
    private static void Write(Func<string> buildLine)
    {
        if (buildLine == null)
        {
            return;
        }

        lock (Sync)
        {
            try
            {
                string text = buildLine();
                Directory.CreateDirectory(_logDirectory);
                RotateIfNeeded();
                File.AppendAllText(_logPath, text, Encoding.UTF8);
            }
            catch
            {
                // Um logger a avariar não pode ser a causa de o mod morrer. O preço é
                // perder uma linha; a alternativa é propagar e voltar ao problema
                // original (a ScriptDomain aborta o script).
            }
        }
    }

    private static void RotateIfNeeded()
    {
        FileInfo info = new FileInfo(_logPath);

        if (!info.Exists || info.Length < MaxLogBytes)
        {
            return;
        }

        // Uma única cópia de reserva, como pedido: a rodada anterior é descartada em vez
        // de acumular errors.log.1, .2, .3...
        string backup = _logPath + ".1";

        File.Delete(backup);
        File.Move(_logPath, backup);
    }
}
