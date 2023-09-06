using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class CropOverlapOARUIHelper
    {
        /// <summary>
        /// Helper method to print the Crop and Overlap structure header information on the Crop/Overlap subtab
        /// </summary>
        /// <returns></returns>
        public static StackPanel GetCropOverlapHeader()
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = 200,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(30, 0, 5, 5)
            };

            Label strName = new Label
            {
                Content = "OAR Id",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 100,
                FontSize = 14,
                Margin = new Thickness(35, 0, 0, 0)
            };
            sp.Children.Add(strName);
            return sp;
        }

        /// <summary>
        /// Helper method to add structures to the UIthat will be evaluated for crop and overlap operations
        /// </summary>
        /// <param name="allStructures"></param>
        /// <param name="OAR"></param>
        /// <param name="clearBtnPrefix"></param>
        /// <param name="clearSpareBtnCounter"></param>
        /// <param name="clearEvtHndl"></param>
        /// <param name="addOAREvenIfNotInSS"></param>
        /// <returns></returns>
        public static StackPanel AddCropOverlapOAR(List<string> allStructures, 
                                                   string OAR, 
                                                   string clearBtnPrefix, 
                                                   int clearSpareBtnCounter, 
                                                   RoutedEventHandler clearEvtHndl, 
                                                   bool addOAREvenIfNotInSS = false)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = 200,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(40, 0, 5, 5)
            };

            ComboBox str_cb = new ComboBox
            {
                Name = "str_cb",
                Width = 120,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0)
            };

            str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (string itr in allStructures)
            {
                str_cb.Items.Add(itr);
                if (itr.ToLower() == OAR.ToLower()) index = j;
                j++;
            }
            if (addOAREvenIfNotInSS && !allStructures.Any(x => string.Equals(x.ToLower(), OAR.ToLower())))
            {
                str_cb.Items.Add(OAR);
                str_cb.SelectedIndex = str_cb.Items.Count - 1;
            }
            else str_cb.SelectedIndex = index;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(str_cb);

            Button clearStructBtn = new Button
            {
                Name = clearBtnPrefix + clearSpareBtnCounter,
                Content = "Clear",
                Width = 50,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 5, 0, 0)
            };
            clearStructBtn.Click += clearEvtHndl;
            sp.Children.Add(clearStructBtn);

            return sp;
        }

        /// <summary>
        /// Helper method to read the selected structures in the UI for crop and overlap operations. Return a list of the structure Ids
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static (List<string>, StringBuilder) ParseCropOverlapOARList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<string> CropOverlapOARList = new List<string> { };
            string OAR = "";
            bool headerObj = true;
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                if (!headerObj)
                {
                    foreach (object obj1 in ((StackPanel)obj).Children)
                    {
                        if (obj1.GetType() == typeof(ComboBox))
                        {
                            //first combo box is the structure and the second is the sparing type
                            OAR = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                    }
                    if (OAR == "--select--")
                    {
                        sb.AppendLine("Error! \nOAR structure not selected for crop/overlap operation! \nSelect a structure and try again");
                        return (new List<string> { }, sb);
                    }
                    else CropOverlapOARList.Add(OAR);
                }
                else headerObj = false;
            }
            return (CropOverlapOARList, sb);
        }
    }
}
