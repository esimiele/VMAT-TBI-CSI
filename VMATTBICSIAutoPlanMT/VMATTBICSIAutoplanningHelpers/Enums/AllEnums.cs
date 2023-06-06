namespace VMATTBICSIAutoPlanningHelpers.Enums
{
    public enum TSManipulationType
    {
        None,
        CropTargetFromStructure,
        ContourOverlapWithTarget,
        CropFromBody,
        ContourSubStructure,
        ContourOuterStructure
    };

    public enum OptimizationObjectiveType
    {
        None,
        Upper,
        Lower,
        Exact,
        Mean
    };

    public enum PlanType
    {
        VMAT_TBI,
        VMAT_CSI
    };
}
