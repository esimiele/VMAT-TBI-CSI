using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using VMATTBICSIAutoPlanningHelpers.Logging;

namespace VMATTBICSIOptLoopMT.Prompts
{
    public partial class SelectPatient : Window
    {
        private string _patientMRN = "";
        private string _fullLogFileName = "";
        private string logPath = "";
        private List<string> logsCSI = new List<string> { };
        private List<string> logsTBI = new List<string> { };
        public bool selectionMade = false;
        public (string,string) GetPatientMRN()
        {
            return (_patientMRN,_fullLogFileName);
        }

        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<string> PatientMRNsCSI { get; set; }
        public ObservableCollection<string> PatientMRNsTBI { get; set; }
        public SelectPatient(string path)
        {
            InitializeComponent();
            logPath = path;
            DataContext = this;
            LoadPatientMRNsFromLogs();
        }

        private void LoadPatientMRNsFromLogs()
        {
            if(Directory.Exists(logPath + "\\preparation\\"))
            {
                if(Directory.Exists(logPath + "\\preparation\\CSI\\"))
                {
                    PatientMRNsCSI = new ObservableCollection<string>() { "--select--" };
                    logsCSI = new List<string>(Directory.GetDirectories(logPath + "\\preparation\\CSI\\", "*", SearchOption.TopDirectoryOnly).OrderByDescending(x => Directory.GetLastWriteTimeUtc(x)));
                    foreach (string itr in logsCSI)
                    {
                        if (Directory.GetFiles(itr, ".", SearchOption.TopDirectoryOnly).Any())
                        {
                            string CSILogFile = Directory.GetFiles(itr, ".", SearchOption.TopDirectoryOnly).First();
                            PatientMRNsCSI.Add(CSILogFile.Substring(itr.LastIndexOf("\\") + 1, CSILogFile.Length - CSILogFile.LastIndexOf("\\") - 1 - 4));
                        }
                    }
                }
                if (Directory.Exists(logPath + "\\preparation\\TBI\\"))
                {
                    PatientMRNsTBI = new ObservableCollection<string>() { "--select--" };
                    logsTBI = new List<string>(Directory.GetDirectories(logPath + "\\preparation\\TBI\\", ".", SearchOption.TopDirectoryOnly).OrderByDescending(x => File.GetLastWriteTimeUtc(x)));
                    foreach (string itr in logsTBI)
                    {
                        if (Directory.GetFiles(itr, ".", SearchOption.TopDirectoryOnly).Any())
                        {
                            string TBILogFile = Directory.GetFiles(itr, ".", SearchOption.TopDirectoryOnly).First();
                            PatientMRNsTBI.Add(TBILogFile.Substring(TBILogFile.LastIndexOf("\\") + 1, TBILogFile.Length - TBILogFile.LastIndexOf("\\") - 1 - 4));
                        }
                    }
                }
            }
            else
            {
                //implement file selection system to select folder
                MessageBox.Show($"Log file directory: {(logPath + "\\preparation\\")}\nDoes not exist! Please open an patient by manually entering an MRN.");
            }
        }

        private void OpenPatient_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(MRNTB.Text) || !string.IsNullOrEmpty(_patientMRN))
            {
                //give priority to the text box data
                if (string.IsNullOrEmpty(MRNTB.Text)) _fullLogFileName = LogHelper.GetFullLogFileFromExistingMRN(_patientMRN, logPath);
                else _patientMRN = MRNTB.Text;
                selectionMade = true;
            }
            this.Close();
        }

        private void mrnListCSI_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string temp = mrnListCSI.SelectedItem as string;
            if (string.IsNullOrEmpty(temp)) return;
            if (temp != "--select--")
            {
                mrnListTBI.UnselectAll();
                _patientMRN = mrnListCSI.SelectedItem as string;
                _fullLogFileName = logsCSI.FirstOrDefault(x => x.Contains(_patientMRN));
            }
            else
            {
                mrnListCSI.UnselectAll();
                _fullLogFileName = "";
                _patientMRN = "";
            }
        }

        private void mrnListTBI_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string temp = mrnListTBI.SelectedItem as string;
            if (string.IsNullOrEmpty(temp)) return;
            if (temp != "--select--")
            {
                mrnListCSI.UnselectAll();
                _patientMRN = mrnListTBI.SelectedItem as string;
                _fullLogFileName = logsTBI.FirstOrDefault(x => x.Contains(_patientMRN));
            }
            else
            {
                mrnListTBI.UnselectAll();
                _fullLogFileName = "";
                _patientMRN = "";
            }
        }
    }
}
