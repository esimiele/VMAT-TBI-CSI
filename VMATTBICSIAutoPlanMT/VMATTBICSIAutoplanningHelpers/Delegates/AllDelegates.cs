namespace VMATTBICSIAutoPlanningHelpers.Delegates
{
    /// <summary>
    /// Template delegate used for progress reporting when the operations is being performed in a separate helper class
    /// </summary>
    /// <param name="comp"></param>
    /// <param name="msg"></param>
    /// <param name="f"></param>
    public delegate void ProvideUIUpdateDelegate(int comp, string msg = "", bool f = false);
}
