using System.Windows;

namespace VMATTBICSIAutoPlanningHelpers.Prompts
{
    public partial class EnterMissingInfoPrompt : Window
    {
        public bool GetSelection() { return confirm; }
        public string GetEnteredValue() { return value; }

        private bool confirm = false;
        private string value = "";

        public EnterMissingInfoPrompt(string message, string info, string button1Content = "Confirm", string button2Content = "Cancel")
        {
            InitializeComponent();
            informationTB.Text = message;
            requestedInfo.Content = info;
            Button1.Content = button1Content;
            Button2.Content = button2Content;
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            confirm = true;
            value = valueTB.Text;
            this.Close();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
