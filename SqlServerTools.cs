using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using McpSqlServerPersonal.Security;

namespace McpSqlServerPersonal;

/// <summary>
/// MCP tools for SQL Server read-only operations.
/// </summary>
[McpServerToolType]
public sealed class SqlServerTools(SqlConnectionFactory connectionFactory, ServerConfig config, ResultSetSanitizer sanitizer)
{
    [McpServerTool, Description("List accessible base tables in the database.")]
    public async Task<string> ListTables(string? schema = null)
    {
        const string sql = @"
            SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
              AND (@schema IS NULL OR TABLE_SCHEMA = @schema)
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

        try
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection)
            {
                CommandTimeout = config.CommandTimeoutSeconds,
            };

            var schemaParam = command.Parameters.Add("@schema", SqlDbType.NVarChar, 128);
            schemaParam.Value = (object?)schema ?? DBNull.Value;

            using var reader = await command.ExecuteReaderAsync();

            var rows = new List<object[]>();
            int count = 0;
            bool truncated = false;

            while (await reader.ReadAsync())
            {
                if (count >= config.MaxRows)
                {
                    truncated = true;
                    break;
                }

                rows.Add(new object[]
                {
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                });
                count++;
            }

            return JsonSerializer.Serialize(new
            {
                columns = new[] { "schema", "table", "type" },
                rows,
                rowCount = count,
                truncated,
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                columns = Array.Empty<string>(),
                rows = Array.Empty<object[]>(),
                rowCount = 0,
                truncated = false,
                errors = new[] { ex.Message },
            });
        }
    }

    [McpServerTool, Description("Describe the columns and metadata of a single table.")]
    public async Task<string> DescribeTable(string schema, string table)
    {
        const string sql = @"
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                    AND tc.TABLE_NAME = ku.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk
                ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA
                AND c.TABLE_NAME = pk.TABLE_NAME
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema
              AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION";

        try
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection)
            {
                CommandTimeout = config.CommandTimeoutSeconds,
            };

            var schemaParam = command.Parameters.Add("@schema", SqlDbType.NVarChar, 128);
            schemaParam.Value = schema;

            var tableParam = command.Parameters.Add("@table", SqlDbType.NVarChar, 128);
            tableParam.Value = table;

            using var reader = await command.ExecuteReaderAsync();

            var rows = new List<object[]>();
            int count = 0;

            while (await reader.ReadAsync())
            {
                string columnName = reader.GetString(0);
                rows.Add(new object[]
                {
                    columnName,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) == 1,
                    sanitizer.IsSensitiveColumn(columnName, columnName),
                });
                count++;
            }

            return JsonSerializer.Serialize(new
            {
                columns = new[] { "name", "sqlType", "nullable", "isPrimaryKey", "isSensitive" },
                rows,
                rowCount = count,
                truncated = false,
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                columns = Array.Empty<string>(),
                rows = Array.Empty<object[]>(),
                rowCount = 0,
                truncated = false,
                errors = new[] { ex.Message },
            });
        }
    }

    /// <summary>
    /// Best-effort retrieval of true source column names for aliased expressions
    /// using SQL Server's sys.dm_exec_describe_first_result_set DMV.
    /// Returns an empty list when the DMV is unavailable or returns no rows.
    /// </summary>
    private static async Task<List<string?>> TryGetBaseColumnNamesAsync(SqlConnection connection, string query)
    {
        var baseNames = new List<string?>();
        try
        {
            const string describeSql = "SELECT name, source_column FROM sys.dm_exec_describe_first_result_set(@tsql, NULL, 0)";
            using var cmd = new SqlCommand(describeSql, connection)
            {
                CommandTimeout = 30,
            };
            cmd.Parameters.Add("@tsql", SqlDbType.NVarChar, -1).Value = query;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string? sourceColumn = reader.IsDBNull(1) ? null : reader.GetString(1);
                baseNames.Add(sourceColumn);
            }
        }
        catch
        {
            // Best-effort: if the DMV fails (permissions, unsupported query shape,
            // older SQL Server version, etc.), return empty to signal fallback.
            return new List<string?>();
        }
        return baseNames;
    }

    [McpServerTool, Description("Execute a controlled read-only SQL query.")]
    public async Task<string> ExecuteReadQuery(string query, int? maxRows = null)
    {
        // 1. Validate query before touching the database.
        var (isValid, validationError) = QueryValidator.ValidateReadOnlyQuery(query);
        if (!isValid)
        {
            return JsonSerializer.Serialize(new
            {
                columns = Array.Empty<string>(),
                rows = Array.Empty<object[]>(),
                rowCount = 0,
                truncated = false,
                errors = new[] { validationError ?? "Query validation failed." },
            });
        }

        // 2. Determine effective row limit (user override capped at server limit).
        int effectiveMaxRows = Math.Min(maxRows ?? config.MaxRows, config.MaxRows);
        if (effectiveMaxRows <= 0)
        {
            effectiveMaxRows = config.MaxRows;
        }

        try
        {
            using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection)
            {
                CommandTimeout = config.CommandTimeoutSeconds,
            };

            using var reader = await command.ExecuteReaderAsync();

            // 3. Read column metadata (alias + optional base column name).
            var schemaTable = reader.GetSchemaTable();
            var columnNames = new List<string>();
            var baseColumnNames = new List<string?>();
            if (schemaTable is not null)
            {
                foreach (DataRow row in schemaTable.Rows)
                {
                    string? colName = row["ColumnName"] as string;
                    if (!string.IsNullOrWhiteSpace(colName))
                    {
                        columnNames.Add(colName);
                        baseColumnNames.Add(row["BaseColumnName"] as string);
                    }
                }
            }

            // 4. Try to get accurate base column names from SQL Server DMV.
            //    This fixes false positives where GetSchemaTable()'s BaseColumnName
            //    is either null or incorrectly set to the alias for aliased columns.
            var dmvBaseNames = await TryGetBaseColumnNamesAsync(connection, query);

            if (dmvBaseNames.Count == columnNames.Count)
            {
                baseColumnNames = dmvBaseNames;
            }
            else
            {
                // DMV unavailable or returned mismatched column count.
                // Schema-table BaseColumnName is unreliable for aliased columns
                // (it often repeats the alias), so we treat lineage as unknown.
                baseColumnNames = Enumerable.Repeat<string?>(null, columnNames.Count).ToList();
            }

            // 5. Read rows up to the effective limit.
            var rows = new List<object[]>();
            int count = 0;
            bool truncated = false;

            while (await reader.ReadAsync())
            {
                if (count >= effectiveMaxRows)
                {
                    truncated = true;
                    break;
                }

                var rowValues = new object[reader.FieldCount];
                reader.GetValues(rowValues);

                // Replace DBNull with null so JSON serialization is cleaner.
                for (int i = 0; i < rowValues.Length; i++)
                {
                    if (rowValues[i] is DBNull)
                    {
                        rowValues[i] = null!;
                    }
                }

                rows.Add(rowValues);
                count++;
            }

            // 5. Obfuscate sensitive columns before serialization.
            var sanitizedRows = sanitizer.SanitizeRows(columnNames, baseColumnNames, rows);

            // 6. Build response and enforce max response bytes.
            var responsePayload = new
            {
                columns = columnNames.ToArray(),
                rows = sanitizedRows,
                rowCount = count,
                truncated,
            };

            string json = JsonSerializer.Serialize(responsePayload);

            if (json.Length > config.MaxResponseBytes)
            {
                // Truncate rows until the payload fits.
                int low = 0;
                int high = sanitizedRows.Count;
                int bestFit = 0;

                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    var testPayload = new
                    {
                        columns = columnNames.ToArray(),
                        rows = sanitizedRows.Take(mid).ToList(),
                        rowCount = mid,
                        truncated = true,
                    };
                    string testJson = JsonSerializer.Serialize(testPayload);

                    if (testJson.Length <= config.MaxResponseBytes)
                    {
                        bestFit = mid;
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }

                if (bestFit < 0)
                {
                    bestFit = 0;
                }

                var finalPayload = new
                {
                    columns = columnNames.ToArray(),
                    rows = sanitizedRows.Take(bestFit).ToList(),
                    rowCount = bestFit,
                    truncated = true,
                };
                json = JsonSerializer.Serialize(finalPayload);
            }

            return json;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                columns = Array.Empty<string>(),
                rows = Array.Empty<object[]>(),
                rowCount = 0,
                truncated = false,
                errors = new[] { ex.Message },
            });
        }
    }
}
