using System.Data;
using Microsoft.Data.SqlClient;
using SqlServer.Core.Services.Interfaces;

namespace SqlServer.Core.Services;

public class SqlServerProvider : IDbProvider
{
    public string ProviderName => "SqlServer";
    public bool SupportsTransactions => true;
    public bool SupportsSchemas => true;

    public IDbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }

    public string GetTablesQuery()
    {
        return @"
            SELECT
                TABLE_SCHEMA as [Schema],
                TABLE_NAME as TableName,
                TABLE_TYPE as TableType
            FROM INFORMATION_SCHEMA.TABLES
            ORDER BY TABLE_SCHEMA, TABLE_NAME";
    }

    public string GetColumnsQuery(string tableName)
    {
        return @"
            SELECT
                c.COLUMN_NAME as ColumnName,
                c.DATA_TYPE as DataType,
                CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as IsNullable,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IsPrimaryKey,
                CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1 ELSE 0 END as IsIdentity,
                c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
                c.COLUMN_DEFAULT as DefaultValue
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA
                AND c.TABLE_NAME = pk.TABLE_NAME
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION";
    }

    public string GetIndexesQuery(string tableName)
    {
        return @"
            SELECT
                i.name as IndexName,
                OBJECT_NAME(i.object_id) as TableName,
                i.is_unique as IsUnique,
                i.is_primary_key as IsPrimaryKey,
                STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) as Columns
            FROM sys.indexes i
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE OBJECT_NAME(i.object_id) = @tableName
            GROUP BY i.name, i.object_id, i.is_unique, i.is_primary_key";
    }

    public string GetForeignKeysQuery(string tableName)
    {
        return @"
            SELECT
                fk.name as ConstraintName,
                OBJECT_NAME(fk.parent_object_id) as TableName,
                c1.name as ColumnName,
                OBJECT_NAME(fk.referenced_object_id) as ReferencedTableName,
                c2.name as ReferencedColumnName,
                fk.delete_referential_action_desc as DeleteRule,
                fk.update_referential_action_desc as UpdateRule
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            JOIN sys.columns c1 ON fkc.parent_object_id = c1.object_id AND fkc.parent_column_id = c1.column_id
            JOIN sys.columns c2 ON fkc.referenced_object_id = c2.object_id AND fkc.referenced_column_id = c2.column_id
            WHERE OBJECT_NAME(fk.parent_object_id) = @tableName";
    }
}
