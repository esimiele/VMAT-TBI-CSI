using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Logging;

namespace VMATTBICSIOptLoopMT.Prompts
{
    public partial class SelectPatient : Window
    {
        private string _patientMRN = "";
        private string _fullLogFileName = "";
        private PlanType _planType = PlanType.None;
        private string logPath = "";
        private List<string> logsCSI = new List<string> { };
        private List<string> logsTBI = new List<string> { };
        private List<string> planTypes = new List<string> { "--select--", "VMAT TBI", "VMAT CSI"};
        public bool selectionMade { get => _planType != PlanType.None; }
        public (string,PlanType,string) GetPatientSelection()
        {
            return (_patientMRN,_planType,_fullLogFileName);
        }

        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<string> PatientMRNsCSI { get; set; }
        public ObservableCollection<string> PatientMRNsTBI { get; set; }
        public SelectPatient(string path, string mrn = "")
        {
            InitializeComponent();
            logPath = path;
            DataContext = this;
            LoadPatientMRNsFromLogs();
            planTypeCB.Items.Clear();
            foreach(string s in planTypes) planTypeCB.Items.Add(s);
            planTypeCB.SelectedIndex = 0;
            if(!string.IsNullOrEmpty(mrn)) MRNTB.Text = mrn;
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
                if (string.IsNullOrEmpty(MRNTB.Text)) _fullLogFileName = LogHelper.GetFullLogFileFromExistingMRN(_patientMRN, logPath, _planType == PlanType.VMAT_CSI ? "CSI" : "TBI");
                else
                {
                    _patientMRN = MRNTB.Text;
                    if (planTypeCB.SelectedItem.ToString().Contains("TBI")) _planType = PlanType.VMAT_TBI;
                    else if (planTypeCB.SelectedItem.ToString().Contains("CSI")) _planType = PlanType.VMAT_CSI;
                    else _planType = PlanType.None;
                }
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
                _planType = PlanType.VMAT_CSI;
                MRNTB.Text = "";
            }
            else
            {
                mrnListCSI.UnselectAll();
                _fullLogFileName = "";
                _patientMRN = "";
                _planType = PlanType.None;
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
                _planType = PlanType.VMAT_TBI;
                MRNTB.Text = "";
            }
            else
            {
                mrnListTBI.UnselectAll();
                _fullLogFileName = "";
                _patientMRN = "";
                _planType = PlanType.None;
            }
        }
    }
}
