using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Delegates
{
    public delegate void UpdateUI(int comp, string msg = "", bool f = false);

    //public static bool counttoonethousand(UpdateUI u)
    //{
    //    for(int i = 0; i < 1000; i++)
    //    {
    //        if (i % 10 == 0) u((i + 1) / 10, i.ToString());
    //        else u((i + 1) / 10);
    //        if(i == 487)
    //        {
    //            u((i + 1) / 10, $"Error! i is {i}", true);
    //        }
    //        Thread.Sleep(10);
    //    }
    //    return false;
    //}
}
