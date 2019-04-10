using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScpDriverInterface;

namespace GCNUSBFeeder
{
    public class JoystickHelper
    {
        private enum FFB_T {
            CONST = 0x26,
            RAMP = 0x27,
            SQUR = 0x30,
            SINE = 0x31,
            TRNG = 0x32,
            STUP = 0x33,
            STDN = 0x34,
            SPRNG = 0x40,
            DMPR = 0x41,
            INTR = 0x42,
            FRIC = 0x43
        };

        private static ScpBus scp = new ScpBus();

        public static event EventHandler<Driver.LogEventArgs> Log;

        static X360Controller[] Controllers = new X360Controller[4];

        public static bool Acquire(int joystickID)
        {
            Controllers[joystickID - 1] = new X360Controller();
            return true;
        }

        public static bool CreateJoystick(int joystickID)
        {
            scp.PlugIn(joystickID);
            Controllers[joystickID - 1] = new X360Controller();
            return true;
        }

        public static bool DestroyJoystick(int joystickID)
        {
            scp.Unplug(joystickID);
            Controllers[joystickID - 1] = null;
            return true;
        }

        public static void DestroyAll()
        {
            scp.UnplugAll();
        }

        public static void setJoystick(GCNState input, int joystickID, ControllerDeadZones deadZones)
        {
            int multiplier = 302;
            X360Controller controller = Controllers[joystickID - 1];

            //32767
            //analog stick
            controller.LeftStickX = (short)(multiplier * (128 - input.analogX));
            controller.LeftStickY = (short)(multiplier * (128 - input.analogY));

            //c stick
            controller.RightStickX = (short)(multiplier * (128 - input.cstickX));
            controller.RightStickY = (short)(multiplier * (128 - input.cstickY));

            //triggers
            controller.LeftTrigger = (byte) input.analogL;
            controller.RightTrigger = (byte)input.analogR;

            controller.Buttons = 0;

            //dpad button mode for DDR pad support
            if (input.up)    controller.Buttons |= X360Buttons.Up;
            if (input.down)  controller.Buttons |= X360Buttons.Down;
            if (input.left)  controller.Buttons |= X360Buttons.Left;
            if (input.right) controller.Buttons |= X360Buttons.Right;

            //buttons
            if (input.A) controller.Buttons |= X360Buttons.A;
            if (input.B) controller.Buttons |= X360Buttons.X;
            if (input.X) controller.Buttons |= X360Buttons.B;
            if (input.Y) controller.Buttons |= X360Buttons.Y;
            if (input.Z) controller.Buttons |= X360Buttons.Logo;
            if (input.R) controller.Buttons |= X360Buttons.RightBumper;
            if (input.L) controller.Buttons |= X360Buttons.LeftBumper;
            if (input.start) controller.Buttons |= X360Buttons.Start;

            scp.Report(joystickID, controller.GetReport());
        }

        public static bool checkJoystick(int id)
        {
            bool checker = Controllers[id - 1] != null;
            if (checker)
            {
                Log(null, new Driver.LogEventArgs(string.Format("Port {0} is already owned by this feeder (OK).", id)));
            }
            return checker;
        }
    }
}
