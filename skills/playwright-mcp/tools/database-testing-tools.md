# Database Testing Tools

MongoDB operations for test data setup, querying, and cleanup.

## Key Methods

### QueryMongoCollection
Query MongoDB collection for test data.

**Parameters:**
- `collectionName` (string, required): Collection name to query
- `connectionString` (string, default: "mongodb://localhost:27017"): MongoDB connection string
- `databaseName` (string, default: "test"): Database name
- `filterJson` (string, default: "{}"): MongoDB query filter as JSON
- `limit` (int, default: 10): Maximum documents to return

**Returns:** string - JSON with query results

---

### InsertTestData
Insert test data into MongoDB collection.

**Parameters:**
- `collectionName` (string, required): Collection name
- `testDataJson` (string, required): Test data as JSON
- `connectionString`, `databaseName`

**Returns:** string - Success message

---

### CleanupTestData
Cleanup test data from MongoDB collection.

**Parameters:**
- `collectionName` (string, required): Collection name
- `filterJson` (string, default: "{}"): Filter for documents to delete
- `connectionString`, `databaseName`

**Returns:** string - Success message with deleted count

---

### ExecuteMongoDbTestCase
Execute MongoDB test case from database.

**Parameters:**
- `testCaseId` (string, required): Test case ID from MongoDB
- `sessionId`, `connectionString`, `databaseName`

**Returns:** string - Test execution results

## Use Case

```
# 1. Insert test data
playwright:insert_test_data \
  --collectionName users \
  --testDataJson '{"name": "Test User", "email": "test@example.com"}'

# 2. Run tests
# ... test actions ...

# 3. Verify in database
playwright:query_mongo_collection \
  --collectionName users \
  --filterJson '{"email": "test@example.com"}'

# 4. Cleanup
playwright:cleanup_test_data \
  --collectionName users \
  --filterJson '{"email": "test@example.com"}'
```
