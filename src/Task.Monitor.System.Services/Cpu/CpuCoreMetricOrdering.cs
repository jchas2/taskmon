namespace Task.Monitor.System.Services.Cpu;

internal static class CpuCoreMetricOrdering
{
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
