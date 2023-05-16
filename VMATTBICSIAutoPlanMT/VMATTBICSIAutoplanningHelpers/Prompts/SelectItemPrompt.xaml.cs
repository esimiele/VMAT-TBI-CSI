using System.Windows;
using System.Collections.Generic;

namespace VMATTBICSIAutoPlanningHelpers.Prompts
{
    public partial class SelectItemPrompt : Window
    {
        public bool GetSelection() { return confirm; }
        public string GetSelectedItem() { return selectedItem; }

        private bool confirm = false;
        private string selectedItem = "";

        public SelectItemPrompt(string message, List<string> items, string button1Content = "Confirm", string button2Content = "Cancel")
        {
            InitializeComponent();
            informationTB.Text = message;
            foreach (string itr in items) requestedItemCB.Items.Add(itr);
            requestedItemCB.SelectedIndex = 0;
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

        private void requestedItemCB_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            selectedItem = requestedItemCB.SelectedItem.ToString();
        }
    }
}
