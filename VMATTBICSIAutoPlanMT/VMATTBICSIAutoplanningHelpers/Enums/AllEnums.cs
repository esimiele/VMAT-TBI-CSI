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
        VMAT_CSI,
        None
    };

    /// <summary>
    /// flash type for vmat tbi. Either global or locally on one structure
    /// </summary>
    public enum FlashType
    {
        Global,
        Local
    };

    /// <summary>
    /// Enum for the operation being performed by the script. Used to ensure log files are not overwritten for important info when different operations are performed with the script
    /// </summary>
    public enum ScriptOperationType
    {
        General,
        ExportCT,
        ImportSS,
        GeneratePrelimTargets,
        PlanPrep,
        AutoConvertHighToDefaultRes
    };

    public enum Units
    {
        cGy,
        Gy,
        cc,
        Percent,
        None
    };

    public enum DVHMetric
    {
        Dmax,
        Dmin,
        Dmean,
        DoseAtVolume,
        VolumeAtDose,
        None
    };

    public enum InequalityOperator
    {
        GreaterThan,
        LessThan,
        GreaterThanOrEqualTo,
        LessThanOrEqualTo,
        Equal,
        None
    };
}
