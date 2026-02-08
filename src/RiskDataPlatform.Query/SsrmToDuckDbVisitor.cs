using System.Text;
using System.Text.Json;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Query;

public sealed class SsrmToDuckDbVisitor
{
    private readonly List<object> _parameters = new();
    private int _parameterIndex = 0;

    public string BuildQuery(
        string tableName,
        SsrmRequest request,
        out List<object> parameters)
    {
        _parameters.Clear();
        _parameterIndex = 0;

        var sql = new StringBuilder();
        var isGrouped = request.RowGroupCols?.Count > 0 && request.GroupKeys?.Count < request.RowGroupCols.Count;

        if (isGrouped)
        {
            sql.Append(BuildGroupedQuery(tableName, request));
        }
        else
        {
            sql.Append(BuildFlatQuery(tableName, request));
        }

        parameters = new List<object>(_parameters);
        return sql.ToString();
    }

    private string BuildFlatQuery(string tableName, SsrmRequest request)
    {
        var sql = new StringBuilder();
        sql.Append("SELECT * FROM ");
        sql.Append(SanitizeIdentifier(tableName));

        if (request.FilterModel != null)
        {
            var whereClause = BuildWhereClause(request.FilterModel);
            if (!string.IsNullOrEmpty(whereClause))
            {
                sql.Append(" WHERE ");
                sql.Append(whereClause);
            }
        }

        if (request.SortModel?.Count > 0)
        {
            sql.Append(" ORDER BY ");
            sql.Append(BuildOrderByClause(request.SortModel));
        }

        sql.Append($" LIMIT {request.EndRow - request.StartRow} OFFSET {request.StartRow}");

        return sql.ToString();
    }

    private string BuildGroupedQuery(string tableName, SsrmRequest request)
    {
        var sql = new StringBuilder();
        var groupLevel = request.GroupKeys?.Count ?? 0;
        var rowGroupCols = request.RowGroupCols ?? new List<string>();
        var valueCols = request.ValueCols ?? new List<string>();

        sql.Append("SELECT ");

        var selectCols = new List<string>();
        
        for (int i = 0; i < groupLevel; i++)
        {
            selectCols.Add(SanitizeIdentifier(rowGroupCols[i]));
        }

        if (groupLevel < rowGroupCols.Count)
        {
            selectCols.Add(SanitizeIdentifier(rowGroupCols[groupLevel]));
        }

        foreach (var valueCol in valueCols)
        {
            selectCols.Add($"SUM({SanitizeIdentifier(valueCol)}) AS {SanitizeIdentifier(valueCol)}");
        }

        selectCols.Add("COUNT(*) AS \"_count\"");

        sql.Append(string.Join(", ", selectCols));
        sql.Append(" FROM ");
        sql.Append(SanitizeIdentifier(tableName));

        var whereClauses = new List<string>();

        if (request.GroupKeys != null && request.GroupKeys.Count > 0)
        {
            for (int i = 0; i < request.GroupKeys.Count; i++)
            {
                var colName = SanitizeIdentifier(rowGroupCols[i]);
                whereClauses.Add($"{colName} = ?");
                _parameters.Add(request.GroupKeys[i]);
                _parameterIndex++;
            }
        }

        if (request.FilterModel != null)
        {
            var filterClause = BuildWhereClause(request.FilterModel);
            if (!string.IsNullOrEmpty(filterClause))
            {
                whereClauses.Add(filterClause);
            }
        }

        if (whereClauses.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", whereClauses));
        }

        sql.Append(" GROUP BY ");
        var groupByCols = new List<string>();
        for (int i = 0; i <= groupLevel && i < rowGroupCols.Count; i++)
        {
            groupByCols.Add(SanitizeIdentifier(rowGroupCols[i]));
        }
        sql.Append(string.Join(", ", groupByCols));

        if (request.SortModel?.Count > 0)
        {
            sql.Append(" ORDER BY ");
            sql.Append(BuildOrderByClause(request.SortModel));
        }

        sql.Append($" LIMIT {request.EndRow - request.StartRow} OFFSET {request.StartRow}");

        return sql.ToString();
    }

    private string BuildWhereClause(object filterModel)
    {
        if (filterModel == null)
        {
            return string.Empty;
        }

        var filters = new List<string>();

        if (filterModel is JsonElement jsonElement)
        {
            foreach (var property in jsonElement.EnumerateObject())
            {
                var columnName = property.Name;
                var filterDef = property.Value;

                var filterClause = BuildColumnFilter(columnName, filterDef);
                if (!string.IsNullOrEmpty(filterClause))
                {
                    filters.Add(filterClause);
                }
            }
        }
        else if (filterModel is Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                var filterClause = BuildColumnFilterFromObject(kvp.Key, kvp.Value);
                if (!string.IsNullOrEmpty(filterClause))
                {
                    filters.Add(filterClause);
                }
            }
        }

