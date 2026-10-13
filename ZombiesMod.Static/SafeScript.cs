using System;
using System.Reflection;
using System.Windows.Forms;
using GTA;

namespace ZombiesMod.Static;

/// <summary>
/// Base opcional para os scripts do mod.
///
/// Substitui <c>GTA.Script</c> e religa os handlers OnTick / OnKeyDown / OnKeyUp do
/// script através do <see cref="SafeExecutor"/>, para que uma excepção derrube apenas
/// esse frame e não o script inteiro.
///
/// Uso num script existente — só duas linhas mudam:
/// <code>
///     public class Foo : SafeScript          // antes: : Script
///     {
///         public Foo()
///         {
///             WrapEvents();                  // antes: Tick += OnTick;
///         }
///
///         private void OnTick(object sender, EventArgs e) { ... }
///     }
/// </code>
///
/// Os OnTick continuam <c>private</c> como estavam: a base não os chama por nome em
/// código, localiza-os por reflexão uma única vez no <see cref="WrapEvents"/>.
/// </summary>
public abstract class SafeScript : Script
{
    /// <summary>
    /// <c>NonPublic</c> porque os handlers do mod são todos <c>private</c>. A reflexão
    /// faz-se uma vez por script, no construtor — irrelevante ao lado de um frame do GTA.
    /// </summary>
    private const BindingFlags HandlerFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Assinatura de <c>(object sender, EventArgs e)</c>, a do Tick.</summary>
    private static readonly Type[] EventSignature = { typeof(object), typeof(EventArgs) };

    /// <summary>Assinatura de <c>(object sender, KeyEventArgs e)</c>, a das teclas.</summary>
    private static readonly Type[] KeySignature = { typeof(object), typeof(KeyEventArgs) };

    protected SafeScript()
    {
        // Regista o handler global da AppDomain. Seguro aqui: faz-se antes do script
        // filho ter tido chance de falhar, e o InstallGlobalHandler é idempotente.
        SafeExecutor.InstallGlobalHandler();
    }

    /// <summary>
    /// Substitui as subscrições directas por versões envolvidas no <see cref="SafeExecutor"/>.
    /// Chamar no construtor do script, no sítio onde estava <c>Tick += OnTick;</c>.
    ///
    /// Só religa os handlers que o script tiver de facto — a maioria só usa OnTick, e o
    /// <c>OnKeyDown</c> inexistente é ignorado em silêncio.
    /// </summary>
    protected void WrapEvents()
    {
        Type self = GetType();
        string context = self.Name;

        EventHandler tick = FindHandler<EventHandler>(self, "OnTick", EventSignature);
        if (tick != null)
        {
            // Remover antes de adicionar: se o script deixou a linha antiga por engano,
            // o OnTick correria duas vezes por frame e ninguém percebia.
            Tick -= tick;
            Tick += (sender, e) => SafeExecutor.Run(context + ".Tick", () => tick(sender, e));
        }

        KeyEventHandler keyDown = FindHandler<KeyEventHandler>(self, "OnKeyDown", KeySignature);
        if (keyDown != null)
        {
            KeyDown -= keyDown;
            KeyDown += (sender, e) => SafeExecutor.Run(context + ".KeyDown", () => keyDown(sender, e));
        }

        KeyEventHandler keyUp = FindHandler<KeyEventHandler>(self, "OnKeyUp", KeySignature);
        if (keyUp != null)
        {
            KeyUp -= keyUp;
            KeyUp += (sender, e) => SafeExecutor.Run(context + ".KeyUp", () => keyUp(sender, e));
        }
    }

    private THandler FindHandler<THandler>(Type self, string name, Type[] parameterTypes)
        where THandler : Delegate
    {
        MethodInfo method = self.GetMethod(name, HandlerFlags, null, parameterTypes, null);

        if (method == null)
        {
            // Caso normal: o script não tem esse handler.
            return null;
        }

        // CreateDelegate aceita também métodos estáticos (ignora o primeiro argumento),
        // o que cobre scripts como o RecruitPeds que declaram OnTick como private static.
        return (THandler)Delegate.CreateDelegate(typeof(THandler), this, method);
    }
}
