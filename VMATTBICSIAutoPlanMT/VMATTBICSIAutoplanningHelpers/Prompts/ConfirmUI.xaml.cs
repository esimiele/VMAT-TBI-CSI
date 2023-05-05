using System.Windows;

namespace VMATTBICSIAutoPlanningHelpers.Prompts
{
    public partial class ConfirmUI : Window
    {
        public bool GetSelection() { return confirm; }
        bool confirm = false;

        public ConfirmUI(string message)
        {
            InitializeComponent();
            MessageTB.Text = message;
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            confirm = true;
            this.Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
