namespace Task.Monitor.System.Services.Cpu;

internal static class CpuCoreMetricOrdering
{
    // PDH \Processor(*) instance names are the plain core index ("0".."63"); "_Total" is already
    // filtered out upstream. Compare numerically so 10 follows 9, not 1. Anything that doesn't
    // parse (a format change, a future \Processor Information "group,core" name) falls back to an
    // ordinal compare and sorts after the numeric names, keeping the result deterministic.
    public static int Compare(string left, string right)
    {
        bool leftIsInt  = int.TryParse(left,  out int leftValue);
        bool rightIsInt = int.TryParse(right, out int rightValue);

        if (leftIsInt && rightIsInt) {
            return leftValue.CompareTo(rightValue);
        }

        if (leftIsInt != rightIsInt) {
            return leftIsInt ? -1 : 1;
        }

        return string.CompareOrdinal(left, right);
    }
}
