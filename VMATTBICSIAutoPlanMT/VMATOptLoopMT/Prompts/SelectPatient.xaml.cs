using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace VMATTBICSIOptLoopMT.Prompts
{
    /// <summary>
    /// Interaction logic for SelectPatient.xaml
    /// </summary>
    public partial class SelectPatient : Window
    {
        private string _patientMRN = "";
        private string _fullLogFileName = "";
        private string logPath = "";
        List<string> logs = new List<string> { };
        public bool selectionMade = false;
        public (string,string) GetPatientMRN()
        {
            return (_patientMRN,_fullLogFileName);
        }

        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<string> PatientMRNs { get; set; }
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
                PatientMRNs = new ObservableCollection<string>() { "--select--" };
                logs = new List<string>(Directory.GetFiles(logPath + "\\preparation\\").OrderByDescending(x => File.GetLastWriteTimeUtc(x)));
                foreach (string itr in logs)
                {
                    PatientMRNs.Add(itr.Substring(itr.LastIndexOf("\\") + 1, itr.Length - itr.LastIndexOf("\\") - 1 - 4));
                }
            }
            else
            {
                //implement file selection system to select folder
                MessageBox.Show(String.Format("Log file directory: {0}\nDoes not exist! Please open an patient by manually entering an MRN.", logPath + "\\preparation\\"));
            }
        }

        private void OpenPatient_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(MRNTB.Text) && !string.IsNullOrEmpty(_patientMRN))
            {
                _patientMRN = MRNTB.Text;
                _fullLogFileName = Directory.GetFiles(logPath + "\\preparation\\").FirstOrDefault(x => x.Contains(_patientMRN));
            }
            selectionMade = true;
            this.Close();
        }

        private void mrnList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string temp = mrnList.SelectedItem as string;
            if (string.IsNullOrEmpty(temp)) return;
            if (temp != "--select--")
            {
                _patientMRN = mrnList.SelectedItem as string;
                _fullLogFileName = logs.FirstOrDefault(x => x.Contains(_patientMRN));
            }
            else
            {
                mrnList.UnselectAll();
                _fullLogFileName = "";
                _patientMRN = "";
            }
        }
    }
}
