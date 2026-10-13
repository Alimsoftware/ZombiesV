using System;
using System.Collections.Generic;
using GTA;
using ZombiesMod.Scripts;

namespace ZombiesMod.Wrappers;

public class EntityEventWrapper
{
	public delegate void OnDeathEvent(EntityEventWrapper sender, Entity entity);

	public delegate void OnWrapperAbortedEvent(EntityEventWrapper sender, Entity entity);

	public delegate void OnWrapperUpdateEvent(EntityEventWrapper sender, Entity entity);

	public delegate void OnWrapperDisposedEvent(EntityEventWrapper sender, Entity entity);

	private static readonly List<EntityEventWrapper> Wrappers = new List<EntityEventWrapper>();

	private bool _isDead;

	private readonly EventHandler _scriptAbortedHandler;

	public Entity Entity { get; }

	public bool IsDead
	{
		get
		{
			return Entity.IsDead;
		}
		private set
		{
			if (value && !_isDead)
			{
				Died?.Invoke(this, Entity);
			}
			_isDead = value;
		}
	}

	public event OnDeathEvent Died;

	public event OnWrapperAbortedEvent Aborted;

	public event OnWrapperUpdateEvent Updated;

	public event OnWrapperDisposedEvent Disposed;

	public EntityEventWrapper(Entity ent)
	{
		Entity = ent;

		// Guardado numa field para poder ser desinscrever. Com o lambda anónimo que
		// havia aqui era impossível, e cada wrapper criado — zombie ou veículo — ficava
		// a arrastar o handler de Aborted até ao fim da sessão.
		_scriptAbortedHandler = delegate
		{
			Abort();
		};

		ScriptEventHandler.Instance.RegisterWrapper(OnTick);
		((Script)ScriptEventHandler.Instance).Aborted += _scriptAbortedHandler;
		Wrappers.Add(this);
	}

	public void OnTick(object sender, EventArgs eventArgs)
	{
		if (Entity == (Entity)null || !Entity.Exists())
		{
			Dispose();
			return;
		}
		IsDead = Entity.IsDead;
		Updated?.Invoke(this, Entity);
	}

	public void Abort()
	{
		Aborted?.Invoke(this, Entity);
	}

	public void Dispose()
	{
		ScriptEventHandler.Instance.UnregisterWrapper(OnTick);

		// Mesma razão do construtor: sem desinscrever aqui, cada wrapper ficava a
		// arrastar o handler de Aborted para sempre.
		ScriptEventHandler handler = ScriptEventHandler.Instance;
		if (handler != null)
		{
			((Script)handler).Aborted -= _scriptAbortedHandler;
		}

		Wrappers.Remove(this);
		Disposed?.Invoke(this, Entity);
	}

	public static void Dispose(Entity entity)
	{
		EntityEventWrapper entityEventWrapper = Wrappers.Find((EntityEventWrapper w) => w.Entity == entity);
		entityEventWrapper?.Dispose();
		Wrappers.Remove(entityEventWrapper);
	}
}
