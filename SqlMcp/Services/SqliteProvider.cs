using System.Data;
using Microsoft.Data.Sqlite;
using SqlMcp.Services.Interfaces;

namespace SqlMcp.Services;

public class SqliteProvider : IDbProvider
{
    public string ProviderName => "Sqlite";
    public bool SupportsTransactions => true;
    public bool SupportsSchemas => false;

    public IDbConnection CreateConnection(string connectionString)
    {
        return new SqliteConnection(connectionString);
    }

    public string GetTablesQuery()
    {
        return @"
            SELECT
                name as TableName,
                type as TableType
            FROM sqlite_master
            WHERE type IN ('table', 'view')
                AND name NOT LIKE 'sqlite_%'
            ORDER BY name";
    }

    public string GetColumnsQuery(string tableName)
    {
        // PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
        // We need to transform this to match ColumnInfo properties
        return $@"
            SELECT
                [name] as ColumnName,
                [type] as DataType,
                CASE WHEN [notnull] = 0 THEN 1 ELSE 0 END as IsNullable,
                CASE WHEN [pk] > 0 THEN 1 ELSE 0 END as IsPrimaryKey,
                0 as IsIdentity,
                NULL as MaxLength,
                [dflt_value] as DefaultValue
            FROM pragma_table_info('{tableName}')
            ORDER BY [cid]";
    }

    public string GetIndexesQuery(string tableName)
    {
        // PRAGMA index_list returns: seq, name, unique, origin, partial
        // We need to get column information from index_info and aggregate them
        return $@"
            SELECT
                il.name as IndexName,
                '{tableName}' as TableName,
                il.[unique] as IsUnique,
                CASE WHEN il.origin = 'pk' THEN 1 ELSE 0 END as IsPrimaryKey,
                (
                    SELECT group_concat(ii.name, ', ')
                    FROM pragma_index_info(il.name) ii
                    ORDER BY ii.seqno
                ) as Columns
            FROM pragma_index_list('{tableName}') il
            ORDER BY il.seq";
    }

    public string GetForeignKeysQuery(string tableName)
    {
        // PRAGMA foreign_key_list returns: id, seq, table, from, to, on_update, on_delete, match
        // SQLite doesn't name foreign keys, so we generate a name
        return $@"
            SELECT
                'FK_' || '{tableName}' || '_' || [table] || '_' || id as ConstraintName,
                '{tableName}' as TableName,
                [from] as ColumnName,
                [table] as ReferencedTableName,
                [to] as ReferencedColumnName,
                on_delete as DeleteRule,
                on_update as UpdateRule
            FROM pragma_foreign_key_list('{tableName}')
            ORDER BY id, seq";
    }
}
