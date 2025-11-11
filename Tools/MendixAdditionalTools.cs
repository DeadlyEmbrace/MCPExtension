using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;
using Mendix.StudioPro.ExtensionsAPI.Model.Microflows;
using Mendix.StudioPro.ExtensionsAPI.Model.Microflows.Actions;
using Mendix.StudioPro.ExtensionsAPI.Model.MicroflowExpressions;
using Mendix.StudioPro.ExtensionsAPI.Model.DomainModels;
using Mendix.StudioPro.ExtensionsAPI.Model.Enumerations;
using Mendix.StudioPro.ExtensionsAPI.Model.Pages;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MCPExtension.Utils;

namespace MCPExtension.Tools
{
    public class MendixAdditionalTools
    {
        private readonly IModel _model;
        private readonly ILogger<MendixAdditionalTools> _logger;
        private readonly IPageGenerationService _pageGenerationService;
        private readonly INavigationManagerService _navigationManagerService;
        private readonly IServiceProvider _serviceProvider;
        private readonly string? _projectDirectory;
        private static string? _lastError;
        private static Exception? _lastException;

        public MendixAdditionalTools(
            IModel model, 
            ILogger<MendixAdditionalTools> logger,
            IPageGenerationService pageGenerationService,
            INavigationManagerService navigationManagerService,
            IServiceProvider serviceProvider,
            string? projectDirectory = null)
        {
            _model = model;
            _logger = logger;
            _pageGenerationService = pageGenerationService;
            _navigationManagerService = navigationManagerService;
            _serviceProvider = serviceProvider;
            _projectDirectory = projectDirectory;
        }

        private string GetDebugLogPath()
        {
            try
            {
                // Use the project directory if available
                if (!string.IsNullOrEmpty(_projectDirectory))
                {
                    string resourcesDir = System.IO.Path.Combine(_projectDirectory, "resources");
                    
                    if (!System.IO.Directory.Exists(resourcesDir))
                    {
                        System.IO.Directory.CreateDirectory(resourcesDir);
                    }
                    
                    return System.IO.Path.Combine(resourcesDir, "mcp_debug.log");
                }
                
                // Fallback to current directory if no project found
                return System.IO.Path.Combine(Environment.CurrentDirectory, "mcp_debug.log");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting debug log path, using fallback");
                return System.IO.Path.Combine(Environment.CurrentDirectory, "mcp_debug.log");
            }
        }

        private string GetAISampleImportLogPath()
        {
            try
            {
                // Use the project directory if available
                if (!string.IsNullOrEmpty(_projectDirectory))
                {
                    string resourcesDir = System.IO.Path.Combine(_projectDirectory, "resources");
                    
                    if (!System.IO.Directory.Exists(resourcesDir))
                    {
                        System.IO.Directory.CreateDirectory(resourcesDir);
                    }
                    
                    return System.IO.Path.Combine(resourcesDir, "AI_Sample_Import.log");
                }
                
                // Fallback to current directory if no project found
                return System.IO.Path.Combine(Environment.CurrentDirectory, "AI_Sample_Import.log");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AI sample import log path, using fallback");
                return System.IO.Path.Combine(Environment.CurrentDirectory, "AI_Sample_Import.log");
            }
        }

        public static void SetLastError(string error, Exception? exception = null)
        {
            _lastError = error;
            _lastException = exception;
        }

        /// <summary>
        /// Gets a module by name with proper error handling and helpful messages
        /// </summary>
        private (IModule? module, string? error) GetModuleByName(string? moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                var availableModules = _model.Root.GetModules()
                    .Where(m => m != null && !m.FromAppStore)
                    .Select(m => m.Name)
                    .ToList();

                var errorMessage = JsonSerializer.Serialize(new
                {
                    error = "Module name is required",
                    message = "Please provide a 'module_name' parameter",
                    available_modules = availableModules,
                    hint = "Use the list_modules tool to see all available modules",
                    example = new { module_name = availableModules.FirstOrDefault() ?? "MyFirstModule" }
                });

                return (null, errorMessage);
            }

            var modules = _model.Root.GetModules();
            var module = modules.FirstOrDefault(m => m?.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase) == true);

            if (module == null)
            {
                var availableModules = modules
                    .Where(m => m != null && !m.FromAppStore)
                    .Select(m => m.Name)
                    .ToList();

                var errorMessage = JsonSerializer.Serialize(new
                {
                    error = $"Module '{moduleName}' not found",
                    message = "The specified module does not exist in the project",
                    available_modules = availableModules,
                    hint = "Use the list_modules tool to see all available modules"
                });

                return (null, errorMessage);
            }

