using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LibUsbDotNet;
using LibUsbDotNet.Main;

//using MadWizard.WinUSBNet;
using ScpDriverInterface;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;


namespace GCNUSBFeeder
{
    public class Driver
    {
        public static event EventHandler<LogEventArgs> Log;
        public static bool run = false;

        public static bool noEventMode = true;

        public static ControllerDeadZones gcn1DZ;
        public static ControllerDeadZones gcn2DZ;
        public static ControllerDeadZones gcn3DZ;
        public static ControllerDeadZones gcn4DZ;

        public static bool gcn1Enabled = false;
        public static bool gcn2Enabled = false;
        public static bool gcn3Enabled = false;
        public static bool gcn4Enabled = false;

        private bool gcn1ok = false;
        private bool gcn2ok = false;
        private bool gcn3ok = false;
        private bool gcn4ok = false;

        private long gcn1Ffb = 0;
        private long gcn2Ffb = 0;
        private long gcn3Ffb = 0;
        private long gcn4Ffb = 0;

        private bool gcn1FfbInf = false;
        private bool gcn2FfbInf = false;
        private bool gcn3FfbInf = false;
        private bool gcn4FfbInf = false;

        private bool gcn1FfbActive = false;
        private bool gcn2FfbActive = false;
        private bool gcn3FfbActive = false;
        private bool gcn4FfbActive = false;

        public Driver()
        {
            gcn1DZ = new ControllerDeadZones();
            gcn2DZ = new ControllerDeadZones();
            gcn3DZ = new ControllerDeadZones();
            gcn4DZ = new ControllerDeadZones();
        }

        UsbEndpointReader reader = null;
        UsbEndpointWriter writer = null;
        UsbDevice GCNAdapter = null;
        IUsbDevice wholeGCNAdapter = null;

        public void Start()
        {
            //WUP-028
            //VENDORID 0x57E
            //PRODUCT ID 0x337
            
            var USBFinder = new UsbDeviceFinder(0x057E, 0x0337);
            GCNAdapter = UsbDevice.OpenUsbDevice(USBFinder);

            if (GCNAdapter != null)
            {
                int transferLength;

                reader = GCNAdapter.OpenEndpointReader(ReadEndpointID.Ep01);
                writer = GCNAdapter.OpenEndpointWriter(WriteEndpointID.Ep02);

                //prompt controller to start sending
                writer.Write(Convert.ToByte((char)19), 10, out transferLength);

                try
                {
                    if (gcn1Enabled && !JoystickHelper.checkJoystick(1)) { JoystickHelper.CreateJoystick(1); }
                    if (gcn2Enabled && !JoystickHelper.checkJoystick(2)) { JoystickHelper.CreateJoystick(2); }
                    if (gcn3Enabled && !JoystickHelper.checkJoystick(3)) { JoystickHelper.CreateJoystick(3); }
                    if (gcn4Enabled && !JoystickHelper.checkJoystick(4)) { JoystickHelper.CreateJoystick(4); }

                    if (gcn1Enabled && JoystickHelper.Acquire(1))
                        gcn1ok = true;
                    if (gcn2Enabled && JoystickHelper.Acquire(2))
                        gcn2ok = true;
                    if (gcn3Enabled && JoystickHelper.Acquire(3))
                        gcn3ok = true;
                    if (gcn4Enabled && JoystickHelper.Acquire(4))
                        gcn4ok = true;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("HRESULT: 0x8007000B"))
                    {
                        Log(null, new LogEventArgs("Error: vJoy driver mismatch. Did you install the wrong version (x86/x64)?"));
                        Driver.run = false;
                        return;
                    }
                }

                if (noEventMode)
                {
                    // PORT 1: bytes 02-09
                    // PORT 2: bytes 11-17
                    // PORT 3: bytes 20-27
                    // PORT 4: bytes 29-36l
                    byte[] ReadBuffer = new byte[37]; // 32 (4 players x 8) bytes for input, 5 bytes for formatting
                    byte[] WriteBuffer = new byte[5]; // 1 for command, 4 for rumble state
                    WriteBuffer[0] = 0x11;
                    WriteBuffer[1] = 0;
                    Log(null, new LogEventArgs("Driver successfully started, entering input loop."));
                    run = true;
                    Stopwatch sw = Stopwatch.StartNew();
                    while (run)
                    {
                        var ec = reader.Read(ReadBuffer, 10, out transferLength);
                        var input1 = GCNState.GetState(getFastInput1(ref ReadBuffer));
                        var input2 = GCNState.GetState(getFastInput2(ref ReadBuffer));
                        var input3 = GCNState.GetState(getFastInput3(ref ReadBuffer));
                        var input4 = GCNState.GetState(getFastInput4(ref ReadBuffer));

                        if (gcn1ok) { JoystickHelper.setJoystick(input1, 1, gcn1DZ); }
                        if (gcn2ok) { JoystickHelper.setJoystick(input2, 2, gcn2DZ); }
                        if (gcn3ok) { JoystickHelper.setJoystick(input3, 3, gcn3DZ); }
                        if (gcn4ok) { JoystickHelper.setJoystick(input4, 4, gcn4DZ); }

                        long elapsed = sw.ElapsedMilliseconds;
                        sw.Reset(); sw.Start();

                        if (Interlocked.Read(ref gcn1Ffb) > 0)
                        {
                            if (gcn1FfbInf == false)
                                Interlocked.Add(ref gcn1Ffb, -elapsed);
                            WriteBuffer[1] = (byte)(gcn1FfbActive ? 1 : 0);
                        }
                        else
                            WriteBuffer[1] = 0;
                        if (Interlocked.Read(ref gcn2Ffb) > 0)
                        {
                            if (gcn2FfbInf == false)
                                Interlocked.Add(ref gcn2Ffb, -elapsed);
                            WriteBuffer[2] = (byte)(gcn2FfbActive ? 1 : 0);
                        }
                        else
                            WriteBuffer[2] = 0;
                        if (Interlocked.Read(ref gcn3Ffb) > 0)
                        {
                            if (gcn3FfbInf == false)
                                Interlocked.Add(ref gcn3Ffb, -elapsed);
                            WriteBuffer[3] = (byte)(gcn3FfbActive ? 1 : 0);
                        }
                        else
                            WriteBuffer[3] = 0;
                        if (Interlocked.Read(ref gcn4Ffb) > 0)
                        {
                            if (gcn4FfbInf == false)
                                Interlocked.Add(ref gcn4Ffb, -elapsed);
                            WriteBuffer[4] = (byte)(gcn4FfbActive ? 1 : 0);
                        }
                        else
                            WriteBuffer[4] = 0;
                        writer.Write(WriteBuffer, 10, out transferLength);
                        System.Threading.Thread.Sleep(5);
                    }

                    WriteBuffer[1] = 0; WriteBuffer[2] = 0; WriteBuffer[3] = 0; WriteBuffer[4] = 0;
                    writer.Write(WriteBuffer, 10, out transferLength);

                    if (GCNAdapter != null)
                    {
                        if (GCNAdapter.IsOpen)
                        {
                            if (!ReferenceEquals(wholeGCNAdapter, null))
                            {
                                wholeGCNAdapter.ReleaseInterface(0);
                            }
                            GCNAdapter.Close();
                        }
                        GCNAdapter = null;
                        UsbDevice.Exit();
                        Log(null, new LogEventArgs("Closing driver thread..."));
                    }
                    Log(null, new LogEventArgs("Driver thread has been stopped."));
                }
                else
                {
                    Log(null, new LogEventArgs("Driver successfully started, entering input loop."));
                    //using  Interrupt request instead of looping behavior.
                    reader.DataReceivedEnabled = true;
                    reader.DataReceived += reader_DataReceived;
                    reader.ReadBufferSize = 37;
                    reader.ReadThreadPriority = System.Threading.ThreadPriority.Highest;
                    run = true;
                }
            }
            else
            {
                Log(null, new LogEventArgs("GCN Adapter not detected."));
                Driver.run = false;
            }
        }

