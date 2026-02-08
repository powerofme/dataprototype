using System.Text;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Query;

public sealed class ComparisonQueryBuilder
{
    public (string sql, List<object> parameters) BuildComparisonQuery(
        string baseTableName,
        string compareTableName,
        List<string> keyColumns,
        List<string> valueColumns,
        double? threshold = null)
    {
        var sql = new StringBuilder();
        var parameters = new List<object>();

        sql.AppendLine("WITH base AS (");
        sql.AppendLine($"  SELECT * FROM {SanitizeIdentifier(baseTableName)}");
        sql.AppendLine("),");
        sql.AppendLine("compare AS (");
        sql.AppendLine($"  SELECT * FROM {SanitizeIdentifier(compareTableName)}");
        sql.AppendLine("),");
        sql.AppendLine("joined AS (");
        sql.Append("  SELECT ");

        var selectCols = new List<string>();

        foreach (var key in keyColumns)
        {
            var sanitizedKey = SanitizeIdentifier(key);
            selectCols.Add($"COALESCE(base.{sanitizedKey}, compare.{sanitizedKey}) AS {sanitizedKey}");
        }

        foreach (var valueCol in valueColumns)
        {
            var sanitizedCol = SanitizeIdentifier(valueCol);
            selectCols.Add($"base.{sanitizedCol} AS {sanitizedCol}_base");
            selectCols.Add($"compare.{sanitizedCol} AS {sanitizedCol}_compare");
            selectCols.Add($"compare.{sanitizedCol} - base.{sanitizedCol} AS {sanitizedCol}_delta");
            selectCols.Add($"CASE WHEN base.{sanitizedCol} != 0 THEN ((compare.{sanitizedCol} - base.{sanitizedCol}) / base.{sanitizedCol}) * 100 ELSE 0 END AS {sanitizedCol}_pct_change");
        }

        selectCols.Add("CASE WHEN base.rowid IS NULL THEN 'ADDED' WHEN compare.rowid IS NULL THEN 'DELETED' ELSE 'CHANGED' END AS \"_change_type\"");

        sql.AppendLine(string.Join(",\n    ", selectCols));
        sql.AppendLine("  FROM base");
        sql.Append("  FULL OUTER JOIN compare ON ");

        var joinConditions = keyColumns.Select(k =>
        {
            var sanitized = SanitizeIdentifier(k);
            return $"base.{sanitized} = compare.{sanitized}";
        });

        sql.AppendLine(string.Join(" AND ", joinConditions));
        sql.AppendLine(")");

        sql.Append("SELECT * FROM joined");

        if (threshold.HasValue && threshold.Value > 0)
        {
            sql.AppendLine();
            sql.Append("WHERE \"_change_type\" != 'CHANGED' OR (");

            var thresholdConditions = valueColumns.Select(col =>
            {
                var sanitizedCol = SanitizeIdentifier(col);
                parameters.Add(threshold.Value);
                return $"ABS({sanitizedCol}_delta) >= ?";
            });

            sql.Append(string.Join(" OR ", thresholdConditions));
            sql.Append(")");
        }

        return (sql.ToString(), parameters);
    }

    public string BuildDifferenceSummaryQuery(
        string comparisonTableName,
        List<string> groupByColumns)
    {
        var sql = new StringBuilder();

        sql.Append("SELECT \"_change_type\", ");

        if (groupByColumns.Count > 0)
        {
            sql.Append(string.Join(", ", groupByColumns.Select(c => SanitizeIdentifier(c))));
            sql.Append(", ");
        }

        sql.AppendLine("COUNT(*) AS count");
        sql.Append($"FROM {SanitizeIdentifier(comparisonTableName)}");

        if (groupByColumns.Count > 0)
        {
            sql.AppendLine();
            sql.Append("GROUP BY \"_change_type\", ");
            sql.Append(string.Join(", ", groupByColumns.Select(c => SanitizeIdentifier(c))));
        }
        else
        {
            sql.AppendLine();
            sql.Append("GROUP BY \"_change_type\"");
        }

        sql.AppendLine();
        sql.Append("ORDER BY \"_change_type\"");

        return sql.ToString();
    }

