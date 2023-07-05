using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using VMS.TPS.Common.Model.API;
using System.Text;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class ExportCTUIHelper
    {
        public static void PrintExportImgInfo()
        {
            MessageBox.Show("Select a CT image to export to the deep learning model (for autocontouring)");
        }

        public static void PopulateCTImageSets(List<StructureSet> structureSets, StructureSet selectedSS, StackPanel theSP)
        {
            //needed to allow automatic selection of CT image for selected CT structure set (nothing will be selected if no structure set is selected)
            if (selectedSS != null)  structureSets.Insert(0, selectedSS);
            foreach (StructureSet itr in structureSets) theSP.Children.Add(GetCTImageSets(theSP, itr.Image, itr == selectedSS ? true : false));
        }

        private static StackPanel GetCTImageSets(StackPanel theSP, VMS.TPS.Common.Model.API.Image theImage, bool isFirst)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(25, 0, 5, 5)
            };

            TextBox SID_TB = new TextBox
            {
                Text = theImage.Series.Id,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 80,
                FontSize = 14,
                Margin = new Thickness(0)
            };

            TextBox CTID_TB = new TextBox
            {
                Text = theImage.Id,
                Name = "theTB",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 160,
                FontSize = 14,
                Margin = new Thickness(25, 0, 0, 0)
            };

            TextBox numImg_TB = new TextBox
            {
                Text = theImage.ZSize.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 40,
                FontSize = 14,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(25, 0, 0, 0)
            };

            TextBox creation_TB = new TextBox
            {
                Text = theImage.HistoryDateTime.ToString("MM/dd/yyyy"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 90,
                FontSize = 14,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(25, 0, 0, 0)
            };

            CheckBox select_CB = new CheckBox
            {
                Margin = new Thickness(30, 5, 0, 0)
            };
            if (isFirst) select_CB.IsChecked = true;

            sp.Children.Add(SID_TB);
            sp.Children.Add(CTID_TB);
            sp.Children.Add(numImg_TB);
            sp.Children.Add(creation_TB);
            sp.Children.Add(select_CB);

            return sp;
        }

        public static string ParseSelectedCTImage(StackPanel theSP)
        {
            string theImageId = "";
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.GetType() == typeof(TextBox))
                    {
                        if ((obj1 as TextBox).Name == "theTB")
                        {
                            theImageId = (obj1 as TextBox).Text;
                        }
                    }
                    else if (obj1.GetType() == typeof(CheckBox))
                    {
                        if ((obj1 as CheckBox).IsChecked.Value) return theImageId;
                    }
                }
            }
            return theImageId;
        }

        public static VMS.TPS.Common.Model.API.Image GetSelectedImageForExport(StackPanel theSP, List<StructureSet> structureSets)
        {
            StringBuilder sb = new StringBuilder();
            string selectedCTID = ParseSelectedCTImage(theSP);
            VMS.TPS.Common.Model.API.Image theImage = null;
            if (!string.IsNullOrWhiteSpace(selectedCTID)) theImage = structureSets.FirstOrDefault(x => x.Image.Id == selectedCTID).Image;
            return (theImage);
        }
    }
}