        #region input parsing
        //Ugly, but faster than linq, at the very least.
        private byte[] getFastInput1(ref byte[] input)
        {
            return new byte[] { input[1], input[2], input[3], input[4], input[5], input[6], input[7], input[8], input[9] };
        }
        private byte[] getFastInput2(ref byte[] input)
        {
            return new byte[] { input[10], input[11], input[12], input[13], input[14], input[15], input[16], input[17], input[18] };
        }
        private byte[] getFastInput3(ref byte[] input)
        {
            return new byte[] { input[19], input[20], input[21], input[22], input[23], input[24], input[25], input[26], input[27] };
        }
        private byte[] getFastInput4(ref byte[] input)
        {
            return new byte[] { input[28], input[29], input[30], input[31], input[32], input[33], input[34], input[35], input[36] };
        }
        #endregion

        public void reader_DataReceived(object sender, EndpointDataEventArgs e)
        {
            if (run)
            {
                var data = e.Buffer;
                var input1 = GCNState.GetState(getFastInput1(ref data));
                var input2 = GCNState.GetState(getFastInput2(ref data));
                var input3 = GCNState.GetState(getFastInput3(ref data));
                var input4 = GCNState.GetState(getFastInput4(ref data));

                if (gcn1ok) { JoystickHelper.setJoystick(input1, 1, gcn1DZ); }
                if (gcn2ok) { JoystickHelper.setJoystick(input2, 2, gcn2DZ); }
                if (gcn3ok) { JoystickHelper.setJoystick(input3, 3, gcn3DZ); }
                if (gcn4ok) { JoystickHelper.setJoystick(input4, 4, gcn4DZ); }
            }
            else
            {
                reader.DataReceivedEnabled = false;

                if (GCNAdapter != null)
                {
                    if (GCNAdapter.IsOpen)
                    {
                        if (!ReferenceEquals(wholeGCNAdapter, null))
                        {
                            wholeGCNAdapter.ReleaseInterface(0);
                        }
                        GCNAdapter.Close();
                    }
                    GCNAdapter = null;
                    UsbDevice.Exit();
                    Log(null, new LogEventArgs("Closing driver thread..."));
                }
                Log(null, new LogEventArgs("Driver thread has been stopped."));
            }
        }

        public class LogEventArgs : EventArgs
        {
            public LogEventArgs(string text = "")
            {
                _text = text;
            }

            private string _text;
            public string Text
            {
                get { return _text; }
                set { _text = value; }
            }
        }
    }
}
