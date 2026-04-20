namespace DataImportExportManager.Contracts;

/// <summary>
/// Represents imported tabular data with extracted column names.
/// </summary>
public sealed record TabularImportResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TabularImportResult"/> class.
    /// </summary>
    /// <param name="columns">The extracted column names, usually from the first imported row.</param>
    /// <param name="rows">The full imported row set, including the header row when present.</param>
    public TabularImportResult(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        Columns = columns;
        Rows = rows;

        if (rows.Count <= 1)
        {
            DataRows = [];
            return;
        }

        var dataRows = new List<IReadOnlyList<string>>(rows.Count - 1);
        for (var i = 1; i < rows.Count; i++)
        {
            dataRows.Add(rows[i]);
        }

        DataRows = dataRows;
    }

    /// <summary>
    /// Gets the extracted column names, usually from the first imported row.
    /// </summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>
    /// Gets the full imported row set, including the header row when present.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    /// <summary>
    /// Gets imported data rows excluding the header row.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> DataRows { get; }

    /// <summary>
    /// Creates an empty import result.
    /// </summary>
    public static TabularImportResult Empty { get; } = new([], []);
}
