using GTA;
using GTA.Math;

namespace ZombiesMod.Extensions;

public static class V3Extended
{
	public static bool IsOnScreen(this Vector3 vector3)
	{
		Vector3 position = GameplayCamera.Position;
		Vector3 direction = GameplayCamera.Direction;
		float fieldOfView = GameplayCamera.FieldOfView;
		Vector3 val = vector3 - position;
		float num = Vector3.Angle(val, direction);
		return num < fieldOfView;
	}
}
