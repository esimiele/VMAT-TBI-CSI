using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Helpers.Tests;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses
{
    public class RequestedOptTSStructureComparer : IEqualityComparer<RequestedOptimizationTSStructureModel>
    {

        public string Print(RequestedOptimizationTSStructureModel x)
        {
            string result;
            OptimizationConstraintComparer optComparer = new OptimizationConstraintComparer();
            OptTSCreationCriteriaComparer creationComparer = new OptTSCreationCriteriaComparer();
            if (x.GetType() == typeof(TSCoolerStructureModel))
            {
                result = $"{x.TSStructureId} {(x as TSCoolerStructureModel).UpperDoseValue}" + Environment.NewLine;
            }
            else
            {
                result = $"{x.TSStructureId} {(x as TSHeaterStructureModel).UpperDoseValue} {(x as TSHeaterStructureModel).LowerDoseValue}" + Environment.NewLine;
            }
            foreach (OptimizationConstraintModel itr in x.Constraints) result += $"{optComparer.Print(itr)}" + Environment.NewLine;
            foreach (OptTSCreationCriteriaModel itr in x.CreationCriteria) result += $"{creationComparer.Print(itr)}" + Environment.NewLine;
            return result;
        }

        public bool Equals(RequestedOptimizationTSStructureModel x, RequestedOptimizationTSStructureModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            OptimizationConstraintComparer optComparer = new OptimizationConstraintComparer();
            OptTSCreationCriteriaComparer creationComparer = new OptTSCreationCriteriaComparer();

            if (x.GetType() == typeof(TSCoolerStructureModel) && y.GetType() == typeof(TSCoolerStructureModel))
            {
                return string.Equals(x.TSStructureId, x.TSStructureId)
                    && CalculationHelper.AreEqual((x as TSCoolerStructureModel).UpperDoseValue, (y as TSCoolerStructureModel).UpperDoseValue)
                    && optComparer.Equals(x.Constraints, y.Constraints)
                    && creationComparer.Equals(x.CreationCriteria, y.CreationCriteria);
            }
            else if (x.GetType() == typeof(TSHeaterStructureModel) && y.GetType() == typeof(TSHeaterStructureModel))
            {
                return string.Equals(x.TSStructureId, x.TSStructureId)
                    && CalculationHelper.AreEqual((x as TSHeaterStructureModel).UpperDoseValue, (y as TSHeaterStructureModel).UpperDoseValue)
                    && CalculationHelper.AreEqual((x as TSHeaterStructureModel).LowerDoseValue, (y as TSHeaterStructureModel).LowerDoseValue)
                    && optComparer.Equals(x.Constraints, y.Constraints)
                    && creationComparer.Equals(x.CreationCriteria, y.CreationCriteria);
            }
            else return false;
        }

        public int GetHashCode(RequestedOptimizationTSStructureModel obj)
        {
            throw new NotImplementedException();
        }
    }
}
