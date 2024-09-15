using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VMATTBICSIOptLoopMT.Prompts
{
    /// <summary>
    /// Interaction logic for ReminderPrompt.xaml
    /// </summary>
    public partial class ReminderPrompt : Window
    {
        public bool ConfirmAll { get; private set; } = false;
        public ReminderPrompt(IEnumerable<string> reminders)
        {
            InitializeComponent();
            PopulateUI(reminders);
        }

        private void PopulateUI(IEnumerable<string> reminders)
        {
            StackPanel header = GetHeader();
            remindersSP.Children.Clear();
            remindersSP.Children.Add(header);
            foreach(string reminder in reminders)
            {
                remindersSP.Children.Add(GenerateReminderSP(reminder));
            }
        }

        private StackPanel GetHeader()
        {
            StackPanel sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };

            Label reminderName = new Label
            {
                Content = "Item",
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 275,
                FontSize = 14,
                Margin = new Thickness(5)
            };
            sp.Children.Add(reminderName);

            Label confirm = new Label
            {
                Content = "Confirm?",
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                FontSize = 14,
                Margin = new Thickness(5)
            };
            sp.Children.Add(confirm);
            return sp;
        }

        private StackPanel GenerateReminderSP(string reminder)
        {
            StackPanel sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };
            Label item = new Label
            {
                Content = reminder + "?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 300,
                FontSize = 14,
                Margin = new Thickness(5)
            };
            sp.Children.Add(item);
            CheckBox check = new CheckBox
            {
                Margin = new Thickness(5,13,5,0)
            };
            sp.Children.Add(check);
            return sp;
        }

        private List<bool> ParseItemsChecked(StackPanel sp)
        {
            bool headerObj = true;
            List<bool> itemChecks = new List<bool>();
            foreach (object obj in sp.Children)
            {
                if(!headerObj)
                {
                    foreach (object obj1 in ((StackPanel)obj).Children)
                    {
                        if(obj1.GetType() == typeof(CheckBox))
                        {
                            itemChecks.Add((bool)(obj1 as CheckBox).IsChecked);
                        }
                    }
                }
                else headerObj = false;
            }
            return itemChecks;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            List<bool> itemChecks = ParseItemsChecked(remindersSP);
            ConfirmAll = itemChecks.All(x => x == true);
        }

        private void ConfirmClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }
    }
}