        return filters.Count > 0 ? string.Join(" AND ", filters) : string.Empty;
    }

    private string BuildColumnFilter(string columnName, JsonElement filterDef)
    {
        if (!filterDef.TryGetProperty("filterType", out var filterTypeElement))
        {
            return string.Empty;
        }

        var filterType = filterTypeElement.GetString();
        var colName = SanitizeIdentifier(columnName);

        switch (filterType)
        {
            case "text":
                return BuildTextFilter(colName, filterDef);
            case "number":
                return BuildNumberFilter(colName, filterDef);
            case "date":
                return BuildDateFilter(colName, filterDef);
            case "set":
                return BuildSetFilter(colName, filterDef);
            default:
                return string.Empty;
        }
    }

    private string BuildColumnFilterFromObject(string columnName, object filterDef)
    {
        var json = JsonSerializer.Serialize(filterDef);
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
        return BuildColumnFilter(columnName, jsonElement);
    }

    private string BuildTextFilter(string colName, JsonElement filterDef)
    {
        if (!filterDef.TryGetProperty("type", out var typeElement))
        {
            return string.Empty;
        }

        var type = typeElement.GetString();
        var filter = filterDef.TryGetProperty("filter", out var filterElement) ? filterElement.GetString() : null;

        if (string.IsNullOrEmpty(filter))
        {
            return string.Empty;
        }

        _parameters.Add(filter);
        _parameterIndex++;

        return type switch
        {
            "equals" => $"{colName} = ?",
            "notEqual" => $"{colName} != ?",
            "contains" => $"{colName} LIKE '%' || ? || '%'",
            "notContains" => $"{colName} NOT LIKE '%' || ? || '%'",
            "startsWith" => $"{colName} LIKE ? || '%'",
            "endsWith" => $"{colName} LIKE '%' || ?",
            _ => string.Empty
        };
    }

    private string BuildNumberFilter(string colName, JsonElement filterDef)
    {
        if (!filterDef.TryGetProperty("type", out var typeElement))
        {
            return string.Empty;
        }

        var type = typeElement.GetString();
        
        if (!filterDef.TryGetProperty("filter", out var filterElement))
        {
            return string.Empty;
        }

        var filter = filterElement.GetDouble();
        _parameters.Add(filter);
        _parameterIndex++;

        return type switch
        {
            "equals" => $"{colName} = ?",
            "notEqual" => $"{colName} != ?",
            "lessThan" => $"{colName} < ?",
            "lessThanOrEqual" => $"{colName} <= ?",
            "greaterThan" => $"{colName} > ?",
            "greaterThanOrEqual" => $"{colName} >= ?",
            "inRange" => BuildInRangeFilter(colName, filterDef),
            _ => string.Empty
        };
    }

    private string BuildInRangeFilter(string colName, JsonElement filterDef)
    {
        if (!filterDef.TryGetProperty("filter", out var filterElement) || 
            !filterDef.TryGetProperty("filterTo", out var filterToElement))
        {
            return string.Empty;
        }

        var from = filterElement.GetDouble();
        var to = filterToElement.GetDouble();

        _parameters.Add(from);
        _parameters.Add(to);
        _parameterIndex += 2;

        return $"{colName} BETWEEN ? AND ?";
    }

    private string BuildDateFilter(string colName, JsonElement filterDef)
    {
        if (!filterDef.TryGetProperty("type", out var typeElement))
        {
            return string.Empty;
        }

        var type = typeElement.GetString();

        if (!filterDef.TryGetProperty("dateFrom", out var dateFromElement))
        {
            return string.Empty;
        }

        var dateFrom = dateFromElement.GetString();
        if (string.IsNullOrEmpty(dateFrom))
        {
            return string.Empty;
        }

        _parameters.Add(dateFrom);
        _parameterIndex++;

        return type switch
        {
            "equals" => $"{colName} = ?",
            "notEqual" => $"{colName} != ?",
            "lessThan" => $"{colName} < ?",
            "greaterThan" => $"{colName} > ?",
            "inRange" => BuildDateRangeFilter(colName, filterDef),
            _ => string.Empty
        };
    }

    private string BuildDateRangeFilter(string colName, JsonElement filterDef)
    {
        if (!filterDef.TryGetProperty("dateFrom", out var dateFromElement) ||
            !filterDef.TryGetProperty("dateTo", out var dateToElement))
        {
            return string.Empty;
        }

        var dateFrom = dateFromElement.GetString();
        var dateTo = dateToElement.GetString();

        if (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo))
        {
            return string.Empty;
        }

        _parameters.Add(dateFrom);
        _parameters.Add(dateTo);
        _parameterIndex += 2;

        return $"{colName} BETWEEN ? AND ?";
    }

    private string BuildSetFilter(string colName, JsonElement filterDef)
    {
        if (!filterDef.TryGetProperty("values", out var valuesElement))
        {
            return string.Empty;
        }

        var values = new List<string>();
        foreach (var value in valuesElement.EnumerateArray())
        {
            var val = value.GetString();
            if (!string.IsNullOrEmpty(val))
            {
                values.Add(val);
                _parameters.Add(val);
                _parameterIndex++;
            }
        }

        if (values.Count == 0)
        {
            return string.Empty;
        }

        var placeholders = string.Join(", ", Enumerable.Repeat("?", values.Count));
        return $"{colName} IN ({placeholders})";
    }

    private string BuildOrderByClause(List<object> sortModel)
    {
        var sorts = new List<string>();

        foreach (var sortObj in sortModel)
        {
            if (sortObj is JsonElement jsonElement)
            {
                if (jsonElement.TryGetProperty("colId", out var colIdElement) &&
                    jsonElement.TryGetProperty("sort", out var sortElement))
                {
                    var colId = colIdElement.GetString();
                    var sort = sortElement.GetString();

                    if (!string.IsNullOrEmpty(colId) && !string.IsNullOrEmpty(sort))
                    {
                        var colName = SanitizeIdentifier(colId);
                        var direction = sort.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
                        sorts.Add($"{colName} {direction}");
                    }
                }
            }
            else if (sortObj is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("colId", out var colIdObj) &&
                    dict.TryGetValue("sort", out var sortObj2))
                {
                    var colId = colIdObj?.ToString();
                    var sort = sortObj2?.ToString();

                    if (!string.IsNullOrEmpty(colId) && !string.IsNullOrEmpty(sort))
                    {
                        var colName = SanitizeIdentifier(colId);
                        var direction = sort.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
                        sorts.Add($"{colName} {direction}");
                    }
                }
            }
        }

        return string.Join(", ", sorts);
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
