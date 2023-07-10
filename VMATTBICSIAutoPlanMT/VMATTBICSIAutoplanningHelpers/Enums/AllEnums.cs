namespace VMATTBICSIAutoPlanningHelpers.Enums
{
    /// <summary>
    /// Tuning structure manipulation type as an enum
    /// </summary>
    public enum TSManipulationType
    {
        None,
        CropTargetFromStructure,
        ContourOverlapWithTarget,
        CropFromBody,
        ContourSubStructure,
        ContourOuterStructure
    };

    /// <summary>
    /// Optimization objective type as an enum. Created own copy to include mean in list
    /// </summary>
    public enum OptimizationObjectiveType
    {
        None,
        Upper,
        Lower,
        Exact,
        Mean
    };

    /// <summary>
    /// CT image export format. Either dicom or png
    /// </summary>
    public enum ImgExportFormat
    {
        DICOM,
        PNG
    };

    /// <summary>
    /// Plan type, either csi or tbi
    /// </summary>
    public enum PlanType
    {
        VMAT_TBI,
        VMAT_CSI
    };

    /// <summary>
    /// flash type for vmat tbi. Either global or locally on one structure
    /// </summary>
    public enum FlashType
    {
        Global,
        Local
    };

    public enum ScriptOperationType
    {
        General,
        ExportCT,
        GeneratePrelimTargets,
        PlanPrep
    };
}
