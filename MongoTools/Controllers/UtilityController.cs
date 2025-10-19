using Microsoft.AspNetCore.Mvc;

namespace MongoTools.Controllers;

/// <summary>
/// Provides utility functions, documentation, and connection profile management
/// </summary>
[ApiController]
[Route("api/utility")]
[Tags("Utility")]
public class UtilityController(MongoDbService mongoDbService, ILogger<UtilityController> logger)
    : ControllerBase
{
    /// <summary>
    /// Understand MongoDB tool capabilities and when to use different connection types
    /// </summary>
    [HttpGet("capabilities")]
    public IActionResult GetCapabilities()
    {
        try
        {
            return Ok(new
            {
                connectionTypes = new
                {
                    primary = new
                    {
                        endpoint = "POST /api/connection/primary",
                        purpose = "Main database connection for day-to-day CRUD operations",
                        supports = (string[])["insert", "query", "update", "delete", "admin_operations"],
                        requirement = "Required for all basic database operations",
                        serverName = "default"
                    },
                    additional = new
                    {
                        endpoint = "POST /api/connection/additional",
                        purpose = "Secondary connections for multi-database operations",
                        supports = (string[])["cross_server_queries", "data_migration", "comparisons"],
                        requirement = "Optional, used for advanced multi-server features",
                        serverName = "user_defined"
                    }
                },
                operationGroups = new
                {
                    basicCrud = new
                    {
                        endpoints = (string[])["POST /api/document/insert-one", "POST /api/document/query", "PUT /api/document/update-one", "DELETE /api/document/delete-many"
                        ],
                        requirement = "Primary connection required",
                        description = "Core database operations for managing documents"
                    },
                    administration = new
                    {
                        endpoints = (string[])["GET /api/collection/list", "POST /api/collection/index", "DELETE /api/collection/drop"
                        ],
                        requirement = "Primary connection required",
                        description = "Database schema and structure management"
                    },
                    multiServer = new
                    {
                        endpoints = (string[])["POST /api/cross-server/compare", "POST /api/cross-server/sync", "POST /api/cross-server/query"
                        ],
                        requirement = "Multiple connections required",
                        description = "Advanced operations across multiple databases"
                    },
                    databaseManagement = new
                    {
                        endpoints = (string[])["GET /api/database/list", "POST /api/database/switch", "POST /api/database/query"
                        ],
                        requirement = "Single connection required",
                        description = "Browse and switch between databases on the same server"
                    }
                },
                commonWorkflows = new
                {
                    singleDatabase = new
                    {
                        step1 = "POST /api/connection/primary with your connection string",
                        step2 = "Use any CRUD operation (insert, query, update, delete)",
                        step3 = "Check status with GET /api/connection/status"
                    },
                    multiDatabase = new
                    {
                        step1 = "POST /api/connection/primary for your main database",
                        step2 = "POST /api/connection/additional for each additional server",
                        step3 = "Use cross-server operations like POST /api/cross-server/compare"
                    },
                    profileConnection = new
                    {
                        step1 = "POST /api/database/connect-and-explore to connect and see all databases",
                        step2 = "Use POST /api/database/switch to change databases or POST /api/database/query",
                        step3 = "Browse with GET /api/database/{databaseName}/collections"
                    }
                },
                troubleshooting = new
                {
                    errorMessage = "No primary connection established",
                    solution = "Run POST /api/connection/primary first",
                    checkHealth = "Use POST /api/cross-server/ping to test connectivity",
                    viewStatus = "Use GET /api/connection/status for comprehensive info",
                    databaseLockIn = "Use POST /api/database/connect-and-explore to avoid being trapped in one database"
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get capabilities");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get quick start workflows and examples for common MongoDB tasks
    /// </summary>
    [HttpGet("quickstart")]
    public IActionResult GetQuickStart()
    {
        try
        {
            return Ok(new
            {
                gettingStarted = new
                {
                    title = "MongoDB Tools Quick Start",
                    firstTime = "If this is your first time, start with POST /api/connection/primary to establish your main database connection"
                },
                workflows = new
                {
                    basicDatabaseWork = new
                    {
                        description = "Working with a single database for CRUD operations",
                        steps = (string[])
                        [
                            "POST /api/connection/primary with connectionString and databaseName",
                            "GET /api/collection/list to see collections",
                            "POST /api/document/insert-one to add a document",
                            "POST /api/document/query to retrieve documents"
                        ]
                    },
                    profileConnectionEnhanced = new
                    {
                        description = "Connect with profile and explore all databases (SOLVES LOCK-IN ISSUE)",
                        steps = (string[])
                        [
                            "POST /api/database/connect-and-explore with profileName",
                            "GET /api/database/list to see all databases",
                            "POST /api/database/switch to change database",
                            "GET /api/collection/list to see collections"
                        ]
                    },
                    dataComparison = new
                    {
                        description = "Compare data between development and production databases",
                        steps = (string[])
                        [
                            "POST /api/connection/primary for dev database",
                            "POST /api/connection/additional for production",
                            "POST /api/cross-server/compare to compare collections"
                        ]
                    },
                    dataMigration = new
                    {
                        description = "Migrate collections from one server to another",
                        steps = (string[])
                        [
                            "Connect to both servers using connection endpoints",
                            "POST /api/cross-server/sync with dryRun=true to preview",
                            "POST /api/cross-server/sync with dryRun=false to execute"
                        ]
                    }
                },
                commonCommands = new
                {
                    connectionStatus = "GET /api/connection/status - Shows all connections and their capabilities",
                    listServers = "GET /api/connection/servers - View all active server connections",
                    healthCheck = "POST /api/cross-server/ping - Test server connectivity",
                    capabilities = "GET /api/utility/capabilities - Understand what each endpoint does",
                    browseDatabases = "GET /api/database/list?serverName=default - See all databases",
                    switchDatabase = "POST /api/database/switch - Change current database"
                },
                examples = new
                {
                    queryWithFilter = "POST /api/document/query with filter: {\"price\": {\"$gte\": 100}}",
                    updateMultiple = "PUT /api/document/update-many with filter and update operations",
                    createIndex = "POST /api/collection/index with indexKeys",
                    aggregation = "POST /api/document/aggregate with pipeline stages",
                    crossDatabaseQuery = "POST /api/database/query to query without switching databases",
                    browseSpecificDatabase = "GET /api/database/{databaseName}/collections"
                },
                nextSteps = new
                {
                    profiles = "GET /api/utility/profiles to see saved connection configurations",
                    advanced = "Explore cross-server operations once you have multiple connections",
                    monitoring = "GET /api/cross-server/health for comprehensive server monitoring",
                    databaseSwitching = "Use database management endpoints to browse multiple databases"
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get quick start");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all available connection profiles and show current connection status
    /// </summary>
    [HttpGet("profiles")]
    public IActionResult ListConnectionProfiles()
    {
        try
        {
            string result = mongoDbService.ListConnectionProfiles();
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list connection profiles");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Connect using a predefined connection profile by name
    /// </summary>
    [HttpPost("connect-profile")]
    public async Task<IActionResult> ConnectWithProfile([FromBody] ConnectWithProfileRequest request)
    {
        try
        {
            string result = await mongoDbService.ConnectWithProfileAsync(request.ProfileName);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect with profile: {ProfileName}", request.ProfileName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get detailed status about auto-connect configuration and available connection methods
    /// </summary>
    [HttpGet("auto-connect-status")]
    public IActionResult GetAutoConnectStatus()
    {
        try
        {
            string result = mongoDbService.GetAutoConnectStatus();
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get auto-connect status");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
