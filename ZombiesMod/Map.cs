using System;
using System.Collections;
using System.Collections.Generic;
using GTA;

namespace ZombiesMod;

[Serializable]
public class Map : ICollection<MapProp>, IEnumerable<MapProp>, IEnumerable
{
	public delegate void OnListChangedEvent(int count);

	public List<MapProp> Props;

	public int Count => Props.Count;

	public bool IsReadOnly { get; }

	[field: NonSerialized]
	public event OnListChangedEvent ListChanged;

	public Map()
	{
		Props = new List<MapProp>();
		IsReadOnly = false;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public IEnumerator<MapProp> GetEnumerator()
	{
		return Props.GetEnumerator();
	}

	public void Add(MapProp item)
	{
		Props.Add(item);
		ListChanged?.Invoke(Props.Count);
	}

	public void Clear()
	{
		while (Props.Count > 0)
		{
			MapProp mapProp = Props[0];
			mapProp.Delete();
			Props.Remove(mapProp);
		}
	}

	public bool Contains(MapProp item)
	{
		return Props.Contains(item);
	}

	public void CopyTo(MapProp[] array, int arrayIndex)
	{
		Props.CopyTo(array, arrayIndex);
	}

	public bool Remove(MapProp item)
	{
		if (!Props.Remove(item))
		{
			return false;
		}
		ListChanged?.Invoke(Props.Count);
		return true;
	}

	public bool Contains(Prop prop)
	{
		return Props.Find((MapProp m) => m.Handle == ((Entity)prop).Handle) != null;
	}

	public void NotifyListChanged()
	{
		ListChanged?.Invoke(Count);
	}
}
