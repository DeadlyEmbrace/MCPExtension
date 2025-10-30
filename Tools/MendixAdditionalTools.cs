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
using Mendix.StudioPro.ExtensionsAPI.Model.DomainModels;
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

    public async Task<object> SaveData(JsonObject arguments)
    {
        try
        {
            if (_model == null)
            {
                var error = "IModel instance is null in SaveData.";
                _logger.LogError(error);
                SetLastError(error);
                return JsonSerializer.Serialize(new { error, success = false });
            }

            var dataProperty = arguments["data"]?.AsObject();
            if (dataProperty == null)
            {
                var currentModule = Utils.Utils.GetMyFirstModule(_model);
                if (currentModule == null)
                {
                    var error = "No module found in SaveData.";
                    _logger.LogError(error);
                    SetLastError(error);
                    return JsonSerializer.Serialize(new { error, success = false });
                }
                var moduleName = currentModule?.Name ?? "MyFirstModule";
                    
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
                            entity_naming = $"Use '{moduleName}.EntityName' format for entity keys (e.g., '{moduleName}.Customer')",
                            virtual_id = "Include a unique VirtualId for each record to establish relationships",
                            relationships = "Reference related entities using their VirtualId in nested objects",
                            dates = "Use ISO 8601 format for dates (YYYY-MM-DDTHH:MM:SSZ)"
                        },
                        purpose = "This tool generates realistic sample data for testing and development purposes.",
                        success = false
                    });
                }

                var module = Utils.Utils.GetMyFirstModule(_model);
                if (module == null)
                {
                    var error = "No module found in SaveData.";
                    _logger.LogError(error);
                    SetLastError(error);
                    return JsonSerializer.Serialize(new { error, success = false });
                }
                if (module?.DomainModel == null)
                {
                    var error = "No domain model found.";
                    SetLastError(error);
                    return JsonSerializer.Serialize(new { 
                        error = error,
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
                    message = "Data validated and saved successfully",
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
                var error = "IModel instance is null in GenerateOverviewPages.";
                _logger.LogError(error);
                SetLastError(error);
                return JsonSerializer.Serialize(new { error, success = false });
            }

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

                var module = Utils.Utils.GetMyFirstModule(_model);
                if (module == null)
                {
                    var error = "No module found in GenerateOverviewPages.";
                    _logger.LogError(error);
                    SetLastError(error);
                    return JsonSerializer.Serialize(new { error, success = false });
                }
                if (module?.DomainModel == null)
                {
                    return JsonSerializer.Serialize(new { 
                        error = "No domain model found",
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
                    message = $"Successfully generated {overviewPages.Length} overview pages",
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
                var error = "IModel instance is null in ListMicroflows.";
                _logger.LogError(error);
                SetLastError(error);
                return JsonSerializer.Serialize(new { error });
            }

            var moduleName = arguments["module_name"]?.ToString();
            
            var module = Utils.Utils.GetMyFirstModule(_model);
            if (module == null)
            {
                var error = "No module found in ListMicroflows.";
                _logger.LogError(error);
                SetLastError(error);
                return JsonSerializer.Serialize(new { error });
            }

            if (!string.IsNullOrEmpty(moduleName) && module.Name != moduleName)
            {
                return JsonSerializer.Serialize(new { error = $"Module '{moduleName}' not found" });
            }

                var microflows = module.GetDocuments()
                    .OfType<IMicroflow>()
                    .Select(mf => new
                    {
                        name = mf.Name,
                        module = module.Name
                    }).ToArray();

                return JsonSerializer.Serialize(new { microflows = microflows });
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
                var error = "IModel instance is null in ReadMicroflowDetails.";
                _logger.LogError(error);
                SetLastError(error);
                return JsonSerializer.Serialize(new { error });
            }

            var microflowName = arguments["microflow_name"]?.ToString();
            
            if (string.IsNullOrEmpty(microflowName))
            {
                var error = "Microflow name is required";
                SetLastError(error);
                return JsonSerializer.Serialize(new { error = error });
            }

            var module = Utils.Utils.GetMyFirstModule(_model);
            if (module == null)
            {
                var error = "No module found in ReadMicroflowDetails.";
                _logger.LogError(error);
                SetLastError(error);
                return JsonSerializer.Serialize(new { error });
            }

            // Find the microflow
                var microflow = module.GetDocuments()
                    .OfType<IMicroflow>()
                    .FirstOrDefault(mf => mf.Name.Equals(microflowName, StringComparison.OrdinalIgnoreCase));

                if (microflow == null)
                {
                    var error = $"Microflow '{microflowName}' not found in module '{module.Name}'";
                    SetLastError(error);
                    return JsonSerializer.Serialize(new { error = error });
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

        public async Task<object> ListAvailableTools(JsonObject arguments)
        {
            try
            {
                var tools = new[]
                {
                    "read_domain_model",
                    "create_entity",
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
                    "get_last_error",
                    "list_available_tools",
                    "debug_info",
                    "read_microflow_details",
                    "create_microflow",
                    "create_microflow_activity",
                    "create_microflow_activities_sequence"
                };

                return JsonSerializer.Serialize(new { available_tools = tools });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing available tools");
                return JsonSerializer.Serialize(new { error = ex.Message });
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

      var moduleInfo = modules.Select(module => new
       {
    name = module.Name,
      fromAppStore = module.FromAppStore,
       hasDomainModel = module.DomainModel != null,
     entityCount = module.DomainModel?.GetEntities()?.Count() ?? 0,
       documentCount = module.GetDocuments()?.Count() ?? 0
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

        public async Task<object> DebugInfo(JsonObject arguments)
        {
            try
            {
                if (_model == null)
                {
                    var error = "IModel instance is null in DebugInfo.";
                    _logger.LogError(error);
                    SetLastError(error);
                    return JsonSerializer.Serialize(new { error });
                }

                var module = Utils.Utils.GetMyFirstModule(_model);
                if (module == null)
                {
                    var error = "No module found in DebugInfo.";
                    _logger.LogError(error);
                    SetLastError(error);
                    return JsonSerializer.Serialize(new { error });
                }
                var response = new Dictionary<string, object>();

                if (module?.DomainModel != null)
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
                    response["error"] = "No domain model found";
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Debug information retrieved successfully",
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
    }
}
