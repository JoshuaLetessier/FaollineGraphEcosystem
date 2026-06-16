namespace Faolline.GraphCore
{
    /// <summary>Comparison operators used by the numeric conditions (int, float). Canonical home in GraphCore;
    /// downstream libs reference this type (the per-lib copies were consolidated here).</summary>
    public enum ComparisonOperator
    {
        Equal          = 0,
        NotEqual       = 1,
        Less           = 2,
        LessOrEqual    = 3,
        Greater        = 4,
        GreaterOrEqual = 5
    }

    internal static class ComparisonOperatorExtensions
    {
        /// <summary>Maps the sign of a <c>CompareTo</c> result against an operator.</summary>
        public static bool Matches(this ComparisonOperator op, int comparison)
        {
            switch (op)
            {
                case ComparisonOperator.Equal:          return comparison == 0;
                case ComparisonOperator.NotEqual:       return comparison != 0;
                case ComparisonOperator.Less:           return comparison < 0;
                case ComparisonOperator.LessOrEqual:    return comparison <= 0;
                case ComparisonOperator.Greater:        return comparison > 0;
                case ComparisonOperator.GreaterOrEqual: return comparison >= 0;
                default:                                return false;
            }
        }
    }
}
