using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System.Text;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public class ExportCTUIHelper
    {
        public void PrintExportImgInfo()
        {
            MessageBox.Show("Select a CT image to export to the deep learning model (for autocontouring)");
        }

        public void PopulateCTImageSets(List<StructureSet> structureSets, StructureSet selectedSS, StackPanel theSP)
        {
            ExportCTUIHelper helper = new ExportCTUIHelper();
            //needed to allow automatic selection of CT image for selected CT structure set (nothing will be selected if no structure set is selected)
            if (selectedSS != null)  structureSets.Insert(0, selectedSS);
            foreach (StructureSet itr in structureSets) theSP.Children.Add(helper.GetCTImageSets(theSP, itr.Image, itr == selectedSS ? true : false));
        }

        private StackPanel GetCTImageSets(StackPanel theSP, VMS.TPS.Common.Model.API.Image theImage, bool isFirst)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(25, 0, 5, 5);

            TextBox SID_TB = new TextBox();
            SID_TB.Text = theImage.Series.Id;
            SID_TB.HorizontalAlignment = HorizontalAlignment.Center;
            SID_TB.VerticalAlignment = VerticalAlignment.Center;
            SID_TB.Width = 80;
            SID_TB.FontSize = 14;
            SID_TB.Margin = new Thickness(0);

            TextBox CTID_TB = new TextBox();
            CTID_TB.Text = theImage.Id;
            CTID_TB.Name = "theTB";
            CTID_TB.HorizontalAlignment = HorizontalAlignment.Center;
            CTID_TB.VerticalAlignment = VerticalAlignment.Center;
            CTID_TB.Width = 160;
            CTID_TB.FontSize = 14;
            CTID_TB.Margin = new Thickness(25, 0, 0, 0);

            TextBox numImg_TB = new TextBox();
            numImg_TB.Text = theImage.ZSize.ToString();
            numImg_TB.HorizontalAlignment = HorizontalAlignment.Center;
            numImg_TB.VerticalAlignment = VerticalAlignment.Center;
            numImg_TB.Width = 40;
            numImg_TB.FontSize = 14;
            numImg_TB.HorizontalContentAlignment = HorizontalAlignment.Center;
            numImg_TB.Margin = new Thickness(25, 0, 0, 0);

            TextBox creation_TB = new TextBox();
            creation_TB.Text = theImage.HistoryDateTime.ToString("MM/dd/yyyy");
            creation_TB.HorizontalAlignment = HorizontalAlignment.Center;
            creation_TB.VerticalAlignment = VerticalAlignment.Center;
            creation_TB.Width = 90;
            creation_TB.FontSize = 14;
            creation_TB.HorizontalContentAlignment = HorizontalAlignment.Center;
            creation_TB.Margin = new Thickness(25, 0, 0, 0);

            CheckBox select_CB = new CheckBox();
            select_CB.Margin = new Thickness(30, 5, 0, 0);
            if (isFirst) select_CB.IsChecked = true;

            sp.Children.Add(SID_TB);
            sp.Children.Add(CTID_TB);
            sp.Children.Add(numImg_TB);
            sp.Children.Add(creation_TB);
            sp.Children.Add(select_CB);

            return sp;
        }

        public string ParseSelectedCTImage(StackPanel theSP)
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

        public (bool, StringBuilder) ExportImage(StackPanel theSP, List<StructureSet> structureSets, string Id, string imgExportPath, string format)
        {
            StringBuilder sb = new StringBuilder();
            ExportCTUIHelper helper = new ExportCTUIHelper();
            string selectedCTID = helper.ParseSelectedCTImage(theSP);
            if (!string.IsNullOrWhiteSpace(selectedCTID))
            {
                VMS.TPS.Common.Model.API.Image theImage = structureSets.FirstOrDefault(x => x.Image.Id == selectedCTID).Image;
                CTImageExport exporter = new CTImageExport(theImage, imgExportPath, Id, format);
                return exporter.ExportImage();
            }
            else return (true, sb.AppendLine("No imaged selected for export!"));
        }
    }
}
