using System.Windows;

namespace VMATTBICSIAutoPlanningHelpers.Prompts
{
    public partial class ConfirmPrompt : Window
    {
        public bool GetSelection() { return confirm; }

        bool confirm = false;

        public ConfirmPrompt(string message, string button1Content = "Confirm", string button2Content = "Cancel")
        {
            InitializeComponent();
            MessageTB.Text = message;
            Button1.Content = button1Content; 
            Button2.Content = button2Content; 
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            confirm = true;
            this.Close();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
