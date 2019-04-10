using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.Windows.Forms;
using Microsoft.Win32;

using HardwareHelperLib;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace GCNUSBFeeder
{
    public class SystemHelper
    {
        public static event EventHandler<Driver.LogEventArgs> Log;
        #region Registry Functions
        public static RegistryKey registryRun = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        public static void addToStartup()
        {
            try
            {
                registryRun.SetValue("GCNAdapter", Application.ExecutablePath.ToString());
            }
            catch
            {
                Log(null, new Driver.LogEventArgs("Unable to set a startup object. (Are you running as administrator?"));
            }
        }

        public static void removeFromStartup()
        {
            try
            {
                registryRun.DeleteValue("GCNAdapter", false);
            }
            catch
            {

            }
        }

        public static bool isOnStartUp()
        {
            if (registryRun.GetValue("GCNAdapter") != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        #region Driver Functions
        public static void checkForMissingDrivers()
        {
            bool libUsb = false;
            try
            {
                SelectQuery query = new SelectQuery("Win32_SystemDriver");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                var drivers = searcher.Get();
                foreach (var d in drivers)
                {
                    if (d["Name"].ToString().Contains("libusb")) libUsb = true;
                    if (libUsb) break;
                }
            }
            catch
            {
                Log(null, new Driver.LogEventArgs("Driver check failed, (Are you running as Administrator?)"));
            }
        }
        #endregion
    }
}
