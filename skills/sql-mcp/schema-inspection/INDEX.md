# Schema Inspection

Explore database structure and metadata.

## Tools

- [list_tables](list_tables.md) - List all tables/views
- [get_table_schema](get_table_schema.md) - Get columns and types
- [get_table_indexes](get_table_indexes.md) - List indexes
- [get_foreign_keys](get_foreign_keys.md) - Get foreign key constraints

## Use Cases

- Database discovery
- Generate documentation
- Validate schema changes
- Build dynamic queries

## Provider Differences

- **SqlServer**: Full schema support, system catalogs
- **Sqlite**: PRAGMA-based inspection, no schemas
