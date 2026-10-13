using GTA.Math;
using GTA.Native;

namespace ZombiesMod.Extensions;

public static class SystemExtended
{
	public static float VDist(this Vector3 v, Vector3 to)
	{
		return Function.Call<float>(Hash.VDIST, new InputArgument[6]
		{
			v.X,
			v.Y,
			v.Z,
			to.X,
			to.Y,
			to.Z
		});
	}
}
