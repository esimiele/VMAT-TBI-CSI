using System.Windows.Controls;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class GeneralUIHelper
    {
        //common to both structure tuning and optimization setup tabs
        public static bool ClearRow(object sender, StackPanel sp)
        {
            //same deal as the clear sparing structure button (clearStructBtn_click)
            Button btn = (Button)sender;
            int i = 0;
            int k = 0;
            foreach (object obj in sp.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.Equals(btn)) k = i;
                }
                if (k > 0) break;
                i++;
            }

            //clear entire list if there are only two entries (header + 1 real entry)
            if (sp.Children.Count < 3) { return true; }
            else sp.Children.RemoveAt(k);
            return false;
        }

        public static void ClearList(StackPanel theSP)
        {
            theSP.Children.Clear();
        }
    }
}