            return (module, null);
        }

    public async Task<object> SaveData(JsonObject arguments)
    {
        try
        {
            if (_model == null)
            {
                var errorMessage = "IModel instance is null in SaveData.";
                _logger.LogError(errorMessage);
                SetLastError(errorMessage);
                return JsonSerializer.Serialize(new { error = errorMessage, success = false });
            }

            var moduleName = arguments["module_name"]?.ToString();
            var dataProperty = arguments["data"]?.AsObject();
            
            if (dataProperty == null)
            {
                // Get module with validation for error message
                var (moduleForError, errorMsg) = GetModuleByName(moduleName);
                var moduleNameForExample = moduleForError?.Name ?? moduleName ?? "MyFirstModule";
                    
                var emptyDataError = "Invalid request format or empty data. The save_data tool is used to generate sample data for Mendix domain models.";
                SetLastError(emptyDataError);
                return JsonSerializer.Serialize(new { 
                    error = emptyDataError,
                        message = "The save_data tool requires a 'data' property with entity data in the specified format.",
                        required_format = new {
                            data = new {
                                CustomerEntity = new[] {
                                    new {
                                        VirtualId = "CUST001",
                                        FirstName = "John",
                                        LastName = "Doe",
                                        Email = "john.doe@example.com"
                                    }
                                },
                                OrderEntity = new[] {
                                    new {
                                        VirtualId = "ORD001",
                                        OrderDate = "2023-11-01T10:30:00Z",
                                        TotalAmount = 99.99,
                                        Customer = new {
                                            VirtualId = "CUST001"
                                        }
                                    }
                                }
                            }
                        },
                        format_notes = new {
                            entity_naming = $"Use '{moduleNameForExample}.EntityName' format for entity keys (e.g., '{moduleNameForExample}.Customer')",
                            virtual_id = "Include a unique VirtualId for each record to establish relationships",
                            relationships = "Reference related entities using their VirtualId in nested objects",
                            dates = "Use ISO 8601 format for dates (YYYY-MM-DDTHH:MM:SSZ)"
                        },
                        purpose = "This tool generates realistic sample data for testing and development purposes.",
                        success = false
                    });
                }

                // Get module with validation
                var (module, error) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return error!;
                }
                
                if (module.DomainModel == null)
                {
                    var errorMessage = $"Module '{module.Name}' does not have a domain model.";
                    SetLastError(errorMessage);
                    return JsonSerializer.Serialize(new { 
                        error = errorMessage,
                        success = false
                    });
                }

                // Validate the data structure
                var validationResult = ValidateDataStructure(dataProperty, module);
                if (!validationResult.IsValid)
                {
                    SetLastError(validationResult.Message);
                    return JsonSerializer.Serialize(new { 
                        error = validationResult.Message,
                        details = validationResult.Details,
                        success = false
                    });
                }

                // Save the data to a JSON file
                var saveResult = await SaveDataToFile(dataProperty);
                if (!saveResult.Success)
                {
                    SetLastError(saveResult.ErrorMessage ?? "Unknown error occurred while saving data");
                    return JsonSerializer.Serialize(new { 
                        error = saveResult.ErrorMessage,
                        success = false
                    });
                }

                return JsonSerializer.Serialize(new { 
                    success = true, 
                    message = $"Data validated and saved successfully for module '{module.Name}'",
                    module = module.Name,
                    file_path = saveResult.FilePath,
                    entities_processed = validationResult.EntitiesProcessed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving data");
                SetLastError("Error saving data", ex);
                return JsonSerializer.Serialize(new { 
                    error = ex.Message,
                    success = false
                });
            }
        }

    public async Task<object> GenerateOverviewPages(JsonObject arguments)
    {
        try
        {
            if (_model == null)
            {
                var errorMessage = "IModel instance is null in GenerateOverviewPages.";
                _logger.LogError(errorMessage);
                SetLastError(errorMessage);
                return JsonSerializer.Serialize(new { error = errorMessage, success = false });
            }

            var moduleName = arguments["module_name"]?.ToString();
            var entityNamesArray = arguments["entity_names"]?.AsArray();
                var generateIndexSnippet = arguments["generate_index_snippet"]?.GetValue<bool>() ?? true;

                if (entityNamesArray == null || !entityNamesArray.Any())
                {
                    return JsonSerializer.Serialize(new { 
                        error = "Invalid request format or no entity names provided",
                        success = false
                    });
                }

                var entityNames = entityNamesArray
                    .Select(node => node?.ToString())
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                if (!entityNames.Any())
                {
                    return JsonSerializer.Serialize(new { 
                        error = "No valid entity names provided",
                        success = false
                    });
                }

                // Get module with validation
                var (module, error) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return error!;
                }
                
                if (module.DomainModel == null)
                {
                    return JsonSerializer.Serialize(new { 
                        error = $"Module '{module.Name}' does not have a domain model",
                        success = false
                    });
                }

                // Get all entities from the domain model
                var allEntities = module.DomainModel.GetEntities().ToList();
                
                // Filter entities based on the requested names
                var entitiesToGenerate = allEntities
                    .Where(e => entityNames.Contains(e.Name, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (!entitiesToGenerate.Any())
                {
                    return JsonSerializer.Serialize(new { 
                        error = "None of the requested entities were found in the domain model",
                        success = false,
                        available_entities = allEntities.Select(e => e.Name).ToArray()
                    });
                }

                // Generate overview pages using the injected service
                var generatedOverviewPages = _pageGenerationService.GenerateOverviewPages(
                    module,
                    entitiesToGenerate,
                    generateIndexSnippet
                );

                // Add pages to navigation using the injected service
                var overviewPages = generatedOverviewPages
                    .Where(page => page.Name.Contains("overview", StringComparison.InvariantCultureIgnoreCase))
                    .Select(page => (page.Name, page))
                    .ToArray();

                _navigationManagerService.PopulateWebNavigationWith(
                    _model,
                    overviewPages
                );

                return JsonSerializer.Serialize(new { 
                    success = true,
                    message = $"Successfully generated {overviewPages.Length} overview pages for module '{module.Name}'",
                    module = module.Name,
                    generated_pages = overviewPages.Select(p => p.Name).ToArray(),
                    entities_processed = entitiesToGenerate.Select(e => e.Name).ToArray()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating overview pages");
                SetLastError("Error generating overview pages", ex);
                return JsonSerializer.Serialize(new { 
                    error = ex.Message,
                    success = false
                });
            }
        }

    public async Task<object> ListMicroflows(JsonObject arguments)
    {
        try
        {
            if (_model == null)
            {
                var errorMessage = "IModel instance is null in ListMicroflows.";
                _logger.LogError(errorMessage);
                SetLastError(errorMessage);
                return JsonSerializer.Serialize(new { error = errorMessage });
            }

            var moduleName = arguments["module_name"]?.ToString();
            
            // Get module with validation
            var (module, error) = GetModuleByName(moduleName);
            if (module == null)
            {
                return error!;
            }

            // Use IProject.GetModuleDocuments instead of module.GetDocuments()
            var microflows = _model.Root.GetModuleDocuments<IMicroflow>(module)
                .Select(mf => new
                {
                    name = mf.Name,
                    module = module.Name,
                    qualifiedName = mf.QualifiedName?.FullName
                }).ToArray();

            return JsonSerializer.Serialize(new { 
                success = true,
                module = module.Name,
                microflowCount = microflows.Length,
                microflows = microflows 
            });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing microflows");
                SetLastError("Error listing microflows", ex);
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

    public async Task<object> ReadMicroflowDetails(JsonObject arguments)
    {
        try
        {
            if (_model == null)
            {
                var errorMessage = "IModel instance is null in ReadMicroflowDetails.";
                _logger.LogError(errorMessage);
                SetLastError(errorMessage);
                return JsonSerializer.Serialize(new { error = errorMessage });
            }

            var moduleName = arguments["module_name"]?.ToString();
            var microflowName = arguments["microflow_name"]?.ToString();
            
            if (string.IsNullOrEmpty(microflowName))
            {
                var errorMessage = "Microflow name is required";
                SetLastError(errorMessage);
                return JsonSerializer.Serialize(new { error = errorMessage });
            }

            // Get module with validation
            var (module, error) = GetModuleByName(moduleName);
            if (module == null)
            {
                return error!;
            }

            // Find the microflow using IProject.GetModuleDocuments
                var microflow = _model.Root.GetModuleDocuments<IMicroflow>(module)
                    .FirstOrDefault(mf => mf.Name.Equals(microflowName, StringComparison.OrdinalIgnoreCase));

                if (microflow == null)
                {
                    var errorMessage = $"Microflow '{microflowName}' not found in module '{module.Name}'";
                    SetLastError(errorMessage);
                    return JsonSerializer.Serialize(new { error = errorMessage });
                }

                // Get microflow service to analyze activities
                var microflowService = _serviceProvider?.GetService<IMicroflowService>();
                var activitiesInfo = new List<object>();
                
                if (microflowService != null)
                {
                    try
                    {
                        var activities = microflowService.GetAllMicroflowActivities(microflow);
                        for (int i = 0; i < activities.Count; i++)
                        {
                            var activity = activities[i];
                            activitiesInfo.Add(new
                            {
                                position = i + 1, // 1-based position
                                index = i, // 0-based index
                                type = activity.GetType().Name,
                                activityId = activity.GetHashCode()
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not retrieve activity details for microflow analysis");
                    }
                }

                // Extract basic microflow information
                var microflowInfo = new
                {
                    name = microflow.Name,
                    qualifiedName = microflow.QualifiedName?.FullName ?? "Unknown",
                    module = module.Name,
                    returnType = microflow.ReturnType?.GetType().Name ?? "Void",
                    returnTypeFullName = microflow.ReturnType?.GetType().FullName ?? "Void",
                    activityCount = activitiesInfo.Count,
                    activities = activitiesInfo,
                    // Note: Advanced activity analysis requires IMicroflowService which is not available
                    limitations = activitiesInfo.Any() 
                        ? "Basic activity information available. Use read_microflow_activities API for detailed analysis."
                        : "Detailed activity analysis requires additional Mendix services not currently available in this MCP implementation"
                };

                return JsonSerializer.Serialize(new 
                { 
                    success = true,
                    microflow = microflowInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading microflow details");
                SetLastError("Error reading microflow details", ex);
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        public async Task<object> ReadMicroflowActivities(JsonObject arguments)
        {
            try
            {
                if (_model == null)
                {
                    var errorMessage = "IModel instance is null in ReadMicroflowActivities.";
                    _logger.LogError(errorMessage);
                    SetLastError(errorMessage);
                    return JsonSerializer.Serialize(new { error = errorMessage });
                }

                var moduleName = arguments["module_name"]?.ToString();
                var microflowName = arguments["microflow_name"]?.ToString();
                
                if (string.IsNullOrEmpty(microflowName))
                {
                    var errorMessage = "Microflow name is required";
                    SetLastError(errorMessage);
                    return JsonSerializer.Serialize(new { error = errorMessage });
                }

                // Get module with validation
                var (module, error) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return error!;
                }

                // Find the microflow
                var microflow = _model.Root.GetModuleDocuments<IMicroflow>(module)
                    .FirstOrDefault(mf => mf.Name.Equals(microflowName, StringComparison.OrdinalIgnoreCase));

                if (microflow == null)
                {
                    var errorMessage = $"Microflow '{microflowName}' not found in module '{module.Name}'";
                    SetLastError(errorMessage);
                    return JsonSerializer.Serialize(new { error = errorMessage });
                }

                // Get microflow service
                var microflowService = _serviceProvider?.GetService<IMicroflowService>();
                
                if (microflowService == null)
                {
                    return JsonSerializer.Serialize(new { 
                        error = "IMicroflowService not available",
                        success = false 
                    });
                }

                // Get all activities with detailed information
                var activities = microflowService.GetAllMicroflowActivities(microflow);
                var activitiesList = new List<object>();
                
                for (int i = 0; i < activities.Count; i++)
                {
                    var activity = activities[i];
                    var activityType = activity.GetType();
                    var activityInfo = new Dictionary<string, object>
                    {
                        ["position"] = i + 1, // 1-based position
                        ["index"] = i, // 0-based index
                        ["activityId"] = activity.GetHashCode(),
                        ["activityType"] = activityType.Name,
                        ["activityFullType"] = activityType.FullName ?? "Unknown"
                    };

                    // Extract caption if available
                    var captionProperty = activityType.GetProperty("Caption");
                    if (captionProperty != null)
                    {
                        var captionValue = captionProperty.GetValue(activity);
                        if (captionValue != null)
                        {
                            activityInfo["caption"] = captionValue.ToString();
                        }
                    }

                    // Extract action details if it's an IActionActivity
                    if (activity is IActionActivity actionActivity)
                    {
                        activityInfo["disabled"] = actionActivity.Disabled;
                        
                        if (actionActivity.Action != null)
                        {
                            activityInfo["actionType"] = actionActivity.Action.GetType().Name;
                            
                            // Extract specific action properties
                            var actionType = actionActivity.Action.GetType();
                            var actionProps = new Dictionary<string, object>();
                            
                            // Try to get common properties
                            foreach (var prop in actionType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                            {
                                try
                                {
                                    if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                                    {
                                        var value = prop.GetValue(actionActivity.Action);
                                        if (value != null)
                                        {
                                            actionProps[prop.Name] = value.ToString() ?? "";
                                        }
                                    }
                                }
                                catch
                                {
                                    // Skip properties that throw exceptions
                                }
                            }
                            
                            if (actionProps.Any())
                            {
                                activityInfo["actionProperties"] = actionProps;
                            }
                        }
                    }

                    activitiesList.Add(activityInfo);
                }

                // Get input parameters
                var inputParameters = microflowService.GetParameters(microflow)
                    .Select(param => new
                    {
                        name = param.Name,
                        dataType = param.Type?.GetType().Name ?? "Unknown",
                        typeFullName = param.Type?.GetType().FullName ?? "Unknown"
                    })
                    .ToList();

                // Get return type
                var returnInfo = new
                {
                    returnType = microflow.ReturnType?.GetType().Name ?? "Void",
                    returnTypeFullName = microflow.ReturnType?.GetType().FullName ?? "Void"
                };

                return JsonSerializer.Serialize(new 
                { 
                    success = true,
                    microflow = new
                    {
                        name = microflow.Name,
                        qualifiedName = microflow.QualifiedName?.FullName ?? "Unknown",
                        module = module.Name,
                        inputParameters = inputParameters,
                        returnInfo = returnInfo,
                        activityCount = activities.Count,
                        activities = activitiesList
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading microflow activities");
                SetLastError("Error reading microflow activities", ex);
                return JsonSerializer.Serialize(new { 
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    success = false 
                });
            }
        }

        public async Task<object> GetLastError(JsonObject arguments)
        {
            try
            {
                if (string.IsNullOrEmpty(_lastError))
                {
                    return JsonSerializer.Serialize(new { 
                        message = "No errors recorded",
                        last_error = (string?)null
                    });
                }

                return JsonSerializer.Serialize(new { 
                    message = "Last error retrieved",
                    last_error = _lastError,
                    details = _lastException?.Message,
                    stack_trace = _lastException?.StackTrace,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last error");
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        public async Task<object> GetProjectErrors(JsonObject arguments)
        {
            try
            {
                if (_model == null)
                {
                    return JsonSerializer.Serialize(new { 
                        error = "IModel instance is null",
                        success = false 
                    });
                }

                // Note: The Mendix Extensions API doesn't provide direct access to project errors/consistency checks.
                // This is a limitation that would require workarounds like:
                // 1. Parsing error output from build logs
                // 2. Implementing custom validation rules
                // 3. Using specific diagnostic tools like diagnose_associations

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Direct error retrieval not supported by Mendix Extensions API",
                    message = "The Mendix Studio Pro Extensions API does not expose a method to programmatically retrieve project errors or consistency checks.",
                    workarounds = new
                    {
                        option1 = new
                        {
                            description = "Use diagnostic tools to check for specific issues",
                            tools = new[]
                            {
                                "diagnose_associations - Check association configuration issues",
                                "list_enumerations - Verify enumeration definitions",
                                "read_domain_model - Validate entity and attribute structures"
                            }
                        },
                        option2 = new
                        {
                            description = "Check for common naming issues",
                            note = "Reserved words like 'CreatedDate', 'ChangedDate', 'Owner', etc. should not be used as attribute names"
                        },
                        option3 = new
                        {
                            description = "Manual verification",
                            note = "Check the Errors panel in Mendix Studio Pro (View -> Errors)"
                        }
                    },
                    common_errors = new[]
                    {
                        new { code = "CE7247", message = "Attribute name is a reserved word", solution = "Rename the attribute to avoid reserved words" },
                        new { code = "CE0552", message = "Entity has no generalization or attributes", solution = "Add attributes or set generalization" },
                        new { code = "CE0103", message = "Association owner must be 'Both'", solution = "Change association owner property" }
                    },
                    api_limitation = "This is a known limitation of the Mendix Studio Pro Extensions API v8.0. Error checking can only be done by creating custom ConsistencyCheckExtension implementations, not by reading existing errors."
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetProjectErrors");
                SetLastError("Error getting project errors", ex);
                return JsonSerializer.Serialize(new { error = ex.Message, details = ex.ToString() });
            }
        }

        public async Task<object> ListAvailableTools(JsonObject arguments)
        {
            try
            {
                var tools = new[]
                {
                    "read_domain_model",
                    "create_entity",
                    "modify_entity",
                    "update_entity_layout",
                    "manage_annotations",
                    "create_association",
                    "delete_model_element",
                    "diagnose_associations",
                    "create_multiple_entities",
                    "create_multiple_associations",
                    "create_domain_model_from_schema",
                    "save_data",
                    "generate_overview_pages",
                    "list_microflows",
                    "list_modules",
                    "list_enumerations",
                    "get_last_error",
                    "get_project_errors",
                    "list_available_tools",
                    "debug_info",
                    "read_microflow_details",
                    "read_microflow_activities",
                    "add_create_object_activity",
                    "add_change_object_activity",
                    "create_microflow",
                    "create_microflow_activity",
                    "create_microflow_activities_sequence",
                    "add_pages_to_navigation",
                    "list_navigation_items",
                    "remove_pages_from_navigation",
                    "list_page_properties",
                    "rename_page"
                };

                return JsonSerializer.Serialize(new { available_tools = tools });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing available tools");
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lists all navigation items across all navigation profiles
        /// </summary>
        public async Task<string> ListNavigationItems(JsonObject parameters)
        {
            try
            {
                // Explore what navigation-related documents exist
                var allModules = _model.Root.GetModules().ToList();
                var explorationResults = new List<object>();

                foreach (var module in allModules)
                {
                    var documents = _model.Root.GetModuleDocuments(module).ToList();
                    var navDocs = documents
                        .Where(doc => doc.document.GetType().Name.Contains("Navigation", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (navDocs.Any())
                    {
                        explorationResults.Add(new
                        {
                            module_name = module.Name,
                            navigation_documents = navDocs.Select(d => new
                            {
                                type = d.documentType.Name,
                                full_type = d.documentType.FullName,
                                document_name = d.document.Name
                            }).ToArray()
                        });
                    }
                }

                var result = new
                {
                    success = true,
                    message = "Navigation API exploration",
                    module_count = allModules.Count,
                    modules_with_navigation = explorationResults.ToArray(),
                    note = "Exploring navigation document types available in Extensions API"
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    stack_trace = ex.StackTrace,
                    success = false
                });
            }
        }

        /// <summary>
        /// Removes specified pages from navigation profiles
        /// </summary>
        public async Task<string> RemovePagesFromNavigation(JsonObject parameters)
        {
            try
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "❌ API LIMITATION: Navigation item removal not yet implemented",
                    message = "The Extensions API does not provide direct access to navigation profiles for item removal",
                    reason = "PopulateWebNavigationWith is the only available method - it only adds items",
                    status = "Under investigation",
                    workaround = "Manual removal required in Mendix Studio Pro: Navigation pane → Delete items"
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    stack_trace = ex.StackTrace,
                    success = false
                });
            }
        }

        /// <summary>
        /// Adds pages to the responsive web navigation profile
        /// </summary>
        public async Task<object> AddPagesToNavigation(JsonObject arguments)
        {
            try
            {
                if (_model == null)
                {
                    var error = "IModel instance is null in AddPagesToNavigation.";
                    _logger.LogError(error);
                    SetLastError(error);
                    return JsonSerializer.Serialize(new { error, success = false });
                }

                // Get parameters
                var moduleName = arguments["module_name"]?.ToString();
                var pageNamesArray = arguments["page_names"]?.AsArray();

                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = "Module name is required",
                        success = false,
                        example = new
                        {
                            module_name = "MyFirstModule",
                            page_names = new[] { "Customer_Overview", "Order_Overview" }
                        }
                    });
                }

                if (pageNamesArray == null || !pageNamesArray.Any())
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = "Page names array is required",
                        success = false,
                        example = new
                        {
                            module_name = "MyFirstModule",
                            page_names = new[] { "Customer_Overview", "Order_Overview" }
                        }
                    });
                }

                var pageNames = pageNamesArray
                    .Select(node => node?.ToString())
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                if (!pageNames.Any())
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = "No valid page names provided",
                        success = false
                    });
                }

                // Get module
                var (module, moduleError) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = moduleError,
                        success = false
                    });
                }

                // Find the pages in the module (search recursively through all folders)
                var allPages = _model.Root.GetModuleDocuments<IPage>(module)
                    .ToList();

                if (!allPages.Any())
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = $"No pages found in module '{moduleName}'",
                        success = false,
                        hint = "Create pages first using generate_overview_pages or manually in Studio Pro"
                    });
                }

                // Filter pages based on requested names
                var pagesToAdd = allPages
                    .Where(p => pageNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    .Select(p => (p.Name, p))
                    .ToArray();

                if (!pagesToAdd.Any())
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = "None of the requested pages were found in the module",
                        success = false,
                        requested_pages = pageNames.ToArray(),
                        available_pages = allPages.Select(p => p.Name).ToArray(),
                        hint = "Page names are case-insensitive but must match exactly"
                    });
                }

                // Check for pages that weren't found
                var foundPageNames = pagesToAdd.Select(p => p.Name).ToList();
                var notFoundPages = pageNames
                    .Where(name => !foundPageNames.Any(fpn => fpn.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                // Add pages to navigation using the service
                // NOTE: The Extensions API doesn't provide a way to check for duplicates before adding
                // PopulateWebNavigationWith will add items even if they already exist in navigation
                _navigationManagerService.PopulateWebNavigationWith(
                    _model,
                    pagesToAdd
                );

                var result = new
                {
                    success = true,
                    message = $"Successfully added {pagesToAdd.Length} page(s) to navigation",
                    module = module.Name,
                    added_pages = pagesToAdd.Select(p => p.Name).ToArray(),
                    not_found = notFoundPages.Any() ? notFoundPages.ToArray() : null,
                    note = "⚠️ API does not support duplicate checking - pages may appear multiple times in navigation if added repeatedly. Use list_navigation_items to explore what exists.",
                    workaround = "To prevent duplicates, manually check navigation in Studio Pro before adding pages"
                };

                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding pages to navigation");
                SetLastError("Error adding pages to navigation", ex);
                return JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    success = false,
                    stack_trace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Lists available page information (read-only exploration)
        /// </summary>
        public async Task<string> ListPageProperties(JsonObject parameters)
        {
            try
            {
                if (_model == null)
                {
                    return JsonSerializer.Serialize(new { error = "IModel instance is null", success = false });
                }

                var moduleName = parameters["module_name"]?.ToString();
                var pageName = parameters["page_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(moduleName) || string.IsNullOrWhiteSpace(pageName))
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = "Both module_name and page_name are required",
                        success = false,
                        example = new { module_name = "MyFirstModule", page_name = "Home" }
                    });
                }

                // Get module
                var (module, moduleError) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return JsonSerializer.Serialize(new { error = moduleError, success = false });
                }

                // Find page
                var page = _model.Root.GetModuleDocuments<IPage>(module)
                    .FirstOrDefault(p => p.Name.Equals(pageName, StringComparison.OrdinalIgnoreCase));

                if (page == null)
                {
                    var availablePages = _model.Root.GetModuleDocuments<IPage>(module)
                        .Select(p => p.Name)
                        .ToArray();
                    
                    return JsonSerializer.Serialize(new
                    {
                        error = $"Page '{pageName}' not found in module '{moduleName}'",
                        success = false,
                        available_pages = availablePages
                    });
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    module = moduleName,
                    page_name = page.Name,
                    note = "⚠️ API LIMITATION: IPage interface only exposes Name property",
                    limitations = new
                    {
                        message = "The Extensions API does not provide access to page properties like title, URL, layout, or widgets",
                        reason = "IPage only inherits from IDocument (which has Name property)",
                        untyped_model = "The untyped model API (IModelProperty) has read-only Value property",
                        what_works = "✅ You can rename pages using rename_page tool"
                    },
                    workaround = "To modify page properties: Open page in Studio Pro → Edit properties manually"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing page properties");
                return JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    stack_trace = ex.StackTrace,
                    success = false
                });
            }
        }

        /// <summary>
        /// Renames a page (only supported page modification operation)
        /// </summary>
        public async Task<string> RenamePage(JsonObject parameters)
        {
            try
            {
                if (_model == null)
                {
                    return JsonSerializer.Serialize(new { error = "IModel instance is null", success = false });
                }

                var moduleName = parameters["module_name"]?.ToString();
                var pageName = parameters["page_name"]?.ToString();
                var newName = parameters["new_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(moduleName) || string.IsNullOrWhiteSpace(pageName) || string.IsNullOrWhiteSpace(newName))
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = "module_name, page_name, and new_name are all required",
                        success = false,
                        example = new
                        {
                            module_name = "MyFirstModule",
                            page_name = "Home",
                            new_name = "HomePage"
                        }
                    });
                }

                // Get module
                var (module, moduleError) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return JsonSerializer.Serialize(new { error = moduleError, success = false });
                }

                // Find page
                var page = _model.Root.GetModuleDocuments<IPage>(module)
                    .FirstOrDefault(p => p.Name.Equals(pageName, StringComparison.OrdinalIgnoreCase));

                if (page == null)
                {
                    var availablePages = _model.Root.GetModuleDocuments<IPage>(module)
                        .Select(p => p.Name)
                        .ToArray();
                    
                    return JsonSerializer.Serialize(new
                    {
                        error = $"Page '{pageName}' not found in module '{moduleName}'",
                        success = false,
                        available_pages = availablePages
                    });
                }

                // Check if new name already exists
                var existingPage = _model.Root.GetModuleDocuments<IPage>(module)
                    .FirstOrDefault(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));

                if (existingPage != null)
                {
                    return JsonSerializer.Serialize(new
                    {
                        error = $"A page named '{newName}' already exists in module '{moduleName}'",
                        success = false
                    });
                }

                // Rename the page
                using var transaction = _model.StartTransaction($"Rename page '{pageName}' to '{newName}'");
                var oldName = page.Name;
                page.Name = newName;
                transaction.Commit();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Successfully renamed page from '{oldName}' to '{newName}'",
                    module = moduleName,
                    old_name = oldName,
                    new_name = newName,
                    note = "This is the ONLY page modification operation supported by the Extensions API"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming page");
                return JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    stack_trace = ex.StackTrace,
                    success = false
                });
            }
        }

    public async Task<object> ListModules(JsonObject arguments)
 {
          try
    {
                if (_model == null)
         {
           var error = "IModel instance is null in ListModules.";
         _logger.LogError(error);
   SetLastError(error);
       return JsonSerializer.Serialize(new { error });
 }

   var modules = _model.Root.GetModules().ToList();
     
   if (!modules.Any())
       {
           var warning = "No modules found in the project.";
   _logger.LogWarning(warning);
        return JsonSerializer.Serialize(new { 
  warning = warning,
    modules = new object[0]
       });
      }

      var moduleInfo = modules.Select(module => 
      {
          try
          {
              var entityCount = module.DomainModel?.GetEntities()?.Count() ?? 0;
              
              // Get document count safely - use generic version
              int documentCount = 0;
              try
              {
                  var documents = _model.Root.GetModuleDocuments<IDocument>(module);
                  documentCount = documents?.Count() ?? 0;
              }
              catch
              {
                  documentCount = 0;
              }

              return new
              {
                  name = module.Name,
                  fromAppStore = module.FromAppStore,
                  hasDomainModel = module.DomainModel != null,
                  entityCount = entityCount,
                  documentCount = documentCount,
                  error = (string?)null
              };
          }
          catch (Exception ex)
          {
              _logger.LogWarning(ex, $"Error getting info for module {module.Name}");
              return new
              {
                  name = module.Name,
                  fromAppStore = module.FromAppStore,
                  hasDomainModel = false,
                  entityCount = 0,
                  documentCount = 0,
                  error = (string?)"Failed to retrieve complete module info"
              };
          }
      }).ToArray();

            return JsonSerializer.Serialize(new { 
   success = true,
        totalModules = modules.Count,
          modules = moduleInfo 
                });
      }
catch (Exception ex)
      {
   _logger.LogError(ex, "Error listing modules");
           SetLastError("Error listing modules", ex);
         return JsonSerializer.Serialize(new { error = ex.Message });
       }
        }

        public async Task<string> ListEnumerations(JsonObject parameters)
        {
            try
            {
                var moduleName = parameters["module_name"]?.ToString();
                
                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    var availableModules = _model.Root.GetModules()
                        .Where(m => m != null && !m.FromAppStore)
                        .Select(m => m.Name)
                        .ToList();

                    return JsonSerializer.Serialize(new
                    {
                        error = "Module name is required",
                        message = "Please provide a 'module_name' parameter",
                        available_modules = availableModules,
                        hint = "Use the list_modules tool to see all available modules",
                        example = new { module_name = availableModules.FirstOrDefault() ?? "MyFirstModule" }
                    });
                }

                var modules = _model.Root.GetModules();
                var module = modules.FirstOrDefault(m => m?.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase) == true);

                if (module == null)
                {
                    var availableModules = modules
                        .Where(m => m != null && !m.FromAppStore)
                        .Select(m => m.Name)
                        .ToList();

                    return JsonSerializer.Serialize(new
                    {
                        error = $"Module '{moduleName}' not found",
                        message = "The specified module does not exist in the project",
                        available_modules = availableModules,
                        hint = "Use the list_modules tool to see all available modules"
                    });
                }

                // Get all enumerations from the module
                var enumerations = _model.Root.GetModuleDocuments<IEnumeration>(module).ToList();

                var enumerationInfo = enumerations.Select(enumeration =>
                {
                    try
                    {
                        var values = enumeration.GetValues()
                            .Select(v => new
                            {
                                name = v.Name,
                                caption = v.Caption?.ToString() ?? v.Name
                            })
                            .ToArray();

                        return new
                        {
                            name = enumeration.Name,
                            qualifiedName = enumeration.QualifiedName,
                            valueCount = values.Length,
                            values = values.Cast<object>().ToArray(),
                            error = (string?)null
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error getting details for enumeration {enumeration.Name}");
                        return new
                        {
                            name = enumeration.Name,
                            qualifiedName = enumeration.QualifiedName,
                            valueCount = 0,
                            values = new object[0],
                            error = (string?)"Failed to retrieve enumeration values"
                        };
                    }
                }).ToArray();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    moduleName = module.Name,
                    totalEnumerations = enumerations.Count,
                    enumerations = enumerationInfo
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing enumerations");
                SetLastError("Error listing enumerations", ex);
                return JsonSerializer.Serialize(new { error = ex.Message, details = ex.ToString() });
            }
        }

        public async Task<object> AddCreateObjectActivity(JsonObject arguments)
        {
            try
            {
                if (_model == null)
                {
                    return JsonSerializer.Serialize(new { error = "IModel instance is null", success = false });
                }

                using (var transaction = _model.StartTransaction("Add create object activity"))
                {
                    var moduleName = arguments["module_name"]?.ToString();
                    var microflowName = arguments["microflow_name"]?.ToString();
                    var entityName = arguments["entity_name"]?.ToString();
                    var outputVariable = arguments["output_variable"]?.ToString();
                    var insertPosition = arguments["insert_position"]?.ToString() ?? "start";
                    var commitType = arguments["commit"]?.ToString() ?? "No";
                    var refreshInClient = arguments["refresh_in_client"]?.GetValue<bool>() ?? false;

                    // Validate required parameters
                    if (string.IsNullOrEmpty(microflowName))
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Microflow name is required",
                            success = false 
                        });
                    }

                    if (string.IsNullOrEmpty(entityName))
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Entity name is required",
                            success = false 
                        });
                    }

                    if (string.IsNullOrEmpty(outputVariable))
                    {
                        outputVariable = $"New{entityName}";
                    }

                    // Get module with validation
                    var (module, error) = GetModuleByName(moduleName);
                    if (module == null)
                    {
                        return error!;
                    }

                    // Find the microflow
                    var microflow = _model.Root.GetModuleDocuments<IMicroflow>(module)
                        .FirstOrDefault(mf => mf.Name.Equals(microflowName, StringComparison.OrdinalIgnoreCase));

                    if (microflow == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = $"Microflow '{microflowName}' not found in module '{module.Name}'",
                            success = false 
                        });
                    }

                    // Find the entity
                    if (module.DomainModel == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = $"Module '{module.Name}' does not have a domain model",
                            success = false 
                        });
                    }

                    var entity = module.DomainModel.GetEntities()
                        .FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                    if (entity == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = $"Entity '{entityName}' not found in module '{module.Name}'",
                            success = false 
                        });
                    }

                    // Get services
                    var microflowService = _serviceProvider?.GetService<IMicroflowService>();
                    var activitiesService = _serviceProvider?.GetService<IMicroflowActivitiesService>();
                    
                    if (microflowService == null || activitiesService == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Required microflow services not available",
                            success = false 
                        });
                    }

                    // Map commit type
                    var commit = MapCommitType(commitType);

                    // Create the activity using the activities service
                    var createActivity = activitiesService.CreateCreateObjectActivity(
                        _model,
                        entity,
                        outputVariable,
                        commit,
                        refreshInClient
                    );

                    // Insert the activity after start
                    // NOTE: GetAllMicroflowActivities returns activities in undefined order per API docs:
                    // "Order and nesting of activities cannot be determined from the result"
                    // Therefore, we always insert after start to maintain sequential flow.
                    // Activities inserted this way will appear in the order they were added.
                    bool inserted = microflowService.TryInsertAfterStart(microflow, new[] { createActivity });

                    if (!inserted)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Failed to insert activity into microflow",
                            success = false 
                        });
                    }

                    transaction.Commit();

                    return JsonSerializer.Serialize(new { 
                        success = true,
                        message = $"Create object activity added to microflow '{microflowName}' in module '{module.Name}'",
                        microflow = microflowName,
                        module = module.Name,
                        entity = entityName,
                        output_variable = outputVariable,
                        commit_type = commitType
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding create object activity");
                SetLastError("Error adding create object activity", ex);
                return JsonSerializer.Serialize(new { 
                    error = ex.Message,
                    success = false 
                });
            }
        }

        public async Task<object> AddChangeObjectActivity(JsonObject arguments)
        {
            try
            {
                if (_model == null)
                {
                    return JsonSerializer.Serialize(new { error = "IModel instance is null", success = false });
                }

                using (var transaction = _model.StartTransaction("Add change object activity"))
                {
                    var moduleName = arguments["module_name"]?.ToString();
                    var microflowName = arguments["microflow_name"]?.ToString();
                    var objectVariable = arguments["object_variable"]?.ToString();
                    var changesArray = arguments["changes"]?.AsArray();
                    var insertPosition = arguments["insert_position"]?.ToString() ?? "start";
                    var commitType = arguments["commit"]?.ToString() ?? "No";
                    var refreshInClient = arguments["refresh_in_client"]?.GetValue<bool>() ?? false;

                    // Validate required parameters
                    if (string.IsNullOrEmpty(microflowName))
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Microflow name is required",
                            success = false 
                        });
                    }

                    if (string.IsNullOrEmpty(objectVariable))
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Object variable name is required (e.g., '$NewInstitution')",
                            success = false 
                        });
                    }

                    if (changesArray == null || !changesArray.Any())
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "At least one attribute change is required",
                            hint = "Provide changes as array: [{\"attribute\": \"CustomerNumber\", \"value\": \"$parameter/SingleSelectionNumber\"}]",
                            success = false 
                        });
                    }

                    // Get module with validation
                    var (module, error) = GetModuleByName(moduleName);
                    if (module == null)
                    {
                        return error!;
                    }

                    // Find the microflow
                    var microflow = _model.Root.GetModuleDocuments<IMicroflow>(module)
                        .FirstOrDefault(mf => mf.Name.Equals(microflowName, StringComparison.OrdinalIgnoreCase));

                    if (microflow == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = $"Microflow '{microflowName}' not found in module '{module.Name}'",
                            success = false 
                        });
                    }

                    // Get services
                    var microflowService = _serviceProvider?.GetService<IMicroflowService>();
                    var activitiesService = _serviceProvider?.GetService<IMicroflowActivitiesService>();
                    var expressionService = _serviceProvider?.GetService<IMicroflowExpressionService>();
                    
                    if (microflowService == null || activitiesService == null || expressionService == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Required microflow services not available",
                            success = false 
                        });
                    }

                    // Find the entity for the object variable to validate attributes
                    // Extract entity name from variable (e.g., "$NewInstitution" -> look for Institution entity)
                    var variableName = objectVariable.TrimStart('$');
                    
                    // Parse changes and find the entity
                    var changesList = new List<(string attribute, string value)>();
                    IEntity? targetEntity = null;
                    
                    foreach (var changeNode in changesArray)
                    {
                        var changeObj = changeNode?.AsObject();
                        if (changeObj != null)
                        {
                            var attrName = changeObj["attribute"]?.ToString();
                            var attrValue = changeObj["value"]?.ToString();
                            
                            if (!string.IsNullOrEmpty(attrName) && !string.IsNullOrEmpty(attrValue))
                            {
                                changesList.Add((attrName, attrValue));
                            }
                        }
                    }

                    if (!changesList.Any())
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "No valid attribute changes found",
                            success = false 
                        });
                    }

                    // Try to find the entity by looking for entities in the module
                    // Note: This is a best-effort approach since we can't easily determine the entity from a variable name
                    if (module.DomainModel != null)
                    {
                        var entities = module.DomainModel.GetEntities().ToList();
                        
                        // First, try to find an attribute that matches any entity
                        var firstAttributeName = changesList.First().attribute;
                        foreach (var entity in entities)
                        {
                            var attr = entity.GetAttributes().FirstOrDefault(a => 
                                a.Name.Equals(firstAttributeName, StringComparison.OrdinalIgnoreCase));
                            
                            if (attr != null)
                            {
                                targetEntity = entity;
                                break;
                            }
                        }
                    }

                    if (targetEntity == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = $"Could not determine entity type for variable '{objectVariable}'. Make sure the entity exists in module '{module.Name}'",
                            hint = "Ensure the object variable was created with a create object activity first",
                            success = false 
                        });
                    }

                    // Map commit type
                    var commit = MapCommitType(commitType);

                    // Create the change object activity for the first attribute
                    // In Mendix API, we need to create the activity for each attribute change
                    var firstChange = changesList.First();
                    var attribute = targetEntity.GetAttributes().FirstOrDefault(a => 
                        a.Name.Equals(firstChange.attribute, StringComparison.OrdinalIgnoreCase));
                    
                    if (attribute == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = $"Attribute '{firstChange.attribute}' not found in entity '{targetEntity.Name}'",
                            available_attributes = targetEntity.GetAttributes().Select(a => a.Name).ToArray(),
                            success = false 
                        });
                    }

                    // Create microflow expression from the value string
                    var expression = expressionService.CreateFromString(firstChange.value);

                    // Create the change attribute activity
                    var changeActivity = activitiesService.CreateChangeAttributeActivity(
                        _model,
                        attribute,
                        ChangeActionItemType.Set,
                        expression,
                        objectVariable,
                        commit
                    );

                    // Insert activity after start
                    // NOTE: GetAllMicroflowActivities returns activities in undefined order per API docs:
                    // "Order and nesting of activities cannot be determined from the result"
                    // Therefore, we always insert after start to maintain sequential flow.
                    // Activities inserted this way will appear in the order they were added.
                    bool inserted = microflowService.TryInsertAfterStart(microflow, changeActivity);

                    if (!inserted)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Failed to insert activity into microflow",
                            success = false 
                        });
                    }

                    transaction.Commit();

                    return JsonSerializer.Serialize(new { 
                        success = true,
                        message = $"Change object activity added to microflow '{microflowName}' in module '{module.Name}'",
                        microflow = microflowName,
                        module = module.Name,
                        object_variable = objectVariable,
                        changes_applied = changesList.Count,
                        changes = changesList.Select(c => new { attribute = c.attribute, value = c.value }).ToArray(),
                        commit_type = commitType
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding change object activity");
                SetLastError("Error adding change object activity", ex);
                return JsonSerializer.Serialize(new { 
                    error = ex.Message,
                    success = false 
                });
            }
        }

        public async Task<object> DebugInfo(JsonObject arguments)
        {
            try
            {
                if (_model == null)
                {
                    var errorMessage = "IModel instance is null in DebugInfo.";
                    _logger.LogError(errorMessage);
                    SetLastError(errorMessage);
                    return JsonSerializer.Serialize(new { error = errorMessage });
                }

                var moduleName = arguments["module_name"]?.ToString();
                
                // Get module with validation
                var (module, error) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return error!;
                }
                
                var response = new Dictionary<string, object>();

                if (module.DomainModel != null)
                {
                    var entities = module.DomainModel.GetEntities().ToList();
                    response["module"] = module.Name;
                    response["entityCount"] = entities.Count;
                    response["entities"] = entities.Select(e => new
                    {
                        Name = e.Name,
                        QualifiedName = $"{module.Name}.{e.Name}",
                        AttributeCount = e.GetAttributes().Count(),
                        Attributes = e.GetAttributes().Select(a => new
                        {
                            Name = a.Name,
                            Type = a.Type?.GetType().Name ?? "Unknown",
                            TypeDetails = a.Type?.ToString() ?? "Unknown"
                        }).ToList(),
                        LocationX = e.Location.X,
                        LocationY = e.Location.Y
                    }).ToList();

                    // Collect association information with detailed mapping
                    var allAssociations = new List<object>();
                    foreach (var entity in entities)
                    {
                        var associations = entity.GetAssociations(AssociationDirection.Both, null).ToList();
                        foreach (var association in associations)
                        {
                            allAssociations.Add(new
                            {
                                Name = association.Association.Name,
                                Parent = association.Parent.Name,
                                ParentQualifiedName = $"{module.Name}.{association.Parent.Name}",
                                Child = association.Child.Name,
                                ChildQualifiedName = $"{module.Name}.{association.Child.Name}",
                                Type = association.Association.Type.ToString(),
                                MappedType = association.Association.Type == AssociationType.Reference ? "one-to-many" : "many-to-many"
                            });
                        }
                    }
                    response["associations"] = allAssociations;
                    response["associationCount"] = allAssociations.Count;
                    
                    // Add comprehensive examples
                    response["examples"] = new
                    {
                        entityCreation = new
                        {
                            simple = new
                            {
                                entity_name = "Customer",
                                attributes = new[]
                                {
                                    new { name = "firstName", type = "String" },
                                    new { name = "lastName", type = "String" },
                                    new { name = "birthDate", type = "DateTime" },
                                    new { name = "isActive", type = "Boolean" }
                                }
                            },
                            withEnumeration = new
                            {
                                entity_name = "Product",
                                attributes = new object[]
                                {
                                    new { name = "productName", type = "String" },
                                    new { name = "price", type = "Decimal" },
                                    new
                                    {
                                        name = "status",
                                        type = "Enumeration",
                                        enumerationValues = new[] { "Available", "OutOfStock", "Discontinued" }
                                    }
                                }
                            }
                        },
                        associationCreation = new
                        {
                            oneToMany = new
                            {
                                name = "Customer_Orders",
                                parent = "Customer",
                                child = "Order",
                                type = "one-to-many"
                            },
                            manyToMany = new
                            {
                                name = "Product_Category",
                                parent = "Product",
                                child = "Category",
                                type = "many-to-many"
                            }
                        },
                        dataFormat = new
                        {
                            data = new
                            {
                                MyFirstModule_Customer = new[]
                                {
                                    new
                                    {
                                        VirtualId = "CUST001",
                                        firstName = "John",
                                        lastName = "Doe",
                                        birthDate = "1990-01-01T00:00:00Z",
                                        isActive = true
                                    }
                                }
                            }
                        }
                    };

                    // Add troubleshooting tips
                    response["troubleshooting"] = new
                    {
                        entityNamesList = entities.Select(e => e.Name).ToList(),
                        associationTips = new[] {
                            "Make sure both entities exist before creating an association",
                            "Use simple names without module prefixes in API calls",
                            "Check that association names are unique",
                            "For data operations, use VirtualId for relationship references"
                        },
                        commonIssues = new[] {
                            "Entity names are case sensitive",
                            "Enumeration attributes must have values defined",
                            "Associations require both parent and child entities to exist",
                            "Data validation requires proper JSON structure"
                        }
                    };
                }
                else
                {
                    response["error"] = $"Module '{module.Name}' does not have a domain model";
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Debug information for module '{module.Name}' retrieved successfully",
                    module = module.Name,
                    data = response,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving debug info");
                SetLastError("Error retrieving debug info", ex);
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }


        // TODO: Restore full implementations of these methods from version control
        private (bool IsValid, string Message, object? Details, int EntitiesProcessed) ValidateDataStructure(JsonObject dataProperty, IModule module)
        {
            // Stub implementation - needs to be restored
            _logger.LogWarning("ValidateDataStructure stub called - implementation needs to be restored");
    return (false, "Method not fully implemented - please restore from version control", null, 0);
        }

        private async Task<(bool Success, string? FilePath, string? ErrorMessage)> SaveDataToFile(JsonObject dataProperty)
     {
    // Stub implementation - needs to be restored
            _logger.LogWarning("SaveDataToFile stub called - implementation needs to be restored");
 return await Task.FromResult<(bool Success, string? FilePath, string? ErrorMessage)>((false, null, "Method not fully implemented - please restore from version control"));
        }

        // Stub implementations for activity creation methods
        private IActionActivity CreateLogActivity(JsonObject? config)
        {
        throw new NotImplementedException("CreateLogActivity needs to be restored from version control");
        }

     private IActionActivity CreateChangeVariableActivity(JsonObject? config)
        {
            throw new NotImplementedException("CreateChangeVariableActivity needs to be restored from version control");
    }

        private IActionActivity CreateCreateVariableActivity(JsonObject? config)
        {
  throw new NotImplementedException("CreateCreateVariableActivity needs to be restored from version control");
        }

    private IActionActivity CreateMicroflowCallActivity(JsonObject? config)
        {
throw new NotImplementedException("CreateMicroflowCallActivity needs to be restored from version control");
        }

        private IActionActivity CreateDatabaseRetrieveActivity(JsonObject? config)
      {
         throw new NotImplementedException("CreateDatabaseRetrieveActivity needs to be restored from version control");
        }

        private IActionActivity CreateAssociationRetrieveActivity(JsonObject? config)
        {
    throw new NotImplementedException("CreateAssociationRetrieveActivity needs to be restored from version control");
        }

        private IActionActivity CreateCommitActivity(JsonObject? config)
        {
    throw new NotImplementedException("CreateCommitActivity needs to be restored from version control");
  }

  private IActionActivity CreateRollbackActivity(JsonObject? config)
        {
     throw new NotImplementedException("CreateRollbackActivity needs to be restored from version control");
  }

        private IActionActivity CreateDeleteActivity(JsonObject? config)
        {
 throw new NotImplementedException("CreateDeleteActivity needs to be restored from version control");
        }

private IActionActivity CreateListActivity(JsonObject? config)
        {
        throw new NotImplementedException("CreateListActivity needs to be restored from version control");
        }

        private IActionActivity CreateChangeListActivity(JsonObject? config)
    {
     throw new NotImplementedException("CreateChangeListActivity needs to be restored from version control");
        }

        private IActionActivity CreateSortListActivity(JsonObject? config)
        {
  throw new NotImplementedException("CreateSortListActivity needs to be restored from version control");
        }

        private IActionActivity CreateFilterListActivity(JsonObject? config)
{
 throw new NotImplementedException("CreateFilterListActivity needs to be restored from version control");
        }

 private IActionActivity CreateFindInListActivity(JsonObject? config)
        {
   throw new NotImplementedException("CreateFindInListActivity needs to be restored from version control");
        }

  private IActionActivity CreateAggregateListActivity(JsonObject? config)
        {
   throw new NotImplementedException("CreateAggregateListActivity needs to be restored from version control");
        }

     private IActionActivity CreateJavaActionCallActivity(JsonObject? config)
      {
          throw new NotImplementedException("CreateJavaActionCallActivity needs to be restored from version control");
        }

      private IActionActivity CreateChangeAttributeActivity(JsonObject? config)
  {
     throw new NotImplementedException("CreateChangeAttributeActivity needs to be restored from version control");
 }

        private IActionActivity CreateChangeAssociationActivity(JsonObject? config)
        {
            throw new NotImplementedException("CreateChangeAssociationActivity needs to be restored from version control");
        }

        // Helper methods for microflow editing
        private CommitEnum MapCommitType(string? commitType)
        {
            if (string.IsNullOrEmpty(commitType))
            {
                return CommitEnum.No;
            }

            return commitType.ToLowerInvariant() switch
            {
                "yes" => CommitEnum.Yes,
                "yeswithoutevents" => CommitEnum.YesWithoutEvents,
                "no" => CommitEnum.No,
                _ => CommitEnum.No
            };
        }
    }
}
