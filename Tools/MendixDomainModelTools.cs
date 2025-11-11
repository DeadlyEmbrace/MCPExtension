using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;
using Mendix.StudioPro.ExtensionsAPI.Model.DomainModels;
using Mendix.StudioPro.ExtensionsAPI.Model.Enumerations;
using Mendix.StudioPro.ExtensionsAPI.Model.Texts;
using Microsoft.Extensions.Logging;
using MCPExtension.Utils;

namespace MCPExtension.Tools
{
    public class MendixDomainModelTools
    {
        private readonly IModel _model;
        private readonly ILogger<MendixDomainModelTools> _logger;

        public MendixDomainModelTools(IModel model, ILogger<MendixDomainModelTools> logger)
        {
            _model = model;
            _logger = logger;
        }

        public async Task<string> ReadDomainModel(JsonObject parameters)
        {
            try
            {
                // Get the module name from parameters
                var moduleName = parameters["module_name"]?.ToString();
        
                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    return JsonSerializer.Serialize(new 
                    { 
                        error = "Module name is required",
                        message = "Please provide a 'module_name' parameter to read the domain model",
                        example = new {
                            module_name = "MyFirstModule"
                        }
                    });
                }

                // Find the specified module
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
                        message = $"The specified module does not exist in the project",
                        available_modules = availableModules,
                        hint = "Use the list_modules tool to see all available modules"
                    });
                }

                if (module.DomainModel == null)
                {
                    return JsonSerializer.Serialize(new 
                    { 
                        error = $"Module '{moduleName}' does not have a domain model",
                        message = "The specified module exists but does not contain a domain model"
                    });
                }

                var domainModel = module.DomainModel;
                var entities = domainModel.GetEntities().ToList();

                var modelData = new
                {
                    ModuleName = module.Name,
                    Entities = entities.Select(entity => new
                    {
                        Name = entity.Name,
                        QualifiedName = $"{module.Name}.{entity.Name}",
                        Attributes = GetEntityAttributes(entity),
                        Associations = GetEntityAssociations(entity, module)
                    }).ToList()
                };

                var result = new
                {
                    success = true,
                    message = $"Domain model for module '{moduleName}' retrieved successfully",
                    data = modelData,
                    status = "success"
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                return JsonSerializer.Serialize(result, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading domain model");
                return JsonSerializer.Serialize(new { error = "Failed to read domain model", details = ex.Message });
            }
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

            if (module.DomainModel == null)
            {
 var errorMessage = JsonSerializer.Serialize(new
    {
         error = $"Module '{moduleName}' does not have a domain model",
       message = "The specified module exists but does not contain a domain model"
    });

        return (null, errorMessage);
 }

          return (module, null);
        }

        public async Task<string> CreateEntity(JsonObject parameters)
        {
            try
            {
                using (var transaction = _model.StartTransaction("create entity"))
                {
                    var moduleName = parameters["module_name"]?.ToString();
                    var entityName = parameters["entity_name"]?.ToString();
                    var attributesArray = parameters["attributes"]?.AsArray();

                    // Get module with validation
 var (module, error) = GetModuleByName(moduleName);
         if (module == null)
{
     return error!;
      }

      // Extract persistable parameter (default to true for backward compatibility)
          bool persistable = true;
   if (parameters.ContainsKey("persistable"))
       {
  if (parameters["persistable"]?.AsValue().TryGetValue<bool>(out var persistableValue) == true)
       {
     persistable = persistableValue;
        }
             }

     // Extract entityType parameter (default to "persistent")
           string entityType = "persistent";
   if (parameters.ContainsKey("entityType"))
    {
             entityType = parameters["entityType"]?.ToString() ?? "persistent";
          }
// Handle backward compatibility: if persistable is false, use non-persistent
    else if (!persistable)
    {
    entityType = "non-persistent";
  }

          if (string.IsNullOrEmpty(entityName))
     {
  return JsonSerializer.Serialize(new { error = "Entity name is required" });
            }

          // Check if entity already exists
         var existingEntity = module.DomainModel.GetEntities()
  .FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

               if (existingEntity != null)
   {
      return JsonSerializer.Serialize(new { error = $"Entity '{entityName}' already exists in module '{moduleName}'" });
         }

         IEntity mxEntity;
           string displayEntityType = entityType;

 if (entityType != "persistent")
           {
     // Use template-based approach for special entity types
                   mxEntity = CreateEntityFromTemplate(module, entityName, attributesArray, entityType);
    if (mxEntity == null)
{
  return JsonSerializer.Serialize(new 
    { 
           error = $"Failed to create {entityType} entity. AIExtension.{GetTemplateName(entityType)} template not found or invalid.",
      details = $"Make sure the AIExtension module exists with a {GetTemplateName(entityType)} entity properly configured."
       });
          }
         }
    else
            {
      // Create regular persistent entity
    mxEntity = CreateEntityFromTemplate(module, entityName, attributesArray, "persistent");
    if (mxEntity == null)
    {
        return JsonSerializer.Serialize(new 
  { 
       error = "Failed to create persistent entity.",
     details = "Error occurred while creating the entity."
          });
            }
         }

     transaction.Commit();

     // Get attributes after commit to avoid collection modification errors
     var attributes = mxEntity.GetAttributes().ToList().Select(a => new
     {
         name = a.Name,
         type = a.Type?.GetType().Name ?? "Unknown"
     }).ToArray();

          return JsonSerializer.Serialize(new 
         { 
success = true, 
    message = $"Entity '{entityName}' created successfully in module '{moduleName}' as {displayEntityType}",
        entity = new
          {
   name = mxEntity.Name,
  module = moduleName,
  persistable = persistable,
    entityType = entityType,
   attributes = attributes
      }
         });
         }
            }
          catch (Exception ex)
 {
      _logger.LogError(ex, "Error creating entity");
  MendixAdditionalTools.SetLastError($"Failed to create entity: {ex.Message}", ex);
   return JsonSerializer.Serialize(new { error = $"Failed to create entity: {ex.Message}" });
            }
        }

        public async Task<string> ModifyEntity(JsonObject parameters)
        {
            try
            {
                using (var transaction = _model.StartTransaction("modify entity"))
                {
                    var moduleName = parameters["module_name"]?.ToString();
                    var entityName = parameters["entity_name"]?.ToString();

                    if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(entityName))
                    {
                        return JsonSerializer.Serialize(new { error = "module_name and entity_name are required" });
                    }

                    // Get module
                    var (module, error) = GetModuleByName(moduleName);
                    if (module == null)
                    {
                        return error!;
                    }

                    // Find the entity
                    var entity = module.DomainModel.GetEntities()
                        .FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                    if (entity == null)
                    {
                        return JsonSerializer.Serialize(new { error = $"Entity '{entityName}' not found in module '{moduleName}'" });
                    }

                    var changes = new List<string>();

                    // Handle adding new attributes
                    if (parameters.ContainsKey("add_attributes") && parameters["add_attributes"] is JsonArray addArray)
                    {
                        foreach (var attrNode in addArray)
                        {
                            var attrObj = attrNode?.AsObject();
                            if (attrObj == null) continue;

                            var attrName = attrObj["name"]?.ToString();
                            var attrType = attrObj["type"]?.ToString();

                            if (string.IsNullOrEmpty(attrName) || string.IsNullOrEmpty(attrType))
                            {
                                changes.Add($"⚠️ Skipped invalid attribute (missing name or type)");
                                continue;
                            }

                            // Check if attribute already exists
                            if (entity.GetAttributes().Any(a => a.Name.Equals(attrName, StringComparison.OrdinalIgnoreCase)))
                            {
                                changes.Add($"⚠️ Attribute '{attrName}' already exists, skipped");
                                continue;
                            }

                            // Create the attribute based on type
                            var success = CreateAttribute(entity, module, attrName, attrType, attrObj);
                            if (success)
                            {
                                changes.Add($"✅ Added attribute '{attrName}' ({attrType})");
                            }
                            else
                            {
                                changes.Add($"❌ Failed to add attribute '{attrName}'");
                            }
                        }
                    }

                    // Handle removing attributes
                    if (parameters.ContainsKey("remove_attributes") && parameters["remove_attributes"] is JsonArray removeArray)
                    {
                        foreach (var attrNode in removeArray)
                        {
                            var attrName = attrNode?.ToString();
                            if (string.IsNullOrEmpty(attrName)) continue;

                            var attribute = entity.GetAttributes()
                                .FirstOrDefault(a => a.Name.Equals(attrName, StringComparison.OrdinalIgnoreCase));

                            if (attribute == null)
                            {
                                changes.Add($"⚠️ Attribute '{attrName}' not found, skipped");
                                continue;
                            }

                            entity.RemoveAttribute(attribute);
                            changes.Add($"✅ Removed attribute '{attrName}'");
                        }
                    }

                    // Handle renaming attributes
                    if (parameters.ContainsKey("rename_attributes") && parameters["rename_attributes"] is JsonArray renameArray)
                    {
                        foreach (var renameNode in renameArray)
                        {
                            var renameObj = renameNode?.AsObject();
                            if (renameObj == null) continue;

                            var oldName = renameObj["old_name"]?.ToString();
                            var newName = renameObj["new_name"]?.ToString();

                            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
                            {
                                changes.Add($"⚠️ Skipped invalid rename (missing old_name or new_name)");
                                continue;
                            }

                            var attribute = entity.GetAttributes()
                                .FirstOrDefault(a => a.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));

                            if (attribute == null)
                            {
                                changes.Add($"⚠️ Attribute '{oldName}' not found, skipped rename");
                                continue;
                            }

                            attribute.Name = newName;
                            changes.Add($"✅ Renamed '{oldName}' to '{newName}'");
                        }
                    }

                    // Handle updating attribute types
                    if (parameters.ContainsKey("update_attributes") && parameters["update_attributes"] is JsonArray updateArray)
                    {
                        foreach (var updateNode in updateArray)
                        {
                            var updateObj = updateNode?.AsObject();
                            if (updateObj == null) continue;

                            var attrName = updateObj["name"]?.ToString();
                            var newType = updateObj["type"]?.ToString();

                            if (string.IsNullOrEmpty(attrName) || string.IsNullOrEmpty(newType))
                            {
                                changes.Add($"⚠️ Skipped invalid update (missing name or type)");
                                continue;
                            }

                            var attribute = entity.GetAttributes()
                                .FirstOrDefault(a => a.Name.Equals(attrName, StringComparison.OrdinalIgnoreCase));

                            if (attribute == null)
                            {
                                changes.Add($"⚠️ Attribute '{attrName}' not found, skipped update");
                                continue;
                            }

                            var oldType = attribute.Type?.GetType().Name ?? "Unknown";
                            
                            // Remove old attribute and create new one with same name but different type
                            entity.RemoveAttribute(attribute);
                            var success = CreateAttribute(entity, module, attrName, newType, updateObj);
                            
                            if (success)
                            {
                                changes.Add($"✅ Updated '{attrName}' from {oldType} to {newType}");
                            }
                            else
                            {
                                changes.Add($"❌ Failed to update '{attrName}' type");
                            }
                        }
                    }

                    if (changes.Count == 0)
                    {
                        return JsonSerializer.Serialize(new 
                        { 
                            success = false,
                            message = "No modifications specified. Use add_attributes, remove_attributes, rename_attributes, or update_attributes parameters."
                        });
                    }

                    transaction.Commit();

                    // Get final attribute list
                    var finalAttributes = entity.GetAttributes().Select(a => new
                    {
                        name = a.Name,
                        type = a.Type?.GetType().Name ?? "Unknown"
                    }).ToArray();

                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Entity '{entityName}' modified successfully",
                        changes = changes.ToArray(),
                        current_attributes = finalAttributes
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error modifying entity");
                MendixAdditionalTools.SetLastError($"Failed to modify entity: {ex.Message}", ex);
                return JsonSerializer.Serialize(new { error = $"Failed to modify entity: {ex.Message}" });
            }
        }

        private bool CreateAttribute(IEntity entity, IModule module, string attrName, string attrType, JsonObject attrObj)
        {
            try
            {
                var mxAttribute = _model.Create<IAttribute>();
                mxAttribute.Name = attrName;

                if (attrType.Equals("Enumeration", StringComparison.OrdinalIgnoreCase) || 
                    attrType.Equals("Enum", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle enumeration attributes
                    if (attrObj.ContainsKey("enumerationValues") && attrObj["enumerationValues"] is JsonArray enumValues)
                    {
                        var values = enumValues.Select(v => v?.ToString())
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Cast<string>()
                            .ToList();

                        if (values.Count > 0)
                        {
                            var enumType = CreateEnumerationType(_model, $"{attrName}Enum", values, module);
                            mxAttribute.Type = enumType;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (attrObj.ContainsKey("enumeration_name"))
                    {
                        var enumName = attrObj["enumeration_name"]?.ToString();
                        var existingEnum = _model.Root.GetModuleDocuments<IEnumeration>(module)
                            .FirstOrDefault(e => e.Name.Equals(enumName, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingEnum != null)
                        {
                            var attributeEnum = _model.Create<IEnumerationAttributeType>();
                            attributeEnum.Enumeration = existingEnum.QualifiedName;
                            mxAttribute.Type = attributeEnum;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    // Handle standard attribute types
                    var attributeType = CreateAttributeType(_model, attrType);
                    mxAttribute.Type = attributeType;
                }

                entity.AddAttribute(mxAttribute);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating attribute {attrName}");
                return false;
            }
        }

        public async Task<string> UpdateEntityLayout(JsonObject parameters)
        {
            try
            {
                using (var transaction = _model.StartTransaction("update entity layout"))
                {
                    var moduleName = parameters["module_name"]?.ToString();
                    var layoutMode = parameters["layout_mode"]?.ToString()?.ToLower() ?? "custom";

                    if (string.IsNullOrEmpty(moduleName))
                    {
                        return JsonSerializer.Serialize(new { error = "module_name is required" });
                    }

                    // Get module
                    var (module, error) = GetModuleByName(moduleName);
                    if (module == null)
                    {
                        return error!;
                    }

                    var entities = module.DomainModel.GetEntities().ToList();
                    if (entities.Count == 0)
                    {
                        return JsonSerializer.Serialize(new 
                        { 
                            success = false,
                            message = $"No entities found in module '{moduleName}'"
                        });
                    }

                    var changes = new List<string>();

                    switch (layoutMode)
                    {
                        case "custom":
                            // Apply custom positions for specific entities
                            if (parameters.ContainsKey("entity_positions") && parameters["entity_positions"] is JsonArray positionsArray)
                            {
                                foreach (var posNode in positionsArray)
                                {
                                    var posObj = posNode?.AsObject();
                                    if (posObj == null) continue;

                                    var entityName = posObj["entity_name"]?.ToString();
                                    var x = posObj["x"]?.GetValue<int>() ?? 0;
                                    var y = posObj["y"]?.GetValue<int>() ?? 0;

                                    if (string.IsNullOrEmpty(entityName)) continue;

                                    var entity = entities.FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));
                                    if (entity != null)
                                    {
                                        entity.Location = new Location(x, y);
                                        changes.Add($"✅ Positioned '{entityName}' at ({x}, {y})");
                                    }
                                    else
                                    {
                                        changes.Add($"⚠️ Entity '{entityName}' not found");
                                    }
                                }
                            }
                            else
                            {
                                return JsonSerializer.Serialize(new 
                                { 
                                    error = "entity_positions array is required for custom layout mode",
                                    example = new
                                    {
                                        module_name = "MyFirstModule",
                                        layout_mode = "custom",
                                        entity_positions = new[]
                                        {
                                            new { entity_name = "Customer", x = 100, y = 100 },
                                            new { entity_name = "Order", x = 400, y = 100 }
                                        }
                                    }
                                });
                            }
                            break;

                        case "grid":
                            // Arrange entities in a grid layout
                            var columns = parameters["columns"]?.GetValue<int>() ?? 5;
                            var spacingX = parameters["spacing_x"]?.GetValue<int>() ?? 250;
                            var spacingY = parameters["spacing_y"]?.GetValue<int>() ?? 200;
                            var startX = parameters["start_x"]?.GetValue<int>() ?? 20;
                            var startY = parameters["start_y"]?.GetValue<int>() ?? 20;

                            for (int i = 0; i < entities.Count; i++)
                            {
                                int column = i % columns;
                                int row = i / columns;
                                int x = startX + (column * spacingX);
                                int y = startY + (row * spacingY);

                                entities[i].Location = new Location(x, y);
                                changes.Add($"✅ Positioned '{entities[i].Name}' at ({x}, {y})");
                            }
                            break;

                        case "horizontal":
                            // Arrange entities horizontally
                            var hSpacing = parameters["spacing"]?.GetValue<int>() ?? 250;
                            var hStartX = parameters["start_x"]?.GetValue<int>() ?? 20;
                            var hY = parameters["y"]?.GetValue<int>() ?? 100;

                            for (int i = 0; i < entities.Count; i++)
                            {
                                int x = hStartX + (i * hSpacing);
                                entities[i].Location = new Location(x, hY);
                                changes.Add($"✅ Positioned '{entities[i].Name}' at ({x}, {hY})");
                            }
                            break;

                        case "vertical":
                            // Arrange entities vertically
                            var vSpacing = parameters["spacing"]?.GetValue<int>() ?? 200;
                            var vX = parameters["x"]?.GetValue<int>() ?? 100;
                            var vStartY = parameters["start_y"]?.GetValue<int>() ?? 20;

                            for (int i = 0; i < entities.Count; i++)
                            {
                                int y = vStartY + (i * vSpacing);
                                entities[i].Location = new Location(vX, y);
                                changes.Add($"✅ Positioned '{entities[i].Name}' at ({vX}, {y})");
                            }
                            break;

                        case "circular":
                            // Arrange entities in a circle
                            var radius = parameters["radius"]?.GetValue<int>() ?? 300;
                            var centerX = parameters["center_x"]?.GetValue<int>() ?? 500;
                            var centerY = parameters["center_y"]?.GetValue<int>() ?? 400;

                            for (int i = 0; i < entities.Count; i++)
                            {
                                double angle = (2 * Math.PI * i) / entities.Count;
                                int x = centerX + (int)(radius * Math.Cos(angle));
                                int y = centerY + (int)(radius * Math.Sin(angle));

                                entities[i].Location = new Location(x, y);
                                changes.Add($"✅ Positioned '{entities[i].Name}' at ({x}, {y})");
                            }
                            break;

                        default:
                            return JsonSerializer.Serialize(new 
                            { 
                                error = $"Unknown layout_mode: {layoutMode}",
                                supported_modes = new[] { "custom", "grid", "horizontal", "vertical", "circular" }
                            });
                    }

                    if (changes.Count == 0)
                    {
                        return JsonSerializer.Serialize(new 
                        { 
                            success = false,
                            message = "No layout changes were made"
                        });
                    }

                    transaction.Commit();

                    // Get final positions
                    var finalPositions = entities.Select(e => new
                    {
                        entity_name = e.Name,
                        x = e.Location.X,
                        y = e.Location.Y
                    }).ToArray();

                    return JsonSerializer.Serialize(new 
                    { 
                        success = true,
                        message = $"Layout updated successfully in module '{moduleName}'",
                        layout_mode = layoutMode,
                        changes = changes.ToArray(),
                        entity_positions = finalPositions
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity layout");
                MendixAdditionalTools.SetLastError($"Failed to update entity layout: {ex.Message}", ex);
                return JsonSerializer.Serialize(new { error = $"Failed to update entity layout: {ex.Message}" });
            }
        }

        public async Task<string> ManageAnnotations(JsonObject parameters)
        {
            try
            {
                using (var transaction = _model.StartTransaction("manage annotations"))
                {
                    var moduleName = parameters["module_name"]?.ToString();
                    var action = parameters["action"]?.ToString()?.ToLower() ?? "set";

                    if (string.IsNullOrEmpty(moduleName))
                    {
                        return JsonSerializer.Serialize(new { error = "module_name is required" });
                    }

                    // Get module
                    var (module, error) = GetModuleByName(moduleName);
                    if (module == null)
                    {
                        return error!;
                    }

                    var changes = new List<string>();
                    var warnings = new List<string>();

                    // Check if domain model documentation is requested
                    // NOTE: Mendix Extensions API doesn't expose IDomainModel.Documentation property
                    // The annotation text box visible in Studio Pro's domain model diagram is part of the visual layer
                    // and cannot be accessed or modified through the Extensions API
                    if (parameters.ContainsKey("domain_model_documentation") || parameters.ContainsKey("domain_model_annotation"))
                    {
                        warnings.Add("⚠️ Domain model-level annotations are not supported by the Mendix Extensions API. " +
                                   "The annotation text box in the domain model diagram is part of the visual layer and cannot be accessed programmatically. " +
                                   "You can only manage annotations for entities and associations.");
                    }

                    switch (action)
                    {
                        case "set":
                        case "update":
                        case "add":
                            // Set or update annotations
                            if (parameters.ContainsKey("entity_annotations") && parameters["entity_annotations"] is JsonArray entityArray)
                            {
                                foreach (var annotNode in entityArray)
                                {
                                    var annotObj = annotNode?.AsObject();
                                    if (annotObj == null) continue;

                                    var entityName = annotObj["entity_name"]?.ToString();
                                    var documentation = annotObj["documentation"]?.ToString();

                                    if (string.IsNullOrEmpty(entityName)) continue;

                                    var entity = module.DomainModel.GetEntities()
                                        .FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                                    if (entity != null && documentation != null)
                                    {
                                        entity.Documentation = documentation;
                                        changes.Add($"✅ Updated annotation for entity '{entityName}'");
                                    }
                                    else if (entity == null)
                                    {
                                        changes.Add($"⚠️ Entity '{entityName}' not found");
                                    }
                                }
                            }

                            if (parameters.ContainsKey("association_annotations") && parameters["association_annotations"] is JsonArray assocArray)
                            {
                                foreach (var annotNode in assocArray)
                                {
                                    var annotObj = annotNode?.AsObject();
                                    if (annotObj == null) continue;

                                    var associationName = annotObj["association_name"]?.ToString();
                                    var documentation = annotObj["documentation"]?.ToString();

                                    if (string.IsNullOrEmpty(associationName)) continue;

                                    // Find association by name in the domain model
                                    var allAssociations = module.DomainModel.GetEntities()
                                        .SelectMany(e => e.GetAssociations(AssociationDirection.Both, null)
                                            .Select(ea => ea.Association))
                                        .Distinct()
                                        .ToList();

                                    var association = allAssociations
                                        .FirstOrDefault(a => a.Name.Equals(associationName, StringComparison.OrdinalIgnoreCase));

                                    if (association != null && documentation != null)
                                    {
                                        association.Documentation = documentation;
                                        changes.Add($"✅ Updated annotation for association '{associationName}'");
                                    }
                                    else if (association == null)
                                    {
                                        changes.Add($"⚠️ Association '{associationName}' not found");
                                    }
                                }
                            }

                            if (changes.Count == 0)
                            {
                                return JsonSerializer.Serialize(new 
                                { 
                                    error = "No annotations specified. Use entity_annotations or association_annotations parameters.",
                                    example = new
                                    {
                                        module_name = "MyFirstModule",
                                        action = "set",
                                        entity_annotations = new[]
                                        {
                                            new { entity_name = "Customer", documentation = "Represents a customer entity" }
                                        },
                                        association_annotations = new[]
                                        {
                                            new { association_name = "Customer_Order", documentation = "Links customers to their orders" }
                                        }
                                    }
                                });
                            }
                            break;

                        case "remove":
                        case "clear":
                            // Remove annotations (set to empty string)
                            if (parameters.ContainsKey("entity_names") && parameters["entity_names"] is JsonArray entityNamesArray)
                            {
                                foreach (var nameNode in entityNamesArray)
                                {
                                    var entityName = nameNode?.ToString();
                                    if (string.IsNullOrEmpty(entityName)) continue;

                                    var entity = module.DomainModel.GetEntities()
                                        .FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                                    if (entity != null)
                                    {
                                        entity.Documentation = string.Empty;
                                        changes.Add($"✅ Removed annotation from entity '{entityName}'");
                                    }
                                    else
                                    {
                                        changes.Add($"⚠️ Entity '{entityName}' not found");
                                    }
                                }
                            }

                            if (parameters.ContainsKey("association_names") && parameters["association_names"] is JsonArray assocNamesArray)
                            {
                                foreach (var nameNode in assocNamesArray)
                                {
                                    var associationName = nameNode?.ToString();
                                    if (string.IsNullOrEmpty(associationName)) continue;

                                    var allAssociations = module.DomainModel.GetEntities()
                                        .SelectMany(e => e.GetAssociations(AssociationDirection.Both, null)
                                            .Select(ea => ea.Association))
                                        .Distinct()
                                        .ToList();

                                    var association = allAssociations
                                        .FirstOrDefault(a => a.Name.Equals(associationName, StringComparison.OrdinalIgnoreCase));

                                    if (association != null)
                                    {
                                        association.Documentation = string.Empty;
                                        changes.Add($"✅ Removed annotation from association '{associationName}'");
                                    }
                                    else
                                    {
                                        changes.Add($"⚠️ Association '{associationName}' not found");
                                    }
                                }
                            }

                            if (changes.Count == 0)
                            {
                                return JsonSerializer.Serialize(new 
                                { 
                                    error = "No entities or associations specified for removal. Use entity_names or association_names parameters."
                                });
                            }
                            break;

                        case "read":
                        case "get":
                            // Read current annotations
                            var entityAnnotations = module.DomainModel.GetEntities()
                                .Select(e => new
                                {
                                    entity_name = e.Name,
                                    documentation = e.Documentation ?? string.Empty,
                                    has_annotation = !string.IsNullOrWhiteSpace(e.Documentation)
                                })
                                .ToList();

                            var allAssocs = module.DomainModel.GetEntities()
                                .SelectMany(e => e.GetAssociations(AssociationDirection.Both, null)
                                    .Select(ea => ea.Association))
                                .Distinct()
                                .ToList();

                            var associationAnnotations = allAssocs
                                .Select(a => new
                                {
                                    association_name = a.Name,
                                    documentation = a.Documentation ?? string.Empty,
                                    has_annotation = !string.IsNullOrWhiteSpace(a.Documentation)
                                })
                                .ToList();

                            return JsonSerializer.Serialize(new 
                            { 
                                success = true,
                                module_name = moduleName,
                                entity_annotations = entityAnnotations,
                                association_annotations = associationAnnotations,
                                summary = new
                                {
                                    total_entities = entityAnnotations.Count,
                                    entities_with_annotations = entityAnnotations.Count(e => e.has_annotation),
                                    total_associations = associationAnnotations.Count,
                                    associations_with_annotations = associationAnnotations.Count(a => a.has_annotation)
                                },
                                warnings = warnings.Count > 0 ? warnings.ToArray() : null,
                                note = "Domain model-level documentation is not accessible through the Mendix Extensions API"
                            });

                        default:
                            return JsonSerializer.Serialize(new 
                            { 
                                error = $"Unknown action: {action}",
                                supported_actions = new[] { "set", "update", "add", "remove", "clear", "read", "get" }
                            });
                    }

                    transaction.Commit();

                    var result = new Dictionary<string, object>
                    {
                        ["success"] = true,
                        ["message"] = $"Annotations {action} completed successfully",
                        ["module_name"] = moduleName,
                        ["action"] = action,
                        ["changes"] = changes.ToArray()
                    };

                    if (warnings.Count > 0)
                    {
                        result["warnings"] = warnings.ToArray();
                    }

                    return JsonSerializer.Serialize(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing annotations");
                MendixAdditionalTools.SetLastError($"Failed to manage annotations: {ex.Message}", ex);
                return JsonSerializer.Serialize(new { error = $"Failed to manage annotations: {ex.Message}" });
            }
        }

        public async Task<string> CreateAssociation(JsonObject parameters)
        {
     try
      {
                using (var transaction = _model.StartTransaction("create association"))
  {
             var moduleName = parameters["module_name"]?.ToString();
   var name = parameters["name"]?.ToString();
           var parent = parameters["parent"]?.ToString();
    var child = parameters["child"]?.ToString();
              var type = parameters["type"]?.ToString() ?? "one-to-many";

         // Get module with validation
        var (module, error) = GetModuleByName(moduleName);
        if (module == null)
            {
              return error!;
      }

  // Add debugging to understand what parameters are being passed
            _logger.LogInformation($"CreateAssociation called with: module='{moduleName}', name='{name}', parent='{parent}', child='{child}', type='{type}'");

             if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(child))
           {
            return JsonSerializer.Serialize(new { 
    error = "Missing required parameters for association creation",
          message = "To create an association, you must provide: name, parent, and child parameters",
          required_parameters = new {
             module_name = new { type = "string", description = "Name of the module", required = true },
             name = new { type = "string", description = "Name of the association (e.g., 'Customer_Orders')", required = true },
   parent = new { type = "string", description = "Name of the parent entity (e.g., 'Customer')", required = true },
                 child = new { type = "string", description = "Name of the child entity (e.g., 'Order')", required = true },
      type = new { type = "string", description = "Type of association ('one-to-many' or 'many-to-many')", required = false, @default = "one-to-many" }
   }
            });
     }

           // Find parent and child entities
              var parentEntity = module.DomainModel.GetEntities()
         .FirstOrDefault(e => e.Name.Equals(parent, StringComparison.OrdinalIgnoreCase));
         var childEntity = module.DomainModel.GetEntities()
           .FirstOrDefault(e => e.Name.Equals(child, StringComparison.OrdinalIgnoreCase));

if (parentEntity == null)
              {
   return JsonSerializer.Serialize(new { error = $"Parent entity '{parent}' not found in module '{moduleName}'" });
                    }

             if (childEntity == null)
         {
      return JsonSerializer.Serialize(new { error = $"Child entity '{child}' not found in module '{moduleName}'" });
                    }

      // Create association
          var mxAssociation = childEntity.AddAssociation(parentEntity);
               mxAssociation.Name = name;
     mxAssociation.Type = MapAssociationType(type);

    _logger.LogInformation($"Created association {mxAssociation.Name} in module '{moduleName}'");

     transaction.Commit();

    return JsonSerializer.Serialize(new 
         { 
          success = true, 
  message = $"Association '{name}' created successfully in module '{moduleName}'",
          association = new
    {
    name = mxAssociation.Name,
       module = moduleName,
         parent = parentEntity.Name,
                 child = childEntity.Name,
     type = mxAssociation.Type.ToString()
       }
      });
         }
}
            catch (Exception ex)
       {
                _logger.LogError(ex, "Error creating association");
            MendixAdditionalTools.SetLastError($"Failed to create association: {ex.Message}", ex);
     return JsonSerializer.Serialize(new { error = $"Failed to create association: {ex.Message}" });
      }
    }

        public async Task<string> CreateMultipleEntities(JsonObject parameters)
        {
            try
            {
                using (var transaction = _model.StartTransaction("create multiple entities"))
                {
                    var moduleName = parameters["module_name"]?.ToString();
                    var entitiesArray = parameters["entities"]?.AsArray();
                    
                    // Extract persistable parameter (default to true for backward compatibility)
                    bool persistable = true;
                    if (parameters.ContainsKey("persistable"))
                    {
                        if (parameters["persistable"]?.AsValue().TryGetValue<bool>(out var persistableValue) == true)
                        {
                            persistable = persistableValue;
                        }
                    }

                    if (entitiesArray == null)
                    {
                        return JsonSerializer.Serialize(new { error = "Entities array is required" });
                    }

                    // Get module with validation
                    var (module, error) = GetModuleByName(moduleName);
                    if (module == null)
                    {
                        return error!;
                    }

                    var createdEntities = new List<object>();
                    string entityType = persistable ? "persistent" : "non-persistent";

                    foreach (var entityNode in entitiesArray)
                    {
                        var entityObj = entityNode?.AsObject();
                        if (entityObj == null) continue;

                        var entityName = entityObj["entity_name"]?.ToString();
                        var attributesArray = entityObj["attributes"]?.AsArray();

                        if (string.IsNullOrEmpty(entityName)) continue;

                        // Check if entity already exists
                        var existingEntity = module.DomainModel.GetEntities()
                            .FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                        if (existingEntity != null)
                        {
                            continue; // Skip existing entities
                        }

                        IEntity mxEntity;
                        var entityAttributes = new List<object>();

                        if (!persistable)
                        {
                            // Use template-based approach for non-persistent entities
                            mxEntity = CreateEntityFromTemplate(module, entityName, attributesArray);
                            if (mxEntity == null)
                            {
                                // Skip this entity and continue with others
                                continue;
                            }

                            // Collect attributes for response
                            foreach (var attr in mxEntity.GetAttributes())
                            {
                                entityAttributes.Add(new { name = attr.Name, type = attr.Type?.GetType().Name ?? "Unknown" });
                            }
                        }
                        else
                        {
                            // Create regular persistent entity
                            mxEntity = _model.Create<IEntity>();
                            mxEntity.Name = entityName;
                            module.DomainModel.AddEntity(mxEntity);

                            // Add attributes if provided
                            if (attributesArray != null)
                            {
                                foreach (var attrNode in attributesArray)
                                {
                                    var attrObj = attrNode?.AsObject();
                                    if (attrObj == null) continue;

                                    var attrName = attrObj["name"]?.ToString();
                                    var attrType = attrObj["type"]?.ToString();

                                    if (string.IsNullOrEmpty(attrName) || string.IsNullOrEmpty(attrType)) continue;

                                    var mxAttribute = _model.Create<IAttribute>();
                                    mxAttribute.Name = attrName;

                                    if (attrType.Equals("Enumeration", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var enumValues = attrObj["enumerationValues"]?.AsArray()
                                            ?.Select(v => v?.ToString())
                                            ?.Where(v => !string.IsNullOrEmpty(v))
                                            ?.Cast<string>() // Cast to non-nullable after null filtering
                                            ?.ToList();

                                        if (enumValues != null && enumValues.Any())
                                        {
                                            var enumTypeInstance = CreateEnumerationType(_model, attrName, enumValues, module);
                                            mxAttribute.Type = enumTypeInstance;
                                        }
                                        else
                                        {
                                            continue; // Skip invalid enumerations
                                        }
                                    }
                                    else
                                    {
                                        var attributeType = CreateAttributeType(_model, attrType);
                                        mxAttribute.Type = attributeType;
                                    }

                                    mxEntity.AddAttribute(mxAttribute);
                                    entityAttributes.Add(new { name = attrName, type = attrType });
                                }
                            }

                            // Position entity
                            PositionEntity(mxEntity, module.DomainModel.GetEntities().Count());
                        }

                        createdEntities.Add(new 
                        { 
                            name = entityName, 
                            persistable = persistable,
                            entityType = entityType,
                            attributes = entityAttributes 
                        });
                    }

                    transaction.Commit();

                    return JsonSerializer.Serialize(new 
                    { 
                        success = true, 
                        message = $"Successfully created {createdEntities.Count} {entityType} entities in module '{module.Name}'",
                        module = module.Name,
                        entities = createdEntities,
                        persistable = persistable,
                        entityType = entityType
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating multiple entities");
                return JsonSerializer.Serialize(new { error = $"Failed to create entities: {ex.Message}" });
            }
        }

        public async Task<string> CreateMultipleAssociations(JsonObject parameters)
        {
            try
            {
                using (var transaction = _model.StartTransaction("create multiple associations"))
                {
                    var moduleName = parameters["module_name"]?.ToString();
                    var associationsArray = parameters["associations"]?.AsArray();

                    if (associationsArray == null)
                    {
                        return JsonSerializer.Serialize(new { 
                            error = "Missing required 'associations' array parameter",
                            message = "To create multiple associations, you must provide an 'associations' array containing association objects",
                            required_parameters = new {
                                module_name = new { type = "string", description = "Name of the module", required = true },
                                associations = new {
                                    type = "array",
                                    description = "Array of association objects to create",
                                    required = true,
                                    item_schema = new {
                                        name = new { type = "string", description = "Name of the association", required = true },
                                        parent = new { type = "string", description = "Name of the parent entity", required = true },
                                        child = new { type = "string", description = "Name of the child entity", required = true },
                                        type = new { type = "string", description = "Type of association", required = false, @default = "one-to-many" }
                                    }
                                }
                            },
                            example_usage = new {
                                tool_name = "create_multiple_associations",
                                parameters = new {
                                    module_name = "MyFirstModule",
                                    associations = new[] {
                                        new {
                                            name = "Customer_Orders",
                                            parent = "Customer",
                                            child = "Order", 
                                            type = "one-to-many"
                                        }
                                    }
                                }
                            },
                            available_entities = new string[] { "Customer", "Order" },
                            guidance = "Each association object must have name, parent, and child properties. Ensure all referenced entities exist before creating associations."
                        });
                    }

                    // Get module with validation
                    var (module, error) = GetModuleByName(moduleName);
                    if (module == null)
                    {
                        return error!;
                    }

                    var createdAssociations = new List<object>();

                    foreach (var assocNode in associationsArray)
                    {
                        var assocObj = assocNode?.AsObject();
                        if (assocObj == null) continue;

                        var name = assocObj["name"]?.ToString();
                        var parent = assocObj["parent"]?.ToString();
                        var child = assocObj["child"]?.ToString();
                        var type = assocObj["type"]?.ToString() ?? "one-to-many";

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(child))
                        {
                            continue; // Skip invalid associations
                        }

                        // Find parent and child entities
                        var parentEntity = module.DomainModel.GetEntities()
                            .FirstOrDefault(e => e.Name.Equals(parent, StringComparison.OrdinalIgnoreCase));
                        var childEntity = module.DomainModel.GetEntities()
                            .FirstOrDefault(e => e.Name.Equals(child, StringComparison.OrdinalIgnoreCase));

                        if (parentEntity == null || childEntity == null)
                        {
                            continue; // Skip if entities don't exist
                        }

                        // Create association - FIXED: Use child.AddAssociation(parent) for correct direction
                        var mxAssociation = childEntity.AddAssociation(parentEntity);
                        mxAssociation.Name = name;
                        mxAssociation.Type = MapAssociationType(type);

                        createdAssociations.Add(new
                        {
                            name = mxAssociation.Name,
                            parent = parentEntity.Name,
                            child = childEntity.Name,
                            type = mxAssociation.Type.ToString()
                        });
                    }

                    transaction.Commit();

                    return JsonSerializer.Serialize(new 
                    { 
                        success = true, 
                        message = $"Successfully created {createdAssociations.Count} associations in module '{module.Name}'",
                        module = module.Name,
                        associations = createdAssociations
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating multiple associations");
                return JsonSerializer.Serialize(new { error = $"Failed to create associations: {ex.Message}" });
            }
        }

        public async Task<string> CreateDomainModelFromSchema(JsonObject parameters)
        {
            try
            {
                using (var transaction = _model.StartTransaction("create domain model from schema"))
                {
                    var moduleName = parameters["module_name"]?.ToString();
                    var schema = parameters["schema"]?.AsObject();

                    if (schema == null)
                    {
                        return JsonSerializer.Serialize(new { error = "Schema object is required" });
                    }

                    // Extract persistable parameter (default to true for backward compatibility)
                    bool persistable = true;
                    if (parameters.ContainsKey("persistable"))
                    {
                        if (parameters["persistable"]?.AsValue().TryGetValue<bool>(out var persistableValue) == true)
                        {
                            persistable = persistableValue;
                        }
                    }

                    // Get module with validation
                    var (module, error) = GetModuleByName(moduleName);
                    if (module == null)
                    {
                        return error!;
                    }

                    var entitiesArray = schema["entities"]?.AsArray();
                    var associationsArray = schema["associations"]?.AsArray();

                    var createdEntities = new List<object>();
                    var createdAssociations = new List<object>();
                    string entityType = persistable ? "persistent" : "non-persistent";

                    // Create entities first
                    if (entitiesArray != null)
                    {
                        foreach (var entityNode in entitiesArray)
                        {
                            var entityObj = entityNode?.AsObject();
                            if (entityObj == null) continue;

                            var entityName = entityObj["entity_name"]?.ToString();
                            var attributesArray = entityObj["attributes"]?.AsArray();

                            if (string.IsNullOrEmpty(entityName)) continue;

                            // Extract entityType for this specific entity
                            string currentEntityType = "persistent";
                            if (entityObj.ContainsKey("entityType"))
                            {
                                currentEntityType = entityObj["entityType"]?.ToString() ?? "persistent";
                            }
                            // Handle backward compatibility: if global persistable is false, use non-persistent
                            else if (!persistable)
                            {
                                currentEntityType = "non-persistent";
                            }

                            // Check if entity already exists
                            var existingEntity = module.DomainModel.GetEntities()
                                .FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

                            if (existingEntity != null)
                            {
                                continue; // Skip existing entities
                            }

                            IEntity mxEntity;
                            var entityAttributes = new List<object>();

                            // Use template-based approach for all entity types
                            mxEntity = CreateEntityFromTemplate(module, entityName, attributesArray, currentEntityType);
                            if (mxEntity == null)
                            {
                                // Skip this entity and continue with others
                                continue;
                            }

                            // Collect attributes for response
                            foreach (var attr in mxEntity.GetAttributes())
                            {
                                entityAttributes.Add(new { name = attr.Name, type = attr.Type?.GetType().Name ?? "Unknown" });
                            }

                            createdEntities.Add(new 
                            { 
                                name = entityName, 
                                persistable = currentEntityType == "persistent",
                                entityType = currentEntityType,
                                attributes = entityAttributes 
                            });
                        }
                    }

                    // Create associations after entities
                    if (associationsArray != null)
                    {
                        foreach (var assocNode in associationsArray)
                        {
                            var assocObj = assocNode?.AsObject();
                            if (assocObj == null) continue;

                            var name = assocObj["name"]?.ToString();
                            var parent = assocObj["parent"]?.ToString();
                            var child = assocObj["child"]?.ToString();
                            var type = assocObj["type"]?.ToString() ?? "one-to-many";

                            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(child))
                            {
                                continue; // Skip invalid associations
                            }

                            // Find parent and child entities
                            var parentEntity = module.DomainModel.GetEntities()
                                .FirstOrDefault(e => e.Name.Equals(parent, StringComparison.OrdinalIgnoreCase));
                            var childEntity = module.DomainModel.GetEntities()
                                .FirstOrDefault(e => e.Name.Equals(child, StringComparison.OrdinalIgnoreCase));

                            if (parentEntity == null || childEntity == null)
                            {
                                continue; // Skip if entities don't exist
                            }

                            // Create association - FIXED: Use child.AddAssociation(parent) for correct direction
                            var mxAssociation = childEntity.AddAssociation(parentEntity);
                            mxAssociation.Name = name;
                            mxAssociation.Type = MapAssociationType(type);

                            createdAssociations.Add(new
                            {
                                name = mxAssociation.Name,
                                parent = parentEntity.Name,
                                child = childEntity.Name,
                                type = mxAssociation.Type.ToString()
                            });
                        }
                    }

                    transaction.Commit();

                    return JsonSerializer.Serialize(new 
                    { 
                        success = true, 
                        message = $"Successfully created domain model in module '{module.Name}' with {createdEntities.Count} entities and {createdAssociations.Count} associations",
                        module = module.Name,
                        entities = createdEntities,
                        associations = createdAssociations,
                        persistable = persistable
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating domain model from schema");
                return JsonSerializer.Serialize(new { error = $"Failed to create domain model: {ex.Message}" });
            }
        }

        public async Task<string> DeleteModelElement(JsonObject parameters)
        {
            try
            {
                var moduleName = parameters["module_name"]?.ToString();
                var elementType = parameters["element_type"]?.ToString();
                var entityName = parameters["entity_name"]?.ToString();
                var attributeName = parameters["attribute_name"]?.ToString();
                var associationName = parameters["association_name"]?.ToString();
                var enumerationName = parameters["enumeration_name"]?.ToString();

                // Get module with validation
                var (module, error) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return error!;
                }

                if (string.IsNullOrEmpty(elementType))
                {
                    return JsonSerializer.Serialize(new { error = "Element type is required" });
                }

                switch (elementType.ToLower())
                {
                    case "entity":
                        if (string.IsNullOrEmpty(entityName))
                        {
                            return JsonSerializer.Serialize(new { error = "Entity name is required for entity deletion" });
                        }
                        return DeleteEntity(module.DomainModel, entityName);
                    
                    case "attribute":
                        if (string.IsNullOrEmpty(entityName))
                        {
                            return JsonSerializer.Serialize(new { error = "Entity name is required for attribute deletion" });
                        }
                        if (string.IsNullOrEmpty(attributeName))
                        {
                            return JsonSerializer.Serialize(new { error = "Attribute name is required for attribute deletion" });
                        }
                        return DeleteAttribute(module.DomainModel, entityName, attributeName);
                    
                    case "association":
                        if (string.IsNullOrEmpty(entityName))
                        {
                            return JsonSerializer.Serialize(new { error = "Entity name is required for association deletion" });
                        }
                        if (string.IsNullOrEmpty(associationName))
                        {
                            return JsonSerializer.Serialize(new { error = "Association name is required for association deletion" });
                        }
                        return DeleteAssociation(module.DomainModel, entityName, associationName);
                    
                    case "enumeration":
                        if (string.IsNullOrEmpty(enumerationName))
                        {
                            return JsonSerializer.Serialize(new { error = "Enumeration name is required for enumeration deletion" });
                        }
                        return DeleteEnumeration(module, enumerationName);
                    
                    default:
                        return JsonSerializer.Serialize(new 
                        { 
                            error = $"Unknown deletion type: {elementType}",
                            supportedTypes = new[] { "entity", "attribute", "association", "enumeration" }
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model element");
                MendixAdditionalTools.SetLastError($"Failed to delete element: {ex.Message}", ex);
                return JsonSerializer.Serialize(new { error = $"Failed to delete element: {ex.Message}" });
            }
        }

        public async Task<string> DiagnoseAssociations(JsonObject parameters)
        {
            try
            {
                var moduleName = parameters["module_name"]?.ToString();
                
                // Get module with validation
                var (module, error) = GetModuleByName(moduleName);
                if (module == null)
                {
                    return error!;
                }

                var domainModel = module.DomainModel;
                var entities = domainModel.GetEntities().ToList();
                var allAssociations = new List<object>();

                // Collect associations with detailed information
                foreach (var entity in entities)
                {
                    var associations = entity.GetAssociations(AssociationDirection.Both, null).ToList();
                    foreach (var association in associations)
                    {
                        allAssociations.Add(new
                        {
                            Name = association.Association.Name,
                            Parent = association.Parent.Name,
                            Child = association.Child.Name,
                            Type = association.Association.Type.ToString(),
                            MappedType = association.Association.Type == AssociationType.Reference ? "one-to-many" : "many-to-many"
                        });
                    }
                }

                var result = new
                {
                    module = module.Name,
                    entities = entities.Select(e => e.Name).ToList(),
                    entityCount = entities.Count,
                    associations = allAssociations,
                    associationCount = allAssociations.Count,
                    status = $"Domain model for module '{module.Name}' diagnosed successfully",
                    guidance = new
                    {
                        commonIssues = new[]
                        {
                            "Entities must exist before creating associations",
                            "Entity names are case sensitive",
                            "Don't use module prefixes in entity names",
                            "Association names must be unique",
                            "For one-to-many associations, parent is the 'one' side, child is the 'many' side"
                        },
                        properFormat = new
                        {
                            Name = "Customer_Orders",
                            Parent = "Customer",
                            Child = "Order",
                            Type = "one-to-many"
                        }
                    }
                };

                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error diagnosing associations");
                return JsonSerializer.Serialize(new { error = "Failed to diagnose associations", details = ex.Message });
            }
        }

        public async Task<string> GetLastError(JsonObject parameters)
        {
            return JsonSerializer.Serialize(new { error = "GetLastError not implemented yet" });
        }

        public async Task<string> ListAvailableTools(JsonObject parameters)
        {
            var tools = new[]
            {
                "read_domain_model",
                "create_entity",
                "create_association",
                "create_multiple_entities",
                "create_multiple_associations",
                "create_domain_model_from_schema",
                "delete_model_element",
                "diagnose_associations",
                "get_last_error",
                "list_available_tools"
            };

            return JsonSerializer.Serialize(new { tools = tools, status = "success" });
        }

        /// <summary>
        /// Get dynamically available entity types based on template availability
        /// </summary>
        /// <returns>List of supported entity types in current project</returns>
        public List<string> GetAvailableEntityTypes()
        {
            var availableTypes = new List<string>
            {
                "persistent" // Always available
            };

            try
            {
                // Check for each template and add to available types if found
                if (FindNonPersistentTemplate() != null)
                {
                    availableTypes.Add("non-persistent");
                }

                if (FindFileDocumentTemplate() != null)
                {
                    availableTypes.Add("filedocument");
                }

                if (FindImageTemplate() != null)
                {
                    availableTypes.Add("image");
                }

                if (FindStoreCreatedDateTemplate() != null)
                {
                    availableTypes.Add("storecreateddate");
                }

                if (FindStoreChangeDateTemplate() != null)
                {
                    availableTypes.Add("storechangedate");
                }

                if (FindStoreCreatedChangeDateTemplate() != null)
                {
                    availableTypes.Add("storecreatedchangedate");
                }

                if (FindStoreOwnerTemplate() != null)
                {
                    availableTypes.Add("storeowner");
                }

                if (FindStoreChangeByTemplate() != null)
                {
                    availableTypes.Add("storechangeby");
                }

                _logger.LogInformation($"Available entity types: {string.Join(", ", availableTypes)}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking template availability, falling back to basic types");
                // If there's an error checking templates, provide basic types
                if (!availableTypes.Contains("non-persistent"))
                {
                    availableTypes.Add("non-persistent"); // Usually available
                }
            }

            return availableTypes;
        }

        /// <summary>
        /// Get detailed information about available entity types including descriptions
        /// </summary>
        /// <returns>Dictionary with entity type details</returns>
        public Dictionary<string, object> GetEntityTypeInfo()
        {
            var availableTypes = GetAvailableEntityTypes();
            var allDescriptions = new Dictionary<string, string>
            {
                { "persistent", "Standard entity stored in database (always available)" },
                { "non-persistent", "Session entity not stored in database (uses NPE template)" },
                { "filedocument", "Entity inheriting from System.FileDocument for file storage" },
                { "image", "Entity inheriting from System.Image for image storage" },
                { "storecreateddate", "Entity with automatic creation date tracking" },
                { "storechangedate", "Entity with automatic modification date tracking" },
                { "storecreatedchangedate", "Entity with both creation and modification date tracking" },
                { "storeowner", "Entity with automatic owner (creator) tracking" },
                { "storechangeby", "Entity with automatic last modifier tracking" }
            };

            var result = new Dictionary<string, object>
            {
                { "availableTypes", availableTypes },
                { "descriptions", availableTypes.ToDictionary(type => type, type => allDescriptions[type]) },
                { "unavailableTypes", allDescriptions.Keys.Except(availableTypes).ToList() },
                { "templateInstructions", "Unavailable types require corresponding templates in AIExtension module" }
            };

            return result;
        }

        #region Helper Methods

        private Dictionary<string, string> GetEntityAttributes(IEntity entity)
        {
            return entity.GetAttributes()
                .Where(attr => attr != null)
                .ToDictionary(
                    attr => attr.Name,
                    attr => {
                        var typeName = attr.Type?.GetType().Name ?? "Unknown";
                        
                        // Remove "AttributeTypeProxy" suffix
                        typeName = typeName.Replace("AttributeTypeProxy", "");
                        
                        // Handle Enumerations specially
                        if (attr.Type is IEnumerationAttributeType enumType)
                        {
                            try
                            {
                                var enumeration = enumType.Enumeration?.Resolve();
                                if (enumeration != null)
                                {
                                    var enumValues = enumeration.GetValues()
                                        ?.Select(v => v?.Name)
                                        .Where(name => !string.IsNullOrEmpty(name))
                                        .ToList();
                                    
                                    if (enumValues != null && enumValues.Any())
                                    {
                                        return $"Enumeration ({string.Join("/", enumValues)})";
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // If enumeration resolution fails, just return the type name
                            }
                            
                            return "Enumeration";
                        }
                        
                        return typeName;
                    }
                );
        }

        private List<Association> GetEntityAssociations(IEntity entity, IModule module)
        {
            var entityAssociations = new List<Association>();
            
            try
            {
                var associations = entity.GetAssociations(AssociationDirection.Both, null);

                foreach (var association in associations)
                {
                    if (association?.Association == null || association.Parent == null || association.Child == null)
                    {
                        continue; // Skip invalid associations
                    }

                    var associationType = association.Association.Type.ToString();
                    var mappedType = associationType switch
                    {
                        "Reference" => "one-to-many",
                        "ReferenceSet" => "many-to-many",
                        _ => "one-to-many"
                    };

                    // FIXED: For Reference associations, we need to swap parent/child to match business semantics
                    // In Mendix: association.Parent is the entity that owns the reference (the "many" side)
                    //           association.Child is the entity being referenced (the "one" side)
                    // In business terms: we want "one" side as parent, "many" side as child
                    string parentName, childName;
                    
                    if (associationType == "Reference")
                    {
                        // Swap: Mendix parent becomes our child, Mendix child becomes our parent
                        parentName = association.Child.Name;  // The "one" side (being referenced)
                        childName = association.Parent.Name;  // The "many" side (owns the reference)
                    }
                    else
                    {
                        // For many-to-many, keep original direction
                        parentName = association.Parent.Name;
                        childName = association.Child.Name;
                    }

                    var associationModel = new Association
                    {
                        Name = association.Association.Name,
                        Parent = parentName,
                        Child = childName,
                        Type = mappedType
                    };

                    entityAssociations.Add(associationModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading associations for entity {EntityName}", entity?.Name ?? "Unknown");
            }

            return entityAssociations;
        }

        private IAttributeType CreateAttributeType(IModel model, string attributeType)
        {
            switch (attributeType.ToLowerInvariant())
            {
                case "decimal":
                    return model.Create<IDecimalAttributeType>();
                case "integer":
                    return model.Create<IIntegerAttributeType>();
                case "string":
                    return model.Create<IStringAttributeType>();
                case "boolean":
                    return model.Create<IBooleanAttributeType>();
                case "datetime":
                    return model.Create<IDateTimeAttributeType>();
                case "autonumber":
                    return model.Create<IAutoNumberAttributeType>();
                default:
                    return model.Create<IStringAttributeType>();
            }
        }

        private IEnumerationAttributeType CreateEnumerationType(IModel model, string attributeName, List<string> enumValues, IModule module)
        {
            // Check if an enumeration with the same name and values already exists in the module
            var desiredEnumName = attributeName + "Enum";
            var existingEnumerations = model.Root.GetModuleDocuments<IEnumeration>(module).ToList();
            
            foreach (var existingEnum in existingEnumerations)
            {
                // Check if the enumeration name matches
                if (existingEnum.Name.Equals(desiredEnumName, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if the values match exactly
                    var existingValues = existingEnum.GetValues().Select(v => v.Name).ToList();
                    
                    if (existingValues.Count == enumValues.Count && 
                        existingValues.OrderBy(v => v).SequenceEqual(enumValues.OrderBy(v => v)))
                    {
                        // Enumeration with same name and values exists - reuse it
                        _logger.LogInformation($"Reusing existing enumeration '{existingEnum.Name}' in module '{module.Name}'");
                        var attributeEnum = model.Create<IEnumerationAttributeType>();
                        attributeEnum.Enumeration = existingEnum.QualifiedName;
                        return attributeEnum;
                    }
                }
            }

            // No matching enumeration found - create a new one
            var newAttributeEnum = model.Create<IEnumerationAttributeType>();
            var enumDoc = model.Create<IEnumeration>();
            
            // Check if the base name is already taken (by a different enumeration)
            var finalEnumName = desiredEnumName;
            var existingNames = existingEnumerations.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            if (existingNames.Contains(finalEnumName))
            {
                // Name exists but with different values - append a number
                int counter = 1;
                do
                {
                    finalEnumName = $"{desiredEnumName}{counter}";
                    counter++;
                } while (existingNames.Contains(finalEnumName));
                
                _logger.LogWarning($"Enumeration name '{desiredEnumName}' already exists with different values. Using '{finalEnumName}' instead.");
            }
            
            enumDoc.Name = finalEnumName;

            foreach (var value in enumValues)
            {
                var enumValue = model.Create<IEnumerationValue>();
                enumValue.Name = value;
                
                var captionText = model.Create<IText>();
                captionText.AddOrUpdateTranslation("en_US", value);
                enumValue.Caption = captionText;
                
                enumDoc.AddValue(enumValue);
            }

            module.AddDocument(enumDoc);
            newAttributeEnum.Enumeration = enumDoc.QualifiedName;
            
            _logger.LogInformation($"Created new enumeration '{enumDoc.Name}' in module '{module.Name}'");
            return newAttributeEnum;
        }

        private AssociationType MapAssociationType(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return AssociationType.Reference;
            }
            
            var normalizedType = type.ToLowerInvariant().Trim();
            
            switch (normalizedType)
            {
                case "one-to-many":
                case "reference":
                    return AssociationType.Reference;
                case "many-to-many":
                case "referenceset":  // FIXED: ReferenceSet should create many-to-many
                    return AssociationType.ReferenceSet;
                default:
                    return AssociationType.Reference;
            }
        }

        private void PositionEntity(IEntity entity, int entityCount)
        {
            const int EntityWidth = 150;
            const int EntityHeight = 75;
            const int SpacingX = 200;
            const int SpacingY = 150;
            const int StartX = 20;
            const int StartY = 20;
            const int MaxColumns = 5;

            int column = entityCount % MaxColumns;
            int row = entityCount / MaxColumns;
            
            int x = StartX + (column * SpacingX);
            int y = StartY + (row * SpacingY);
            
            entity.Location = new Location(x, y);
        }

        private string DeleteEntity(IDomainModel domainModel, string entityName)
        {
            using (var transaction = _model.StartTransaction("Delete Entity"))
            {
                var entity = domainModel.GetEntities().FirstOrDefault(e => e.Name == entityName);
                if (entity == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Entity '{entityName}' not found" });
                }

                // Delete all associations first
                var entityAssociations = entity.GetAssociations(AssociationDirection.Both, null).ToList();
                foreach (var entityAssociation in entityAssociations)
                {
                    var association = entityAssociation.Association;
                    entity.DeleteAssociation(association);
                }

                domainModel.RemoveEntity(entity);
                transaction.Commit();

                return JsonSerializer.Serialize(new 
                { 
                    success = true, 
                    message = $"Entity '{entityName}' and its associations deleted successfully" 
                });
            }
        }

        private string DeleteAttribute(IDomainModel domainModel, string entityName, string attributeName)
        {
            using (var transaction = _model.StartTransaction("Delete Attribute"))
            {
                var entity = domainModel.GetEntities().FirstOrDefault(e => e.Name == entityName);
                if (entity == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Entity '{entityName}' not found" });
                }

                var attribute = entity.GetAttributes().FirstOrDefault(a => a.Name == attributeName);
                if (attribute == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Attribute '{attributeName}' not found in entity '{entityName}'" });
                }

                entity.RemoveAttribute(attribute);
                transaction.Commit();

                return JsonSerializer.Serialize(new 
                { 
                    success = true, 
                    message = $"Attribute '{attributeName}' deleted successfully from entity '{entityName}'" 
                });
            }
        }

        private string DeleteAssociation(IDomainModel domainModel, string entityName, string associationName)
        {
            using (var transaction = _model.StartTransaction("Delete Association"))
            {
                var entity = domainModel.GetEntities().FirstOrDefault(e => e.Name == entityName);
                if (entity == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Entity '{entityName}' not found" });
                }

                var entityAssociation = entity.GetAssociations(AssociationDirection.Both, null)
                    .FirstOrDefault(a => a.Association.Name == associationName);
                if (entityAssociation == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Association '{associationName}' not found" });
                }

                var association = entityAssociation.Association;
                entity.DeleteAssociation(association);
                transaction.Commit();

                return JsonSerializer.Serialize(new 
                { 
                    success = true, 
                    message = $"Association '{associationName}' deleted successfully" 
                });
            }
        }

        private string DeleteEnumeration(IModule module, string enumerationName)
        {
            using (var transaction = _model.StartTransaction("Delete Enumeration"))
            {
                try
                {
                    // Find the enumeration in the module
                    var enumerations = _model.Root.GetModuleDocuments<IEnumeration>(module).ToList();
                    var enumeration = enumerations.FirstOrDefault(e => e.Name.Equals(enumerationName, StringComparison.OrdinalIgnoreCase));
                    
                    if (enumeration == null)
                    {
                        return JsonSerializer.Serialize(new 
                        { 
                            error = $"Enumeration '{enumerationName}' not found in module '{module.Name}'",
                            availableEnumerations = enumerations.Select(e => e.Name).ToArray()
                        });
                    }

                    // Check if the enumeration is in use by any attributes
                    var entitiesUsingEnum = new List<string>();
                    foreach (var entity in module.DomainModel.GetEntities())
                    {
                        foreach (var attribute in entity.GetAttributes())
                        {
                            if (attribute.Type is IEnumerationAttributeType enumType)
                            {
                                var resolvedEnum = enumType.Enumeration.Resolve();
                                if (resolvedEnum != null && resolvedEnum.QualifiedName == enumeration.QualifiedName)
                                {
                                    entitiesUsingEnum.Add($"{entity.Name}.{attribute.Name}");
                                }
                            }
                        }
                    }

                    // If the enumeration is in use, return an error with details
                    if (entitiesUsingEnum.Any())
                    {
                        return JsonSerializer.Serialize(new 
                        { 
                            error = $"Cannot delete enumeration '{enumerationName}' because it is in use",
                            usedBy = entitiesUsingEnum.ToArray(),
                            message = "Delete or modify the attributes using this enumeration first"
                        });
                    }

                    // Delete the enumeration
                    module.RemoveDocument(enumeration);
                    transaction.Commit();

                    _logger.LogInformation($"Deleted enumeration '{enumerationName}' from module '{module.Name}'");
                    
                    return JsonSerializer.Serialize(new 
                    { 
                        success = true, 
                        message = $"Enumeration '{enumerationName}' deleted successfully from module '{module.Name}'" 
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error deleting enumeration '{enumerationName}'");
                    return JsonSerializer.Serialize(new 
                    { 
                        error = $"Failed to delete enumeration '{enumerationName}': {ex.Message}" 
                    });
                }
            }
        }

        #region Entity Template Methods

        /// <summary>
        /// Finds the template non-persistent entity (AIExtension.NPE) for copying
        /// </summary>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindNonPersistentTemplate()
        {
            return FindTemplateEntity("NPE", "non-persistent");
        }

        /// <summary>
        /// Finds the template FileDocument entity (AIExtension.FileDocument) for copying
        /// </summary>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindFileDocumentTemplate()
        {
            return FindTemplateEntity("FileDocument", "FileDocument");
        }

        /// <summary>
        /// Finds the template Image entity (AIExtension.Image) for copying
        /// </summary>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindImageTemplate()
        {
            return FindTemplateEntity("Image", "Image");
        }

        /// <summary>
        /// Finds the template StoreCreatedDate entity (AIExtension.StoreCreatedDate) for copying
        /// </summary>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindStoreCreatedDateTemplate()
        {
            return FindTemplateEntity("StoreCreatedDate", "StoreCreatedDate");
        }

        /// <summary>
        /// Finds the template StoreChangeDate entity (AIExtension.StoreChangeDate) for copying
        /// </summary>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindStoreChangeDateTemplate()
        {
            return FindTemplateEntity("StoreChangeDate", "StoreChangeDate");
        }

        /// <summary>
        /// Finds the template StoreCreatedChangeDate entity (AIExtension.StoreCreatedChangeDate) for copying
        /// </summary>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindStoreCreatedChangeDateTemplate()
        {
            return FindTemplateEntity("StoreCreatedChangeDate", "StoreCreatedChangeDate");
        }

        /// <summary>
        /// Finds the template StoreOwner entity (AIExtension.StoreOwner) for copying
        /// </summary>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindStoreOwnerTemplate()
        {
            return FindTemplateEntity("StoreOwner", "StoreOwner");
        }

        /// <summary>
        /// Finds the template StoreChangeBy entity (AIExtension.StoreChangeBy) for copying
        /// </summary>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindStoreChangeByTemplate()
        {
            return FindTemplateEntity("StoreChangeBy", "StoreChangeBy");
        }

        /// <summary>
        /// Generic method to find template entities in the AIExtension module
        /// </summary>
        /// <param name="templateName">Name of the template entity to find</param>
        /// <param name="templateType">Type description for logging purposes</param>
        /// <returns>The template entity if found, null otherwise</returns>
        private IEntity? FindTemplateEntity(string templateName, string templateType)
        {
            try
            {
                // Use the same module access pattern as the rest of the codebase
                // Find all modules the safe way
                var modules = _model.Root.GetModules();
                
                // Look specifically for AIExtension module
                var aiExtensionModule = modules.FirstOrDefault(m => m?.Name == "AIExtension");

                if (aiExtensionModule?.DomainModel == null)
                {
                    _logger.LogWarning($"AIExtension module or its domain model not found for {templateType} template");
                    return null;
                }

                // Find the specified entity in AIExtension
                var templateEntity = aiExtensionModule.DomainModel.GetEntities()
                    .FirstOrDefault(e => e?.Name == templateName);

                if (templateEntity == null)
                {
                    _logger.LogWarning($"{templateName} template entity not found in AIExtension module");
                    return null;
                }

                _logger.LogInformation($"Found {templateType} template entity: AIExtension.{templateName}");
                return templateEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding {templateType} template entity");
                return null;
            }
        }        /// <summary>
        /// Creates an entity by copying from a template based on entity type
        /// </summary>
        /// <param name="targetModule">Module where the new entity will be created</param>
        /// <param name="entityName">Name for the new entity</param>
        /// <param name="attributesArray">Attributes to add to the entity</param>
        /// <param name="entityType">Type of entity: "persistent", "non-persistent", "filedocument", "image"</param>
        /// <returns>The created entity if successful, null otherwise</returns>
        private IEntity? CreateEntityFromTemplate(IModule targetModule, string entityName, JsonArray? attributesArray, string entityType = "persistent")
        {
            try
            {
                IEntity? templateEntity = null;
                string templateDescription = "";

                // Find the appropriate template based on entity type
                switch (entityType.ToLower())
                {
                    case "non-persistent":
                        templateEntity = FindNonPersistentTemplate();
                        templateDescription = "non-persistent";
                        break;
                    case "filedocument":
                        templateEntity = FindFileDocumentTemplate();
                        templateDescription = "FileDocument";
                        break;
                    case "image":
                        templateEntity = FindImageTemplate();
                        templateDescription = "Image";
                        break;
                    case "storecreateddate":
                        templateEntity = FindStoreCreatedDateTemplate();
                        templateDescription = "StoreCreatedDate";
                        break;
                    case "storechangedate":
                        templateEntity = FindStoreChangeDateTemplate();
                        templateDescription = "StoreChangeDate";
                        break;
                    case "storecreatedchangedate":
                        templateEntity = FindStoreCreatedChangeDateTemplate();
                        templateDescription = "StoreCreatedChangeDate";
                        break;
                    case "storeowner":
                        templateEntity = FindStoreOwnerTemplate();
                        templateDescription = "StoreOwner";
                        break;
                    case "storechangeby":
                        templateEntity = FindStoreChangeByTemplate();
                        templateDescription = "StoreChangeBy";
                        break;
                    case "persistent":
                    default:
                        // For persistent entities, create normally without template
                        return CreatePersistentEntity(targetModule, entityName, attributesArray);
                }

                if (templateEntity == null)
                {
                    _logger.LogError($"Cannot create {templateDescription} entity: template not found");
                    return null;
                }

                // Copy the template entity (this preserves the special properties)
                var newEntity = _model.Copy(templateEntity);

                // Rename the entity
                newEntity.Name = entityName;

                // Add the desired attributes
                if (attributesArray != null)
                {
                    foreach (var attrNode in attributesArray)
                    {
                        var attrObj = attrNode?.AsObject();
                        if (attrObj == null) continue;

                        var attrName = attrObj["name"]?.ToString();
                        var attrType = attrObj["type"]?.ToString();

                        if (string.IsNullOrEmpty(attrName) || string.IsNullOrEmpty(attrType)) continue;

                        var mxAttribute = _model.Create<IAttribute>();
                        mxAttribute.Name = attrName;

                        if (attrType.Equals("Enumeration", StringComparison.OrdinalIgnoreCase))
                        {
                            var enumValues = attrObj["enumerationValues"]?.AsArray()
                                ?.Select(v => v?.ToString())
                                ?.Where(v => !string.IsNullOrEmpty(v))
                                ?.Cast<string>() // Cast to non-nullable after null filtering
                                ?.ToList();

                            if (enumValues != null && enumValues.Any())
                            {
                                var enumTypeInstance = CreateEnumerationType(_model, attrName, enumValues, targetModule);
                                mxAttribute.Type = enumTypeInstance;
                            }
                            else
                            {
                                continue; // Skip invalid enumerations
                            }
                        }
                        else
                        {
                            var attributeType = CreateAttributeType(_model, attrType);
                            mxAttribute.Type = attributeType;
                        }

                        newEntity.AddAttribute(mxAttribute);
                    }
                }

                // Add the entity to the target module
                targetModule.DomainModel.AddEntity(newEntity);

                // Position the entity
                PositionEntity(newEntity, targetModule.DomainModel.GetEntities().Count());

                _logger.LogInformation($"Successfully created {templateDescription} entity '{entityName}' from template");
                return newEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating entity '{entityName}' from template");
                return null;
            }
        }

        /// <summary>
        /// Creates a regular persistent entity without using templates
        /// </summary>
        /// <param name="targetModule">Module where the new entity will be created</param>
        /// <param name="entityName">Name for the new entity</param>
        /// <param name="attributesArray">Attributes to add to the entity</param>
        /// <returns>The created entity if successful, null otherwise</returns>
        private IEntity? CreatePersistentEntity(IModule targetModule, string entityName, JsonArray? attributesArray)
        {
            try
            {
                // Create regular persistent entity
                var mxEntity = _model.Create<IEntity>();
                mxEntity.Name = entityName;
                targetModule.DomainModel.AddEntity(mxEntity);

                // Add attributes if provided
                if (attributesArray != null)
                {
                    foreach (var attrNode in attributesArray)
                    {
                        var attrObj = attrNode?.AsObject();
                        if (attrObj == null) continue;

                        var attrName = attrObj["name"]?.ToString();
                        var attrType = attrObj["type"]?.ToString();

                        if (string.IsNullOrEmpty(attrName) || string.IsNullOrEmpty(attrType)) continue;

                        var mxAttribute = _model.Create<IAttribute>();
                        mxAttribute.Name = attrName;

                        if (attrType.Equals("Enumeration", StringComparison.OrdinalIgnoreCase))
                        {
                            var enumValues = attrObj["enumerationValues"]?.AsArray()
                                ?.Select(v => v?.ToString())
                                ?.Where(v => !string.IsNullOrEmpty(v))
                                ?.Cast<string>() // Cast to non-nullable after null filtering
                                ?.ToList();

                            if (enumValues != null && enumValues.Any())
                            {
                                var enumTypeInstance = CreateEnumerationType(_model, attrName, enumValues, targetModule);
                                mxAttribute.Type = enumTypeInstance;
                            }
                            else
                            {
                                continue; // Skip invalid enumerations
                            }
                        }
                        else
                        {
                            var attributeType = CreateAttributeType(_model, attrType);
                            mxAttribute.Type = attributeType;
                        }

                        mxEntity.AddAttribute(mxAttribute);
                    }
                }

                // Position entity
                PositionEntity(mxEntity, targetModule.DomainModel.GetEntities().Count());

                return mxEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating persistent entity '{entityName}'");
                return null;
            }
        }

        /// <summary>
        /// Gets the template name for a given entity type
        /// </summary>
        /// <param name="entityType">The entity type</param>
        /// <returns>The template name</returns>
        private static string GetTemplateName(string entityType)
        {
            return entityType.ToLower() switch
            {
                "non-persistent" => "NPE",
                "filedocument" => "FileDocument",
                "image" => "Image",
                "storecreateddate" => "StoreCreatedDate",
                "storechangedate" => "StoreChangeDate",
                "storecreatedchangedate" => "StoreCreatedChangeDate",
                "storeowner" => "StoreOwner",
                "storechangeby" => "StoreChangeBy",
                _ => "Unknown"
            };
        }

        #endregion

        #endregion
    }

    public class Association
    {
        public string Name { get; set; }
        public string Parent { get; set; }
        public string Child { get; set; }
        public string Type { get; set; }
    }
}
