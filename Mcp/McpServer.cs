using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.IO;
using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;

namespace MCPExtension.MCP
{
    public class McpServer
    {
        private readonly ILogger<McpServer> _logger;
        private readonly Dictionary<string, Func<JsonObject, Task<object>>> _tools;
        private bool _isRunning;
        private IWebHost? _webHost;
        private int _port;

        private readonly string? _projectDirectory;

        public McpServer(ILogger<McpServer> logger, int port = 3001, string? projectDirectory = null)
        {
            _logger = logger;
            _tools = new Dictionary<string, Func<JsonObject, Task<object>>>();
            _port = port;
            _projectDirectory = projectDirectory;
        }

        public void RegisterTool(string name, Func<JsonObject, Task<object>> handler)
        {
            _tools[name] = handler;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _isRunning = true;
            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MCP Server starting on port {_port}...");
            _logger.LogInformation($"MCP Server starting on port {_port}...");

            try
            {
                var builder = new WebHostBuilder()
                    .UseKestrel(options =>
                    {
                        options.ListenLocalhost(_port);
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(_logger);
                        services.AddSingleton(this);
                    })
                    .Configure(app =>
                    {
                        // Add middleware to log all incoming requests
                        app.Use(async (context, next) =>
                        {
                            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Incoming request: {context.Request.Method} {context.Request.Path}{context.Request.QueryString} from {context.Connection.RemoteIpAddress}");
                            if (context.Request.Headers.Count > 0)
                            {
                                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Headers: {string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}"))}");
                            }
                            await next();
                        });
                        
                        // Handle SSE endpoint
                        app.Map("/sse", HandleSseApp);
                        
                        // Handle MCP message endpoint - this is where clients send MCP requests
                        app.Map("/message", messageApp =>
                        {
                            messageApp.Run(async context =>
                            {
                                await HandleMcpMessage(context);
                            });
                        });
                        
                        // Handle root endpoint for MCP messages (some clients might expect this)
                        app.Use(async (context, next) =>
                        {
                            if (context.Request.Method == "POST" && context.Request.Path == "/")
                            {
                                await HandleMcpMessage(context);
                                return;
                            }
                            await next();
                        });
                        
                        // Handle health endpoint
                        app.Map("/health", healthApp =>
                        {
                            healthApp.Run(async context =>
                            {
                                await context.Response.WriteAsync("MCP Server is running");
                            });
                        });
                        
                        // Handle metadata endpoint
                        app.Map("/.well-known/mcp", metadataApp =>
                        {
                            metadataApp.Run(async context =>
                            {
                                var metadata = new
                                {
                                    transport = "sse",
                                    sse = new
                                    {
                                        endpoint = "/sse"
                                    },
                                    message = new
                                    {
                                        endpoint = "/message"
                                    },
                                    serverInfo = new
                                    {
                                        name = "mendix-mcp-server",
                                        version = "1.0.0"
                                    }
                                };
                                
                                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Metadata endpoint accessed");
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsync(JsonSerializer.Serialize(metadata));
                            });
                        });
                        
                        // Fallback handler for any other requests
                        app.Run(async context =>
                        {
                            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Unhandled request: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
                            context.Response.StatusCode = 404;
                            await context.Response.WriteAsync("Not Found");
                        });
                    });

                _webHost = builder.Build();
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] WebHost built, starting...");
                await _webHost.StartAsync(cancellationToken);
                
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MCP Server started successfully on http://localhost:{_port}");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Available endpoints:");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] - SSE: http://localhost:{_port}/sse");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] - Messages: http://localhost:{_port}/message");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] - Root POST: http://localhost:{_port}/");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] - Health: http://localhost:{_port}/health");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] - Metadata: http://localhost:{_port}/.well-known/mcp");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Registered {_tools.Count} tools");
                _logger.LogInformation($"MCP Server started successfully on http://localhost:{_port}");
                
