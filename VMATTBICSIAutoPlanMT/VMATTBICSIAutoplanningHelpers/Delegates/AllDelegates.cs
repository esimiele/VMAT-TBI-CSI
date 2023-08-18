namespace VMATTBICSIAutoPlanningHelpers.Delegates
{
    /// <summary>
    /// Template delegate used for progress reporting when the operations is being performed in a separate helper class
    /// </summary>
    /// <param name="comp"></param>
    /// <param name="msg"></param>
    /// <param name="f"></param>
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
