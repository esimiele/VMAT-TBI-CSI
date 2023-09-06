using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using VMS.TPS.Common.Model.API;
using System.Text;
using Image = VMS.TPS.Common.Model.API.Image;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class ExportCTUIHelper
    {
        /// <summary>
        /// Simple method to print an info message about selecting a CT image for export
        /// </summary>
        public static void PrintExportImgInfo()
        {
            MessageBox.Show("Select a CT image to export to the deep learning model (for autocontouring)");
        }

        /// <summary>
        /// Helper method to add all CT images to the UI for the user to select which image they want to export for auto contouring
        /// </summary>
        /// <param name="structureSets"></param>
        /// <param name="selectedSS"></param>
        /// <param name="theSP"></param>
        public static void PopulateCTImageSets(List<Image> CTImages, Image selectedImage, StackPanel theSP)
        {
            //needed to allow automatic selection of CT image for selected CT structure set (nothing will be selected if no structure set is selected)
            if (selectedImage != null)  CTImages.Insert(0, selectedImage);
            foreach (Image itr in CTImages) theSP.Children.Add(AddCTImageSetToUI(theSP, itr, (itr == selectedImage || CTImages.Count == 1) ? true : false));
        }

        /// <summary>
        /// Helper method to add a CT image to the UI with a check box next to it
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="theImage"></param>
        /// <param name="isFirst"></param>
        /// <returns></returns>
        private static StackPanel AddCTImageSetToUI(StackPanel theSP, Image theImage, bool isFirst)
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
                HorizontalContentAlignment = HorizontalAlignment.Center,
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
                HorizontalContentAlignment = HorizontalAlignment.Center,
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

        /// <summary>
        /// Helper method to read through the list of CT images in the UI and determine the last image that had the checkbox selected
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static string ParseSelectedCTImage(StackPanel theSP)
        {
            string theImageId = "";
            foreach (object obj in theSP.Children)
            {
                string tmpImageId = "";
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.GetType() == typeof(TextBox))
                    {
                        if ((obj1 as TextBox).Name == "theTB")
                        {
                            tmpImageId = (obj1 as TextBox).Text;
                        }
                    }
                    else if (obj1.GetType() == typeof(CheckBox))
                    {
                        if ((obj1 as CheckBox).IsChecked.Value)
                        {
                            theImageId = tmpImageId;
                        }
                    }
                }
            }
            return theImageId;
        }

        /// <summary>
        /// Helper method to retrieve the CT image that was selected for export to auto contouring
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="structureSets"></param>
        /// <returns></returns>
        public static Image GetSelectedImageForExport(StackPanel theSP, List<Image> CTImages)
        {
            StringBuilder sb = new StringBuilder();
            string selectedCTID = ParseSelectedCTImage(theSP);
            Image theImage = null;
            if (!string.IsNullOrWhiteSpace(selectedCTID)) theImage = CTImages.FirstOrDefault(x => string.Equals(x.Id, selectedCTID));
            return theImage;
        }

        /// <summary>
        /// Helper method to return a list of all CT images for the patient
        /// </summary>
        /// <param name="pi"></param>
        /// <returns></returns>
        public static List<Image> GetAllCTImagesForPatient(Patient pi)
        {
            return pi.Studies.SelectMany(x => x.Series).Where(x => x.Modality == SeriesModality.CT).SelectMany(x => x.Images).Where(x => x.ZSize > 1).ToList();
        }
    }
}
