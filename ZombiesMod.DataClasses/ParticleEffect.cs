using System.Drawing;
using GTA;
using GTA.Native;

namespace ZombiesMod.DataClasses;

public class ParticleEffect : IDeletable
{
	public int Handle { get; }

	public Color Color
	{
		set
		{
			Function.Call(Hash.SET_PARTICLE_FX_LOOPED_COLOUR, new InputArgument[5]
			{
				Handle,
				value.R,
				value.G,
				value.B,
				true
			});
		}
	}

	internal ParticleEffect(int handle)
	{
		Handle = handle;
	}

	public bool Exists()
	{
		return Function.Call<bool>(Hash.DOES_PARTICLE_FX_LOOPED_EXIST, new InputArgument[1] { Handle });
	}

	public void Delete()
	{
		Function.Call(Hash.REMOVE_PARTICLE_FX, new InputArgument[2]
		{
			Handle,
			1
		});
	}
}
