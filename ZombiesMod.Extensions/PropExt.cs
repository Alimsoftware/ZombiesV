using GTA;
using GTA.Native;
using ZombiesMod.Static;

namespace ZombiesMod.Extensions;

public static class PropExt
{
	public static void SetStateOfDoor(this Prop prop, bool locked, DoorState heading)
	{
		InputArgument[] array = new InputArgument[7];
		Model model = ((Entity)prop).Model;
		array[0] = model.Hash;
		array[1] = ((Entity)prop).Position.X;
		array[2] = ((Entity)prop).Position.Y;
		array[3] = ((Entity)prop).Position.Z;
		array[4] = locked;
		array[5] = (int)heading;
		array[6] = 1;
		Function.Call(Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, array);
	}

	/// <summary>
	/// EXEMPLO — SafeExecutor.Run&lt;T&gt; à volta de uma leitura de native.
	///
	/// GET_STATE_OF_CLOSEST_DOOR_OF_TYPE escreve através de ponteiros; se a porta não
	/// existir ou o native falhar, antes a excepção subia e a ScriptDomain matava o
	/// script. Fica registado e devolve false (porta destrancada), que é o mesmo que o
	/// caller já trataba como caso possível.
	/// </summary>
	public unsafe static bool GetDoorLockState(this Prop prop)
	{
		return SafeExecutor.Run("PropExt.GetDoorLockState", () =>
		{
			bool result = false;
			int num = 0;
			InputArgument[] array = new InputArgument[6];
			Model model = ((Entity)prop).Model;
			array[0] = model.Hash;
			array[1] = ((Entity)prop).Position.X;
			array[2] = ((Entity)prop).Position.Y;
			array[3] = ((Entity)prop).Position.Z;
			array[4] = &result;
			array[5] = &num;
			Function.Call(Hash.GET_STATE_OF_CLOSEST_DOOR_OF_TYPE, array);
			return result;
		}, defaultValue: false);
	}
}