    public string BuildMultiExecutionComparisonQuery(
        List<string> tableNames,
        List<Guid> executionIds,
        List<string> keyColumns,
        List<string> valueColumns)
    {
        if (tableNames.Count != executionIds.Count || tableNames.Count < 2)
        {
            throw new ArgumentException("Must provide at least 2 tables with matching execution IDs");
        }

        var sql = new StringBuilder();

        for (int i = 0; i < tableNames.Count; i++)
        {
            var alias = $"exec_{i}";
            sql.AppendLine($"{alias} AS (");
            sql.AppendLine($"  SELECT *, '{executionIds[i]}' AS execution_id FROM {SanitizeIdentifier(tableNames[i])}");
            sql.Append(")");

            if (i < tableNames.Count - 1)
            {
                sql.AppendLine(",");
            }
        }

        sql.AppendLine();
        sql.Append("SELECT ");

        var selectCols = new List<string>();

        foreach (var key in keyColumns)
        {
            var sanitizedKey = SanitizeIdentifier(key);
            var coalesces = string.Join(", ", Enumerable.Range(0, tableNames.Count).Select(i => $"exec_{i}.{sanitizedKey}"));
            selectCols.Add($"COALESCE({coalesces}) AS {sanitizedKey}");
        }

        for (int i = 0; i < tableNames.Count; i++)
        {
            foreach (var valueCol in valueColumns)
            {
                var sanitizedCol = SanitizeIdentifier(valueCol);
                selectCols.Add($"exec_{i}.{sanitizedCol} AS {sanitizedCol}_exec_{i}");
            }
        }

        sql.AppendLine(string.Join(",\n  ", selectCols));
        sql.Append($"FROM exec_0");

        for (int i = 1; i < tableNames.Count; i++)
        {
            sql.AppendLine();
            sql.Append($"FULL OUTER JOIN exec_{i} ON ");

            var joinConditions = keyColumns.Select(k =>
            {
                var sanitized = SanitizeIdentifier(k);
                return $"exec_0.{sanitized} = exec_{i}.{sanitized}";
            });

            sql.Append(string.Join(" AND ", joinConditions));
        }

        return sql.ToString();
    }

    public (string sql, List<object> parameters) BuildSignificantDifferencesQuery(
        string comparisonTableName,
        List<string> valueColumns,
        double threshold)
    {
        var sql = new StringBuilder();
        var parameters = new List<object>();

        sql.Append($"SELECT * FROM {SanitizeIdentifier(comparisonTableName)} WHERE ");

        var conditions = valueColumns.Select(col =>
        {
            var sanitizedCol = SanitizeIdentifier(col);
            parameters.Add(threshold);
            return $"ABS({sanitizedCol}_delta) >= ?";
        });

        sql.Append(string.Join(" OR ", conditions));

        return (sql.ToString(), parameters);
    }

    public string BuildDifferencesByBookQuery(
        string comparisonTableName,
        string bookColumnName)
    {
        var sanitizedBook = SanitizeIdentifier(bookColumnName);
        var sanitizedTable = SanitizeIdentifier(comparisonTableName);

        return $@"
SELECT 
    {sanitizedBook}, 
    ""_change_type"", 
    COUNT(*) AS count 
FROM {sanitizedTable}
GROUP BY {sanitizedBook}, ""_change_type""
ORDER BY {sanitizedBook}, ""_change_type""";
    }

    private static string SanitizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be null or whitespace", nameof(identifier));
        }

        if (!identifier.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException($"Invalid identifier: {identifier}. Only alphanumeric characters and underscores are allowed.", nameof(identifier));
        }

        return $"\"{identifier}\"";
    }
}
