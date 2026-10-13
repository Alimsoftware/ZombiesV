using GTA;
using GTA.Native;
using GTA.Input;

namespace ZombiesMod.Extensions;

public static class GameExtended
{
	public static void DisableWeaponWheel()
	{
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)13);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)14);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)15);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)12);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)262);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)56);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)261);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)53);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)54);
		Controls.DisableControlActionThisFrame(ControlType.PlayerControl, (ControlAction)37);
	}

	public static int GetMobilePhoneId()
	{
		OutputArgument val = new OutputArgument();
		Function.Call(Hash.GET_MOBILE_PHONE_RENDER_ID, new InputArgument[1] { (InputArgument)val });
		return val.GetResult<int>();
	}
}
