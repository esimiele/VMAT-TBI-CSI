using System.Windows;

namespace VMATTBICSIAutoPlanningHelpers.Prompts
{
    public partial class SaveChangesPrompt : Window
    {
        public bool GetSelection() { return save; }

        private bool save = false;

        public SaveChangesPrompt()
        {
            InitializeComponent();
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            save = true;
            this.Close();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
