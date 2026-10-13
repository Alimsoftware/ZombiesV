using System;

namespace ZombiesMod.Static;

/// <summary>
/// Corre código de risco sem deixar a excepção subir.
///
/// Porquê: a ScriptDomain do SHVDN3 apanha a excepção, escreve no
/// ScriptHookVDotNet.log e depois aborta o script. Isto inverte a ordem — fica
/// registado no log do mod e o script continua no frame seguinte.
///
/// A troca é assumida: em vez de o script morrer, a operação que falhou devolve
/// <c>default</c> (false, null, 0). Quem chama tem de estar ok com isso.
/// </summary>
public static class SafeExecutor
{
    /// <summary>Executa uma acção sem propagar excepções.</summary>
    public static void Run(string context, Action action)
    {
        if (action == null)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception exception)
        {
            ModLogger.Error(context, exception);
        }
    }

    /// <summary>
    /// Executa uma função sem propagar excepções. Se algo falhar devolve
    /// <paramref name="defaultValue"/>, para quem chama conseguir distinguir
    /// «devolveu falso» de «nunca chegou a correr» se precisar.
    /// </summary>
    public static T Run<T>(string context, Func<T> func, T defaultValue = default)
    {
        if (func == null)
        {
            return defaultValue;
        }

        try
        {
            return func();
        }
        catch (Exception exception)
        {
            ModLogger.Error(context, exception);
            return defaultValue;
        }
    }

    /// <summary>
    /// Instala o handler da AppDomain. Idempotente: chamar mais do que uma vez não
    /// regista o handler duas vezes, por isso pode ser chamado por todos os scripts.
    /// </summary>
    public static void InstallGlobalHandler()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Última linha de defesa. A AppDomain morre a seguir, por isso isto só garante que
    /// a causa fica escrita em vez de desaparecer com o processo. Não serve para salvar
    /// o jogo — só para se perceber o que o matou.
    /// </summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        try
        {
            Exception exception = eventArgs.ExceptionObject as Exception;

            if (exception != null)
            {
                ModLogger.Error("AppDomain", exception);
            }
            else
            {
                ModLogger.Error("AppDomain", "ExceptionObject sem tipo Exception: " + eventArgs.ExceptionObject);
            }

            ModLogger.Error("AppDomain", "Excepção não tratada — o processo vai terminar. IsTerminating=" + eventArgs.IsTerminating);
        }
        catch
        {
            // Nada a fazer: o processo está já em fim de vida.
        }
    }
}
