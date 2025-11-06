using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MongoServer.Core;

namespace MongoTools.Controllers;

/// <summary>
/// Manages MongoDB document CRUD operations
/// </summary>
[ApiController]
[Route("api/document")]
[Tags("Document")]
public class DocumentController(MongoDbService mongoDbService, ILogger<DocumentController> logger)
    : ControllerBase
{
    private BadRequestObjectResult? ValidateDefaultConnection(string operation)
    {
        if (!mongoDbService.ConnectionManager.IsConnected("default"))
        {
            return BadRequest(new
            {
                success = false,
                error = $"No primary connection established for {operation} operation",
                solution = "Run POST /api/connection/primary to establish your main database connection",
                operatedOn = "none",
                suggestion = "Primary connection is required for CRUD operations"
            });
        }
        return null;
    }

    /// <summary>
    /// Query documents from a collection
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] QueryRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("query");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.QueryAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Filter ?? "{}",
                request.Limit ?? 100,
                request.Skip ?? 0);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "query",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                serverName = resultObj.GetProperty("serverName").GetString(),
                count = resultObj.GetProperty("count").GetInt32(),
                documents = resultObj.GetProperty("documents")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query collection {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "query",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = (request.ServerName ?? "default") == "default" 
                    ? "Ensure primary connection is established and healthy"
                    : $"Verify server '{request.ServerName}' is connected and accessible"
            });
        }
    }

    /// <summary>
    /// Insert a single document into a collection
    /// </summary>
    [HttpPost("insert-one")]
    public async Task<IActionResult> InsertOne([FromBody] InsertOneRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("insert");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.InsertOneAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Document);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "insert_one",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                insertedId = resultObj.GetProperty("insertedId").GetString(),
                message = "Document inserted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to insert document into {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "insert_one",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = (request.ServerName ?? "default") == "default"
                    ? "Ensure primary connection is established and document JSON is valid"
                    : $"Verify server '{request.ServerName}' is connected and document JSON is valid"
            });
        }
    }

    /// <summary>
    /// Insert multiple documents into a collection
    /// </summary>
    [HttpPost("insert-many")]
    public async Task<IActionResult> InsertMany([FromBody] InsertManyRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("insert_many");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.InsertManyAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Documents);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "insert_many",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                message = resultObj.GetProperty("message").GetString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to insert documents into {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "insert_many",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure documents is a valid JSON array"
            });
        }
    }

    /// <summary>
    /// Update a single document in a collection
    /// </summary>
    [HttpPut("update-one")]
    public async Task<IActionResult> UpdateOne([FromBody] UpdateOneRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("update_one");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.UpdateOneAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Filter,
                request.Update);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "update_one",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                matchedCount = resultObj.GetProperty("matchedCount").GetInt64(),
                modifiedCount = resultObj.GetProperty("modifiedCount").GetInt64()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update document in {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "update_one",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure filter/update JSON syntax is correct"
            });
        }
    }

    /// <summary>
    /// Update multiple documents in a collection
    /// </summary>
    [HttpPut("update-many")]
    public async Task<IActionResult> UpdateMany([FromBody] UpdateManyRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("update_many");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.UpdateManyAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Filter,
                request.Update);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "update_many",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                matchedCount = resultObj.GetProperty("matchedCount").GetInt64(),
                modifiedCount = resultObj.GetProperty("modifiedCount").GetInt64()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update documents in {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "update_many",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure filter/update JSON syntax is correct"
            });
        }
    }

    /// <summary>
    /// Delete a single document from a collection
    /// </summary>
    [HttpDelete("delete-one")]
    public async Task<IActionResult> DeleteOne([FromBody] DeleteOneRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("delete_one");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.DeleteOneAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Filter);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "delete_one",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                deletedCount = resultObj.GetProperty("deletedCount").GetInt64()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete document from {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "delete_one",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure filter JSON syntax is correct"
            });
        }
    }

    /// <summary>
    /// Delete multiple documents from a collection
    /// </summary>
    [HttpDelete("delete-many")]
    public async Task<IActionResult> DeleteMany([FromBody] DeleteManyRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("delete_many");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.DeleteManyAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Filter);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "delete_many",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                deletedCount = resultObj.GetProperty("deletedCount").GetInt64()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete documents from {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "delete_many",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure filter JSON syntax is correct"
            });
        }
    }

    /// <summary>
    /// Run an aggregation pipeline on a collection for advanced data processing
    /// </summary>
    [HttpPost("aggregate")]
    public async Task<IActionResult> Aggregate([FromBody] AggregateRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("aggregate");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.AggregateAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Pipeline);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "aggregate",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                stages = resultObj.GetProperty("stages").GetInt32(),
                results = resultObj.GetProperty("results")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to aggregate on {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "aggregate",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure pipeline is a valid JSON array of aggregation stages"
            });
        }
    }

    /// <summary>
    /// Count documents in a collection, optionally with a filter
    /// </summary>
    [HttpPost("count")]
    public async Task<IActionResult> CountDocuments([FromBody] CountDocumentsRequest request)
    {
        if ((request.ServerName ?? "default") == "default")
        {
            BadRequestObjectResult? validationResult = ValidateDefaultConnection("count_documents");
            if (validationResult != null) return validationResult;
        }

        try
        {
            string result = await mongoDbService.CountDocumentsAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.Filter ?? "{}");
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "count_documents",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                count = resultObj.GetProperty("count").GetInt64()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to count documents in {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "count_documents",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure filter JSON syntax is correct"
            });
        }
    }
}

// Request DTOs
public record QueryRequest(
    string CollectionName,
    string? Filter = "{}",
    int? Limit = 100,
    int? Skip = 0,
    string? ServerName = "default"
);

public record InsertOneRequest(
    string CollectionName,
    string Document,
    string? ServerName = "default"
);

public record InsertManyRequest(
    string CollectionName,
    string Documents,
    string? ServerName = "default"
);

public record UpdateOneRequest(
    string CollectionName,
    string Filter,
    string Update,
    string? ServerName = "default"
);

public record UpdateManyRequest(
    string CollectionName,
    string Filter,
    string Update,
    string? ServerName = "default"
);

public record DeleteOneRequest(
    string CollectionName,
    string Filter,
    string? ServerName = "default"
);

public record DeleteManyRequest(
    string CollectionName,
    string Filter,
    string? ServerName = "default"
);

public record AggregateRequest(
    string CollectionName,
    string Pipeline,
    string? ServerName = "default"
);

public record CountDocumentsRequest(
    string CollectionName,
    string? Filter = "{}",
    string? ServerName = "default"
);