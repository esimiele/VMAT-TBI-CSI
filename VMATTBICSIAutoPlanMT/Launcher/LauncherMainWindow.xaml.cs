using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.IO;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Reflection;

namespace Launcher
{
    /// <summary>
    /// Interaction logic for LauncherMainWindow.xaml
    /// </summary>
    public partial class LauncherMainWindow : Window
    {
        bool isCSIPlan = false;
        string arguments = "";
        public LauncherMainWindow(List<string> startupArgs)
        {
            InitializeComponent();
            if (startupArgs.Any())
            {
                if (startupArgs.Count > 2) LaunchOptBtn.Visibility = Visibility.Visible;
                if (startupArgs.Count > 3) isCSIPlan = true;
                arguments = String.Format("{0} {1}", startupArgs.ElementAt(0), startupArgs.ElementAt(1));
            }
        }

        private void VMATTBIBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchExe("VMATTBIAutoPlanMT");
        }

        private void VMATCSIBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchExe("VMATCSIAutoPlanMT");
        }

        private void LaunchOptLoopBtn_Click(object sender, RoutedEventArgs e)
        {
            string exeName;
            if (isCSIPlan) exeName = "VMATCSIOptLoopMT";
            else exeName = "VMATTBIOptLoopMT";
            LaunchExe(exeName);
        }

        private void LaunchExe(string exeName)
        {
            string path = AppExePath(exeName);
            if (!string.IsNullOrEmpty(path))
            {
                ProcessStartInfo p = new ProcessStartInfo(path);
                p.Arguments = arguments;
                Process.Start(p);
                this.Close();
            }
            else MessageBox.Show(String.Format("Error! {0} executable NOT found!", exeName));
        }

        private string AppExePath(string exeName)
        {
            return FirstExePathIn(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), exeName);
        }

        private string FirstExePathIn(string dir, string exeName)
        {
            return Directory.GetFiles(dir, "*.exe").FirstOrDefault(x => x.Contains(exeName));
        }
    }
}