                // Keep the server running
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MCP Server error: {ex}");
                _logger.LogError(ex, "MCP Server error");
                throw;
            }
        }

        private void HandleSseApp(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                if (context.Request.Method == "GET")
                {
                    // Handle SSE connection
                    await HandleSseConnection(context);
                }
                else if (context.Request.Method == "POST")
                {
                    // Handle MCP message sent to SSE endpoint
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] POST request to /sse from {context.Connection.RemoteIpAddress}");
                    await HandleMcpMessage(context);
                }
                else
                {
                    context.Response.StatusCode = 405; // Method Not Allowed
                    await context.Response.WriteAsync("Method not allowed. Use GET for SSE connection or POST for messages.");
                }
            });
        }

        private async Task HandleMcpMessage(HttpContext context)
        {
            try
            {
                // Add detailed logging
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MCP Message received - Method: {context.Request.Method}, ContentType: {context.Request.ContentType}");
                
                if (context.Request.Method != "POST")
                {
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Invalid method: {context.Request.Method}, expected POST");
                    context.Response.StatusCode = 405; // Method Not Allowed
                    await context.Response.WriteAsync("Method not allowed. Use POST.");
                    return;
                }

                // Read the request body
                string requestBody;
                using (var reader = new StreamReader(context.Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MCP Request body: {requestBody}");

                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Empty request body");
                    context.Response.StatusCode = 400; // Bad Request
                    await context.Response.WriteAsync("Empty request body");
                    return;
                }

                // Parse JSON
                JsonObject request;
                try
                {
                    request = JsonNode.Parse(requestBody)?.AsObject();
                }
                catch (JsonException ex)
                {
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] JSON parsing error: {ex.Message}");
                    context.Response.StatusCode = 400; // Bad Request
                    await context.Response.WriteAsync($"Invalid JSON: {ex.Message}");
                    return;
                }

                if (request == null)
                {
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Request is null after parsing");
                    context.Response.StatusCode = 400; // Bad Request
                    await context.Response.WriteAsync("Invalid JSON object");
                    return;
                }

                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Processing MCP request...");

                // Process the MCP request
                var response = await ProcessRequest(request);

                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MCP Response: {JsonSerializer.Serialize(response)}");

                // Send response
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] HandleMcpMessage error: {ex}");
                _logger.LogError(ex, "Error handling MCP message");
                
                context.Response.StatusCode = 500; // Internal Server Error
                await context.Response.WriteAsync($"Internal server error: {ex.Message}");
            }
        }

        private string GetLogFilePath()
        {
            try
            {
                // Use the Mendix project directory if available
                if (!string.IsNullOrEmpty(_projectDirectory))
                {
                    string resourcesDir = System.IO.Path.Combine(_projectDirectory, "resources");
                    if (!System.IO.Directory.Exists(resourcesDir))
                    {
                        System.IO.Directory.CreateDirectory(resourcesDir);
                    }
                    
                    return System.IO.Path.Combine(resourcesDir, "mcp_debug.log");
                }
                
                // Fallback to extension project directory if no project directory provided
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string executingDirectory = System.IO.Path.GetDirectoryName(assembly.Location);
                DirectoryInfo directory = new DirectoryInfo(executingDirectory);
                string targetDirectory = directory?.Parent?.Parent?.Parent?.FullName 
                    ?? throw new InvalidOperationException("Could not determine target directory");

                string resourcesDir2 = System.IO.Path.Combine(targetDirectory, "resources");
                if (!System.IO.Directory.Exists(resourcesDir2))
                {
                    System.IO.Directory.CreateDirectory(resourcesDir2);
                }
                
                return System.IO.Path.Combine(resourcesDir2, "mcp_debug.log");
            }
            catch (Exception ex)
            {
                // Fallback to current directory if we can't determine project directory
                System.Diagnostics.Debug.WriteLine($"Could not determine log file path: {ex.Message}");
                return System.IO.Path.Combine(Environment.CurrentDirectory, "mcp_debug.log");
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = GetLogFilePath();
                File.AppendAllText(logPath, message + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors to prevent infinite loops
            }
        }

        private async Task HandleSseConnection(HttpContext context)
        {
            context.Response.Headers.Add("Content-Type", "text/event-stream");
            context.Response.Headers.Add("Cache-Control", "no-cache");
            context.Response.Headers.Add("Connection", "keep-alive");
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Cache-Control");

            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] SSE client connected from {context.Connection.RemoteIpAddress}");
            _logger.LogInformation("SSE client connected");

            try
            {
                // Send initial connection message
                await SendSseMessage(context.Response, "connected", "MCP Server ready");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Sent SSE connected message");

                // Keep connection alive and handle incoming messages via POST to /message endpoint
                while (!context.RequestAborted.IsCancellationRequested && _isRunning)
                {
                    await Task.Delay(30000, context.RequestAborted); // Send keepalive every 30 seconds
                    await SendSseMessage(context.Response, "keepalive", "");
                    // Keepalive sent silently - no logging to prevent log file clutter
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] SSE connection error: {ex}");
                _logger.LogError(ex, "SSE connection error");
            }
            finally
            {
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] SSE client disconnected");
                _logger.LogInformation("SSE client disconnected");
            }
        }

        private async Task SendSseMessage(HttpResponse response, string eventType, string data)
        {
            var message = $"event: {eventType}\ndata: {data}\n\n";
            var bytes = Encoding.UTF8.GetBytes(message);
            await response.Body.WriteAsync(bytes);
            await response.Body.FlushAsync();
        }

        public async Task<object> ProcessMcpRequest(JsonObject request)
        {
            return await ProcessRequest(request);
        }

        private async Task<object> ProcessRequest(JsonObject request)
        {
            var method = request["method"]?.ToString();
            var id = request["id"]?.AsValue();

            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Processing method: {method}, id: {id}");

            switch (method)
            {
                case "initialize":
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Handling initialize request");
                    var initResponse = CreateInitializeResponse(id);
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Initialize response created: {JsonSerializer.Serialize(initResponse)}");
                    return initResponse;

                case "tools/list":
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === TOOLS/LIST REQUEST ===");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Handling tools/list request");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Available tools count: {_tools.Count}");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Available tools: {string.Join(", ", _tools.Keys)}");
                    var toolsResponse = CreateToolsListResponse(id);
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Tools list response created with {_tools.Count} tools");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === END TOOLS/LIST REQUEST ===");
                    return toolsResponse;

                case "tools/call":
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Handling tools/call request");
                    var paramsObj = request["params"]?.AsObject();
                    if (paramsObj != null)
                    {
                        return await HandleToolCall(id, paramsObj);
                    }
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] tools/call missing params");
                    return CreateErrorResponse(id, "Invalid parameters", "Missing params");

                default:
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Unknown method: {method}");
                    return CreateErrorResponse(id, "Method not found", $"Unknown method: {method}");
            }
        }

        private object CreateInitializeResponse(JsonNode id)
        {
            return new
            {
                jsonrpc = "2.0",
                id = id?.AsValue(),
                result = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new
                        {
                            listChanged = false
                        }
                    },
                    serverInfo = new
                    {
                        name = "mendix-mcp-server",
                        version = "1.0.0"
                    }
                }
            };
        }

        private object CreateToolsListResponse(JsonNode id)
        {
            var tools = new List<object>();
            
            foreach (var toolName in _tools.Keys)
            {
                var schema = GetToolInputSchema(toolName);
                var description = GetToolDescription(toolName);
                
                var tool = new
                {
                    name = toolName,
                    description = description,
                    inputSchema = schema
                };
                
                // Special logging for create_microflow_activities
                if (toolName == "create_microflow_activities")
                {
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === create_microflow_activities TOOL DEFINITION ===");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Tool name: {toolName}");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Description: {description}");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Schema: {JsonSerializer.Serialize(schema)}");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Full tool object: {JsonSerializer.Serialize(tool)}");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === END TOOL DEFINITION ===");
                }
                
                tools.Add(tool);
            }

            var response = new
            {
                jsonrpc = "2.0",
                id = id?.AsValue(),
                result = new
                {
                    tools = tools
                }
            };
            
            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Tools list response contains {tools.Count} tools");
            
            return response;
        }

        private async Task<object> HandleToolCall(JsonNode id, JsonObject paramsObj)
        {
            try
            {
                var toolName = paramsObj["name"]?.ToString();
                var arguments = paramsObj["arguments"]?.AsObject();

                // Enhanced logging for create_microflow_activities debugging
                if (toolName == "create_microflow_activities")
                {
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === DEBUGGING create_microflow_activities ===");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Full paramsObj: {JsonSerializer.Serialize(paramsObj)}");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Extracted toolName: '{toolName}'");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Raw arguments object: {arguments}");
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Arguments JSON: {JsonSerializer.Serialize(arguments)}");
                    
                    if (arguments != null)
                    {
                        foreach (var kvp in arguments)
                        {
                            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Argument Key: '{kvp.Key}', Value: '{kvp.Value}'");
                        }
                    }
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === END DEBUG ===");
                }

                if (string.IsNullOrEmpty(toolName) || !_tools.ContainsKey(toolName))
                {
                    return CreateErrorResponse(id, "Tool not found", $"Unknown tool: {toolName}");
                }

                // Log the tool call and arguments for debugging
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Tool call: {toolName}");
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Arguments: {JsonSerializer.Serialize(arguments)}");

                var result = await _tools[toolName](arguments ?? new JsonObject());

                return new
                {
                    jsonrpc = "2.0",
                    id = id?.AsValue(),
                    result = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = result
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool");
                return CreateErrorResponse(id, "Tool execution error", ex.Message);
            }
        }

        private object CreateErrorResponse(JsonNode id, string message, string details)
        {
            return new
            {
                jsonrpc = "2.0",
                id = id?.AsValue(),
                error = new
                {
                    code = -32000,
                    message = message,
                    data = details
                }
            };
        }

        private string GetToolDescription(string toolName)
        {
            return toolName switch
            {
      "read_domain_model" => "Read the domain model structure for a specific module. Requires module_name parameter.",
       "create_entity" => "Create a new entity in the domain model",
                "modify_entity" => "Modify an existing entity by adding, removing, renaming, or updating attributes",
                "update_entity_layout" => "Update the visual layout/positioning of entities in the domain model (grid, horizontal, vertical, circular, or custom positions)",
                "manage_annotations" => "Add, update, remove, or read documentation annotations for entities and associations. NOTE: Domain model-level annotations are not accessible through the API.",
                "create_association" => "Create a new association between entities",
                "delete_model_element" => "Delete domain model elements (entity, attribute, association, enumeration). NOTE: Cannot delete documents (pages, microflows, folders) - API limitation",
                "diagnose_associations" => "Diagnose association creation issues",
                "create_multiple_entities" => "Create multiple entities at once",
                "create_multiple_associations" => "Create multiple associations at once",
                "create_domain_model_from_schema" => "Create a complete domain model from a schema definition",
                "save_data" => "Generate realistic sample data for Mendix domain model entities",
                "generate_overview_pages" => "Generate overview pages for entities",
                "list_microflows" => "List all microflows in a module",
                "list_modules" => "List all modules in the Mendix project with basic information",
                "list_enumerations" => "List all enumerations in a specific module with their values",
                "get_last_error" => "Get details about the last error",
                "get_project_errors" => "Get project errors and consistency check information (Note: API limitation - provides workarounds and common errors)",
                "list_available_tools" => "List all available tools",
                "add_pages_to_navigation" => "Add pages to the responsive web navigation profile. Use this to make pages accessible through the app's main navigation menu. Check for duplicates before adding.",
                "list_navigation_items" => "List all navigation items across all navigation profiles. Shows navigation document structure and types available in the Extensions API.",
                "remove_pages_from_navigation" => "Remove pages from navigation profiles. NOTE: API LIMITATION - Direct removal not supported, manual workaround provided.",
                "list_page_properties" => "List available properties of a page. NOTE: API LIMITATION - IPage interface only exposes Name property. Other properties (title, URL, layout, widgets) not accessible.",
                "rename_page" => "Rename a page in a module. This is the ONLY page modification operation supported by the Extensions API.",
                "debug_info" => "Get comprehensive debug information about the domain model",
                "read_microflow_details" => "Get details about a specific microflow including activities with their positions",
                "read_microflow_activities" => "Get comprehensive details about a microflow including all activities, input parameters, and return type. Shows activity properties, types, and positions.",
                "add_create_object_activity" => "Add a create object activity to an existing microflow. Creates a new object instance of the specified entity and optionally commits it to the database. Activities are inserted in the order they are added (after the start event).",
                "add_change_object_activity" => "Add a change object activity to modify attributes of an existing object variable in a microflow. Use this to set attribute values using expressions. Activities are inserted in the order they are added (after the start event).",
                "create_microflow" => "Create a new microflow in the module with parameters and return type",
                "create_microflow_activities" => "Create one or more microflow activities in sequence within an existing microflow. Activities are inserted in the correct order automatically. For single activities, use an array with one item. This unified approach replaces individual activity creation for better reliability.",
                _ => "Tool description not available"
            };
        }

        private object GetToolInputSchema(string toolName)
        {
            return toolName switch
            {
                "read_domain_model" => new
                {
                    type = "object",
                  properties = new {
                  module_name = new { 
 type = "string",
   description = "Name of the module whose domain model should be read"
        }
     },
         required = new[] { "module_name" }
        },
     "create_entity" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module where the entity will be created" },
                        entity_name = new { type = "string" },
                        attributes = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string" },
                                    type = new { type = "string" },
                                    enumerationValues = new
                                    {
                                        type = "array",
                                        items = new { type = "string" }
                                    }
                                }
                            }
                        }
                    },
                    required = new[] { "module_name", "entity_name", "attributes" }
                },
                "modify_entity" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the entity" },
                        entity_name = new { type = "string", description = "Name of the entity to modify" },
                        add_attributes = new 
                        { 
                            type = "array",
                            description = "Attributes to add to the entity",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Attribute name" },
                                    type = new { type = "string", description = "Attribute type (String, Integer, Boolean, DateTime, Decimal, Long, Enumeration, etc.)" },
                                    enumerationValues = new { type = "array", items = new { type = "string" }, description = "Required for Enumeration type" },
                                    enumeration_name = new { type = "string", description = "Name of existing enumeration to use (alternative to enumerationValues)" }
                                },
                                required = new[] { "name", "type" }
                            }
                        },
                        remove_attributes = new 
                        { 
                            type = "array",
                            description = "Attribute names to remove",
                            items = new { type = "string" }
                        },
                        rename_attributes = new 
                        { 
                            type = "array",
                            description = "Attributes to rename",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    old_name = new { type = "string" },
                                    new_name = new { type = "string" }
                                },
                                required = new[] { "old_name", "new_name" }
                            }
                        },
                        update_attributes = new 
                        { 
                            type = "array",
                            description = "Attributes to update (change type)",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Attribute name" },
                                    type = new { type = "string", description = "New attribute type" },
                                    enumerationValues = new { type = "array", items = new { type = "string" }, description = "Required for Enumeration type" },
                                    enumeration_name = new { type = "string", description = "Name of existing enumeration to use" }
                                },
                                required = new[] { "name", "type" }
                            }
                        }
                    },
                    required = new[] { "module_name", "entity_name" }
                },
                "update_entity_layout" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the entities" },
                        layout_mode = new 
                        { 
                            type = "string", 
                            description = "Layout arrangement mode: 'custom' (specify positions), 'grid' (arrange in grid), 'horizontal' (arrange horizontally), 'vertical' (arrange vertically), 'circular' (arrange in circle)",
                            @enum = new[] { "custom", "grid", "horizontal", "vertical", "circular" }
                        },
                        entity_positions = new
                        {
                            type = "array",
                            description = "Array of entity positions (required for 'custom' mode)",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    entity_name = new { type = "string", description = "Name of the entity to position" },
                                    x = new { type = "integer", description = "X coordinate" },
                                    y = new { type = "integer", description = "Y coordinate" }
                                },
                                required = new[] { "entity_name", "x", "y" }
                            }
                        },
                        columns = new { type = "integer", description = "Number of columns (for 'grid' mode, default: 5)" },
                        spacing = new { type = "integer", description = "Spacing between entities (for 'horizontal' and 'vertical' modes)" },
                        spacing_x = new { type = "integer", description = "Horizontal spacing (for 'grid' mode, default: 250)" },
                        spacing_y = new { type = "integer", description = "Vertical spacing (for 'grid' mode, default: 200)" },
                        start_x = new { type = "integer", description = "Starting X coordinate (default: 20)" },
                        start_y = new { type = "integer", description = "Starting Y coordinate (default: 20)" },
                        x = new { type = "integer", description = "X coordinate (for 'vertical' mode or 'horizontal' y position)" },
                        y = new { type = "integer", description = "Y coordinate (for 'horizontal' mode or 'vertical' x position)" },
                        radius = new { type = "integer", description = "Circle radius (for 'circular' mode, default: 300)" },
                        center_x = new { type = "integer", description = "Circle center X (for 'circular' mode, default: 500)" },
                        center_y = new { type = "integer", description = "Circle center Y (for 'circular' mode, default: 400)" }
                    },
                    required = new[] { "module_name" }
                },
                "manage_annotations" => new
                {
                    type = "object",
                    description = "Add, update, remove, or read documentation annotations for entities and associations. NOTE: Domain model-level annotations (the annotation text box in the domain model diagram) are NOT supported by the Mendix Extensions API as they are part of the visual layer.",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the entities/associations" },
                        action = new 
                        { 
                            type = "string", 
                            description = "Action to perform: 'set'/'update'/'add' (add/update annotations), 'remove'/'clear' (remove annotations), 'read'/'get' (read current annotations)",
                            @enum = new[] { "set", "update", "add", "remove", "clear", "read", "get" }
                        },
                        entity_annotations = new
                        {
                            type = "array",
                            description = "Entity annotations to set/update (for 'set', 'update', 'add' actions). These appear in Studio Pro when hovering over entities.",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    entity_name = new { type = "string", description = "Name of the entity" },
                                    documentation = new { type = "string", description = "Documentation/annotation text" }
                                },
                                required = new[] { "entity_name", "documentation" }
                            }
                        },
                        association_annotations = new
                        {
                            type = "array",
                            description = "Association annotations to set/update (for 'set', 'update', 'add' actions). These appear in Studio Pro when hovering over associations.",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    association_name = new { type = "string", description = "Name of the association" },
                                    documentation = new { type = "string", description = "Documentation/annotation text" }
                                },
                                required = new[] { "association_name", "documentation" }
                            }
                        },
                        entity_names = new
                        {
                            type = "array",
                            description = "Entity names to remove annotations from (for 'remove', 'clear' actions)",
                            items = new { type = "string" }
                        },
                        association_names = new
                        {
                            type = "array",
                            description = "Association names to remove annotations from (for 'remove', 'clear' actions)",
                            items = new { type = "string" }
                        }
                    },
                    required = new[] { "module_name" }
                },
                "create_association" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the entities" },
                        name = new { type = "string" },
                        parent = new { type = "string" },
                        child = new { type = "string" },
                        type = new { type = "string" }
                    },
                    required = new[] { "module_name", "name", "parent", "child" }
                },
                "create_multiple_associations" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the entities" },
                        associations = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string" },
                                    parent = new { type = "string" },
                                    child = new { type = "string" },
                                    type = new { type = "string" }
                                },
                                required = new[] { "name", "parent", "child" }
                            }
                        }
                    },
                    required = new[] { "module_name", "associations" }
                },
                "create_domain_model_from_schema" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module where the domain model will be created" },
                        schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                entities = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            entity_name = new { type = "string" },
                                            attributes = new
                                            {
                                                type = "array",
                                                items = new
                                                {
                                                    type = "object",
                                                    properties = new
                                                    {
                                                        name = new { type = "string" },
                                                        type = new { type = "string" },
                                                        enumerationValues = new
                                                        {
                                                            type = "array",
                                                            items = new { type = "string" }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                associations = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            name = new { type = "string" },
                                            parent = new { type = "string" },
                                            child = new { type = "string" },
                                            type = new { type = "string" }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    required = new[] { "module_name", "schema" }
                },
                "delete_model_element" => new
                {
                    type = "object",
                    description = "Delete elements from the domain model. NOTE: API LIMITATION - Can only delete domain model elements (entity, attribute, association, enumeration). Cannot delete documents (pages, microflows, folders) - this is a known Mendix Extensions API v8.0 limitation. Documents must be deleted manually in Studio Pro.",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the element" },
                        element_type = new 
                        { 
                            type = "string", 
                            description = "Type of element to delete. SUPPORTED: 'entity', 'attribute', 'association', 'enumeration'. NOT SUPPORTED (API limitation): 'page', 'microflow', 'folder', 'document'",
                            @enum = new[] { "entity", "attribute", "association", "enumeration", "page", "microflow", "folder", "document" }
                        },
                        entity_name = new { type = "string", description = "Name of the entity (required for entity, attribute, and association deletion)" },
                        attribute_name = new { type = "string", description = "Name of the attribute (required for attribute deletion)" },
                        association_name = new { type = "string", description = "Name of the association (required for association deletion)" },
                        enumeration_name = new { type = "string", description = "Name of the enumeration (required for enumeration deletion)" },
                        document_name = new { type = "string", description = "Name of the document (for API limitation error messages only - deletion not supported)" }
                    },
                    required = new[] { "module_name", "element_type" }
                },
                "diagnose_associations" => new
                {
                    type = "object",
                    properties = new 
                    { 
                        module_name = new { type = "string", description = "Name of the module to diagnose (optional)" }
                    },
                    required = new string[0]
                },
                "create_multiple_entities" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module where entities will be created" },
                        entities = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    entity_name = new { type = "string" },
                                    attributes = new
                                    {
                                        type = "array",
                                        items = new
                                        {
                                            type = "object",
                                            properties = new
                                            {
                                                name = new { type = "string" },
                                                type = new { type = "string" },
                                                enumerationValues = new
                                                {
                                                    type = "array",
                                                    items = new { type = "string" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    required = new[] { "module_name", "entities" }
                },
                "save_data" => new
                {
                    type = "object",
                    properties = new
                    {
                        data = new { 
                            type = "object",
                            description = "Entity data organized by ModuleName.EntityName keys with arrays of records containing VirtualId for relationships",
                            additionalProperties = new {
                                type = "array",
                                items = new {
                                    type = "object",
                                    properties = new {
                                        VirtualId = new { type = "string", description = "Unique temporary identifier for establishing relationships" }
                                    },
                                    required = new[] { "VirtualId" }
                                }
                            }
                        }
                    },
                    required = new[] { "data" }
                },
                "generate_overview_pages" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the entities" },
                        entity_names = new
                        {
                            type = "array",
                            items = new { type = "string" }
                        },
                        generate_index_snippet = new { type = "boolean" }
                    },
                    required = new[] { "module_name", "entity_names" }
                },
                "list_microflows" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string" }
                    },
                    required = new[] { "module_name" }
                },
                "list_modules" => new
                {
                    type = "object",
                    properties = new { },
                    required = new string[0]
                },
                "list_enumerations" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module to list enumerations from" }
                    },
                    required = new[] { "module_name" }
                },
                "get_last_error" => new
                {
                    type = "object",
                    properties = new { },
                    required = new string[0]
                },
                "get_project_errors" => new
                {
                    type = "object",
                    properties = new { },
                    required = new string[0]
                },
                "list_available_tools" => new
                {
                    type = "object",
                    properties = new { },
                    required = new string[0]
                },
                "add_pages_to_navigation" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new 
                        { 
                            type = "string", 
                            description = "Name of the module containing the pages to add to navigation" 
                        },
                        page_names = new
                        {
                            type = "array",
                            description = "Array of page names to add to the navigation menu. Duplicates will be checked before adding.",
                            items = new { type = "string" }
                        }
                    },
                    required = new[] { "module_name", "page_names" },
                    description = "Adds specified pages to the responsive web navigation profile, making them accessible through the app's main navigation menu."
                },
                "list_navigation_items" => new
                {
                    type = "object",
                    properties = new { },
                    required = new string[0],
                    description = "Lists all navigation items and explores the navigation structure available through the Extensions API."
                },
                "remove_pages_from_navigation" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new
                        {
                            type = "string",
                            description = "Name of the module containing navigation items (optional - for documentation purposes)"
                        },
                        page_names = new
                        {
                            type = "array",
                            description = "Array of page names to remove from navigation (optional - for documentation purposes)",
                            items = new { type = "string" }
                        }
                    },
                    required = new string[0],
                    description = "API LIMITATION: Navigation item removal not directly supported. Returns workaround instructions for manual removal."
                },
                "list_page_properties" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new
                        {
                            type = "string",
                            description = "Name of the module containing the page"
                        },
                        page_name = new
                        {
                            type = "string",
                            description = "Name of the page to inspect"
                        }
                    },
                    required = new[] { "module_name", "page_name" },
                    description = "Lists page information. API LIMITATION: IPage interface only exposes Name property. Other page properties (title, URL, layout, widgets) are not accessible through the Extensions API."
                },
                "rename_page" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new
                        {
                            type = "string",
                            description = "Name of the module containing the page"
                        },
                        page_name = new
                        {
                            type = "string",
                            description = "Current name of the page to rename"
                        },
                        new_name = new
                        {
                            type = "string",
                            description = "New name for the page"
                        }
                    },
                    required = new[] { "module_name", "page_name", "new_name" },
                    description = "Renames a page. This is the ONLY page modification operation supported by the Extensions API."
                },
                "debug_info" => new
                {
                    type = "object",
                    properties = new { },
                    required = new string[0]
                },
                "read_microflow_details" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string" },
                        microflow_name = new { type = "string" }
                    },
                    required = new[] { "module_name", "microflow_name" }
                },
                "read_microflow_activities" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the microflow" },
                        microflow_name = new { type = "string", description = "Name of the microflow to analyze" }
                    },
                    required = new[] { "module_name", "microflow_name" }
                },
                "add_create_object_activity" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the microflow" },
                        microflow_name = new { type = "string", description = "Name of the microflow to edit" },
                        entity_name = new { type = "string", description = "Name of the entity to create (e.g., 'Customer')" },
                        output_variable = new { type = "string", description = "Optional: Name for the variable holding the created object. Defaults to 'New{EntityName}'" },
                        commit = new { type = "string", description = "Optional: Commit option ('yes', 'no', 'yeswithoutevents'). Default: 'no'" },
                        refresh_in_client = new { type = "boolean", description = "Optional: Refresh object in client after commit. Default: false" }
                    },
                    required = new[] { "module_name", "microflow_name", "entity_name" },
                    description = "NOTE: Activities are inserted after the start event in the order they are added. Call this tool multiple times in the desired sequence to build your microflow."
                },
                "add_change_object_activity" => new
                {
                    type = "object",
                    properties = new
                    {
                        module_name = new { type = "string", description = "Name of the module containing the microflow" },
                        microflow_name = new { type = "string", description = "Name of the microflow to edit" },
                        object_variable = new { type = "string", description = "Name of the object variable to modify (e.g., '$NewInstitution')" },
                        changes = new { 
                            type = "array", 
                            description = "Array of attribute changes to apply",
                            items = new {
                                type = "object",
                                properties = new {
                                    attribute = new { type = "string", description = "Name of the attribute to change (e.g., 'CustomerNumber')" },
                                    value = new { type = "string", description = "Expression for the new value (e.g., '$parameter/SingleSelectionNumber' or '\"literal value\"')" }
                                },
                                required = new[] { "attribute", "value" }
                            }
                        },
                        commit = new { type = "string", description = "Optional: Commit option ('yes', 'no', 'yeswithoutevents'). Default: 'no'" },
                        refresh_in_client = new { type = "boolean", description = "Optional: Refresh object in client after commit. Default: false" }
                    },
                    required = new[] { "module_name", "microflow_name", "object_variable", "changes" },
                    description = "NOTE: Activities are inserted after the start event in the order they are added. Call this tool multiple times in the desired sequence to build your microflow."
                },
                "create_microflow" => new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string" },
                        parameters = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string" },
                                    type = new { type = "string" }
                                },
                                required = new[] { "name", "type" }
                            }
                        },
                        returnType = new { type = "string" }
                    },
                    required = new[] { "name" }
                },
                "create_microflow_activities" => new
                {
                    type = "object",
                    properties = new
                    {
                        microflow_name = new { type = "string", description = "Name of the microflow to add activities to" },
                        activities = new 
                        { 
                            type = "array", 
                            description = "Array of activity definitions to create in sequence. For single activities, use an array with one item.",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    activity_type = new { type = "string", description = "Type of activity to create (e.g., 'create_object', 'commit', 'retrieve_from_database')" },
                                    activity_config = new { 
                                        type = "object", 
                                        description = "Configuration object for the activity",
                                        additionalProperties = true
                                    }
                                },
                                required = new[] { "activity_type" }
                            }
                        }
                    },
                    required = new[] { "microflow_name", "activities" }
                },
                _ => new
                {
                    type = "object",
                    properties = new { },
                    required = new string[0]
                }
            };
        }

        public void Stop()
        {
            _isRunning = false;
            _webHost?.StopAsync().Wait(5000);
            _webHost?.Dispose();
        }

        public int Port => _port;
    }
}
