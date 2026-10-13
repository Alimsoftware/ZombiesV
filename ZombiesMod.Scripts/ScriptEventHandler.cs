using System;
using System.Collections.Generic;
using GTA;
using ZombiesMod.Static;

namespace ZombiesMod.Scripts;

public class ScriptEventHandler : SafeScript
{
	private readonly List<EventHandler> _wrapperEventHandlers;

	private readonly List<EventHandler> _scriptEventHandlers;

	private int _index;

	public static ScriptEventHandler Instance { get; private set; }

	public ScriptEventHandler()
	{
		Instance = this;
		_wrapperEventHandlers = new List<EventHandler>();
		_scriptEventHandlers = new List<EventHandler>();
		WrapEvents();
	}

	public void RegisterScript(EventHandler eventHandler)
	{
		_scriptEventHandlers.Add(eventHandler);
	}

	public void UnregisterScript(EventHandler eventHandler)
	{
		_scriptEventHandlers.Remove(eventHandler);
	}

	public void RegisterWrapper(EventHandler eventHandler)
	{
		_wrapperEventHandlers.Add(eventHandler);
	}

	public void UnregisterWrapper(EventHandler eventHandler)
	{
		_wrapperEventHandlers.Remove(eventHandler);
	}

	private void OnTick(object sender, EventArgs eventArgs)
	{
		UpdateWrappers(sender, eventArgs);
		UpdateScripts(sender, eventArgs);
	}

	private void UpdateScripts(object sender, EventArgs eventArgs)
	{
		// De trás para a frente, para que um handler que se desregistre a si próprio
		// durante a invocação não faça o índice saltar o seguinte.
		for (int num = _scriptEventHandlers.Count - 1; num >= 0; num--)
		{
			EventHandler handler = _scriptEventHandlers[num];
			if (handler == null)
			{
				continue;
			}

			// SafeExecutor por handler e não um só à volta do ciclo: um handler que
			// rebente não pode impedir os restantes de correrem, nem de se registarem
			// ou desregistarem a meio do frame.
			SafeExecutor.Run("ScriptEventHandler.Script#" + num, delegate
			{
				handler(sender, eventArgs);
			});
		}
	}

	private void UpdateWrappers(object sender, EventArgs eventArgs)
	{
		// Só 5 por frame, de propósito — é o que distribui a carga ao longo dos ticks.
		// Cada um vai no seu SafeExecutor: sem isso, a excepção do primeiro rebentava o
		// ciclo antes de _index += 5 e a rotação ficava presa no mesmo lote para sempre,
		// congelando os wrappers que ainda não tinham sido chamados nesse frame.
		int start = _index;
		int end = start + 5;

		for (int i = start; i < end && i < _wrapperEventHandlers.Count; i++)
		{
			EventHandler handler = _wrapperEventHandlers[i];
			if (handler == null)
			{
				continue;
			}

			SafeExecutor.Run("ScriptEventHandler.Wrapper#" + i, delegate
			{
				handler(sender, eventArgs);
			});
		}

		_index += 5;
		if (_index >= _wrapperEventHandlers.Count)
		{
			_index = 0;
		}
	}
}
