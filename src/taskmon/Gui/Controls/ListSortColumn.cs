namespace Task.Monitor.Gui.Controls;

internal sealed record ListSortColumn<T>(string Title, int ColumnIndex, Comparison<T> Compare, Func<T, bool> IsBlank)
{
    public static ListSortColumn<T> ByText(string title, int columnIndex, Func<T, string?> key) =>
        new(title,
            columnIndex,
            (left, right) => string.Compare(key(left), key(right), StringComparison.CurrentCultureIgnoreCase),
            item => string.IsNullOrWhiteSpace(key(item)));

    public static ListSortColumn<T> ByValue<TKey>(string title, int columnIndex, Func<T, TKey?> key)
        where TKey : struct =>
        new(title,
            columnIndex,
            (left, right) => Nullable.Compare(key(left), key(right)),
            item => key(item) is null);

    public static ListSortColumn<T> ByVersion(string title, int columnIndex, Func<T, string?> key) =>
        new(title,
            columnIndex,
            (left, right) => CompareVersions(key(left), key(right)),
            item => ParseVersion(key(item)) is null);

    // Versions with suffixes, such as 6.0.0d8, compare on their numeric part first.
    private static int CompareVersions(string? left, string? right)
    {
        int byNumber = Comparer<Version>.Default.Compare(ParseVersion(left), ParseVersion(right));

        return byNumber != 0
            ? byNumber
            : string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
    }

    private static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) {
            return null;
        }

        int length = 0;

        while (length < text.Length && (char.IsAsciiDigit(text[length]) || text[length] == '.')) {
            length++;
        }

        string[] parts = text[..length].Split('.', StringSplitOptions.RemoveEmptyEntries);
        int[]    numbers = new int[Math.Min(parts.Length, 4)];

        for (int i = 0; i < numbers.Length; i++) {
            if (!int.TryParse(parts[i], out numbers[i])) {
                return null;
            }
        }

        return numbers.Length switch {
            0 => null,
            1 => new Version(numbers[0], 0),
            2 => new Version(numbers[0], numbers[1]),
            3 => new Version(numbers[0], numbers[1], numbers[2]),
            _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3])
        };
    }
}
