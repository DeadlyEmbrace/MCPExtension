# Mendix Studio Pro MCPExtension

## 📦 Mendix Marketplace Module

**For Mendix 10.24.2 Users**: A pre-built version of this extension is available in the **`Mendix Marketplace Module`** folder, complete with installation instructions and setup guide. This provides a ready-to-use version without requiring compilation from source.

**Location**: `./Mendix Marketplace Module/`  
**Target Version**: Mendix 10.24.2  
**Contents**: Built extension files and comprehensive setup documentation

## Overview

MCPExtension is an experimental C# Mendix Extensibility framework project that exposes **Mendix Studio Pro capabilities and tools** through a **Model Context Protocol (MCP) Server**. This extension allows external applications and AI tools to interact with Mendix Studio Pro's domain modeling capabilities via standardized MCP protocols.

## Features

- **HTTP/SSE MCP Server** - Provides a standards-compliant MCP server implementation
- **Advanced Domain Model Management** - Create, read, update, and delete domain model entities and associations with support for 9 entity types
- **Template-Based Entity Creation** - Leverages AIExtension module templates for specialized entity types including audit trails and file storage
- **Comprehensive Entity Types** - Full support for persistent, non-persistent, FileDocument, Image, and audit trail entities
- **Page Generation** - Generate overview pages for domain entities
- **Microflow Management** - Create, inspect, and manage microflows with activity sequences
- **Sample Data Generation** - Create realistic sample data for testing with proper relationships
- **Real-time Debug Logging** - Comprehensive logging for troubleshooting
- **Visual Studio Pro Integration** - Seamless integration with Mendix Studio Pro through a dockable pane
- **Complete Parameter Documentation** - Full JSON schemas for all MCP tools with detailed parameter specifications

## Architecture

The extension consists of several key components:

### Core Components

- **`AIAPIEngine`** - Main extension entry point and lifecycle management
- **`MendixMcpServer`** - MCP server implementation with tool registration
- **`McpServer`** - HTTP/SSE server handling MCP protocol messages
- **`AIAPIEngineViewModel`** - WebView-based UI for server control

### Tool Categories

#### Domain Model Tools (`MendixDomainModelTools`)
- `read_domain_model` - Read current domain model structure with entities and associations
- `create_entity` - Create new entities with comprehensive support for 9 entity types and attributes
- `create_association` - Create associations between entities with proper relationship types
- `create_multiple_entities` - Bulk entity creation with mixed entity types support
- `create_multiple_associations` - Bulk association creation for complex domain models
- `create_domain_model_from_schema` - Create complete domain models from JSON schemas with all entity types
- `delete_model_element` - Delete entities, attributes, or associations from the domain model
- `diagnose_associations` - Troubleshoot association creation issues with detailed diagnostics

##### Supported Entity Types

The extension supports **9 comprehensive entity types** through template-based creation:

1. **`persistent`** (default) - Standard database entities
2. **`non-persistent`** - Session entities (NPE template)
3. **`filedocument`** - File storage entities (inherits from System.FileDocument)
4. **`image`** - Image storage entities (inherits from System.Image)
5. **`storecreateddate`** - Automatic creation date tracking
6. **`storechangedate`** - Automatic modification date tracking
7. **`storecreatedchangedate`** - Both creation and modification date tracking
8. **`storeowner`** - Automatic owner (creator) tracking
9. **`storechangeby`** - Automatic last modifier tracking

**Template Requirements**: All special entity types require corresponding templates in the AIExtension module for proper inheritance and property setup.

#### Additional Tools (`MendixAdditionalTools`)
- `save_data` - Generate and validate sample data with proper entity relationships
- `generate_overview_pages` - Create list view pages for entities with navigation support
- `list_microflows` - List microflows in a module with detailed metadata
- `create_microflow` - Create new microflows with parameters and return types
- `create_microflow_activities` - Create microflow activity sequences with proper ordering
- `read_microflow_details` - Get detailed microflow information including all activities
- `get_last_error` - Retrieve last error information with stack traces
- `list_available_tools` - List all available MCP tools with capabilities
- `debug_info` - Get comprehensive domain model debug information with usage examples

## Installation

1. **Build the Extension**
   ```powershell
   dotnet build MCPExtension.sln
   ```

2. **Deploy to Mendix Studio Pro**
   - The extension automatically copies to `C:\Mendix Projects\{YourProjectName}\extensions\MCP\` after build
   - Alternatively, copy the built files to your Mendix project's `extensions` folder

3. **Load in Studio Pro**
   - Open Mendix Studio Pro while using --enable-extension-development
   - Open your Mendix project
   - Access via menu: **Extensions → MCP → MCP Server**

## Usage

### Server Endpoints

Once started, the MCP server exposes several endpoints:

- **SSE Endpoint**: `http://localhost:3001/sse` - Server-Sent Events connection
- **Message Endpoint**: `http://localhost:3001/message` - MCP message handling
- **Health Check**: `http://localhost:3001/health` - Server health status
- **MCP Metadata**: `http://localhost:3001/.well-known/mcp` - MCP server metadata

### Entity Creation Examples

The extension provides powerful entity creation capabilities with comprehensive type support:

#### Basic Persistent Entity
```json
{
  "entity_name": "Customer",
  "attributes": [
    {"name": "firstName", "type": "String"},
    {"name": "lastName", "type": "String"},
    {"name": "birthDate", "type": "DateTime"},
    {"name": "isActive", "type": "Boolean"}
  ]
}
```

#### Non-Persistent Entity (Session Data)
```json
{
  "entity_name": "ShoppingCart",
  "entityType": "non-persistent",
  "attributes": [
    {"name": "sessionId", "type": "String"},
    {"name": "totalAmount", "type": "Decimal"}
  ]
}
```

#### FileDocument Entity (File Storage)
```json
{
  "entity_name": "Invoice",
  "entityType": "filedocument",
  "attributes": [
    {"name": "invoiceNumber", "type": "String"},
    {"name": "issueDate", "type": "DateTime"}
  ]
}
```

#### Image Entity (Image Storage)
```json
{
  "entity_name": "ProductPhoto",
  "entityType": "image",
  "attributes": [
    {"name": "altText", "type": "String"},
    {"name": "displayOrder", "type": "Integer"}
  ]
}
```

#### Audit Trail Entities

**Creation Date Tracking:**
```json
{
  "entity_name": "AuditDocument",
  "entityType": "storecreateddate",
  "attributes": [
    {"name": "documentTitle", "type": "String"},
    {"name": "description", "type": "String"}
  ]
}
```

**Modification Date Tracking:**
```json
{
  "entity_name": "TrackedProduct",
  "entityType": "storechangedate",
  "attributes": [
    {"name": "productName", "type": "String"},
    {"name": "price", "type": "Decimal"}
  ]
}
```

**Full Audit Trail (Creation + Modification):**
```json
{
  "entity_name": "FullAuditEntity",
  "entityType": "storecreatedchangedate",
  "attributes": [
    {"name": "name", "type": "String"},
    {"name": "value", "type": "String"}
  ]
}
```

**Owner Tracking:**
```json
{
  "entity_name": "OwnedDocument",
  "entityType": "storeowner",
  "attributes": [
    {"name": "title", "type": "String"},
    {"name": "content", "type": "String"}
  ]
}
```

**Last Modifier Tracking:**
```json
{
  "entity_name": "EditableRecord",
  "entityType": "storechangeby",
  "attributes": [
    {"name": "recordName", "type": "String"},
    {"name": "data", "type": "String"}
  ]
}
```

#### Entity with Enumeration
```json
{
  "entity_name": "Product",
  "attributes": [
    {"name": "productName", "type": "String"},
    {"name": "price", "type": "Decimal"},
    {
      "name": "status",
      "type": "Enumeration",
      "enumerationValues": ["Available", "OutOfStock", "Discontinued"]
    }
  ]
}
```

#### Bulk Entity Creation
```json
{
  "entities": [
    {
      "entity_name": "Customer",
      "attributes": [{"name": "name", "type": "String"}]
    },
    {
      "entity_name": "Order",
      "entityType": "storecreateddate",
      "attributes": [{"name": "orderNumber", "type": "String"}]
    },
    {
      "entity_name": "OrderImage",
      "entityType": "image",
      "attributes": [{"name": "description", "type": "String"}]
    }
  ]
}
```

### Parameter Documentation

All MCP tools include comprehensive JSON schemas with:
- **Required/Optional Parameters** - Clear specification of mandatory fields
- **Parameter Types** - Detailed type information with validation
- **Enumeration Values** - Valid options for choice parameters (e.g., entityType)
- **Parameter Descriptions** - Detailed explanations of parameter usage
- **Examples** - Practical usage examples for each tool

Use the `debug_info` tool to see complete parameter documentation and examples for all available tools.

### Logging

Debug logs are written to:
- `{MendixProjectPath}/resources/mcp_debug.log`

## Configuration

### Port Configuration

The server automatically finds an available port starting from 3001. You can modify the port in `AIAPIEngine.cs`:

```csharp
// Use a different starting port
_mcpPort = FindAvailablePort(3001);
```

### Project Directory

The extension automatically detects the Mendix project directory. Sample data and logs are stored in:
- `{MendixProjectPath}/resources/`

## Troubleshooting

### Server Won't Start

1. Check if the port is already in use
2. Verify the Mendix project is open
3. Check the debug log at `{MendixProjectPath}/resources/mcp_debug.log`

### Connection Issues

1. Verify the server is running (check the UI status indicator)
2. Test the health endpoint: `http://localhost:3001/health`
3. Check Windows Firewall settings
4. Ensure no antivirus software is blocking the connection

### Tool Errors

1. Use the `get_last_error` tool to retrieve detailed error information
2. Use the `debug_info` tool to inspect the current domain model state
3. Check the debug log for detailed error traces

### Entity Creation Issues

#### Template Not Found Errors
- **Problem**: Special entity types (non-persistent, filedocument, image, audit entities) require templates
- **Solution**: Ensure the AIExtension module contains the required templates:
  - `NPE` - For non-persistent entities
  - `FileDocument` - For file document entities
  - `Image` - For image entities
  - `StoreCreatedDate` - For creation date tracking
  - `StoreChangeDate` - For modification date tracking
  - `StoreCreatedChangeDate` - For full audit tracking
  - `StoreOwner` - For owner tracking
  - `StoreChangeBy` - For modifier tracking

#### Parameter Validation Errors
- **Problem**: Invalid entityType or missing required parameters
- **Solution**: Use valid entityType values: `persistent`, `non-persistent`, `filedocument`, `image`, `storecreateddate`, `storechangedate`, `storecreatedchangedate`, `storeowner`, `storechangeby`

#### Legacy Parameter Support
- **Problem**: Using old `persistable` parameter
- **Solution**: Migrate to `entityType` parameter:
  - `persistable: false` → `entityType: "non-persistent"`
  - `persistable: true` → `entityType: "persistent"` (or omit for default)

#### Association Creation Issues
- **Problem**: Cannot create associations between entities
- **Solution**: 
  1. Ensure both entities exist in the domain model
  2. Use the `diagnose_associations` tool for detailed troubleshooting
  3. Check entity names match exactly (case-sensitive)

### Association Type Mapping

The extension correctly maps association types as follows:
- **`"Reference"`** → **One-to-Many** associations (`AssociationType.Reference`)
- **`"ReferenceSet"`** → **Many-to-Many** associations (`AssociationType.ReferenceSet`)

**Note**: This mapping was fixed in August 2025 to ensure `ReferenceSet` properly creates many-to-many associations instead of incorrectly creating one-to-many associations.

## Known Issues & Improvement Roadmap

### Critical Issues

#### 1. ✅ FIXED - list_modules Tool (November 2025)
- **Status**: ✅ Fixed
- **Issue**: Dictionary key error when calling `list_modules`
- **Fix**: Changed to use generic `GetModuleDocuments<IDocument>()` with proper error handling
- **Commit**: ad9da41

#### 2. ✅ FIXED - Success/Error Reporting Inconsistency (November 2025)
- **Status**: ✅ Fixed
- **Issue**: Tools reported errors but actually succeeded (e.g., "Collection was modified..." error during `create_entity`)
- **Fix**: Materialized collections with `.ToList()` before enumeration to avoid live collection modification
- **Commit**: ad9da41

### Parameter Handling Issues

#### 3. ✅ FIXED - module_name Inconsistency (November 2025)
- **Status**: ✅ Fixed
- **Issue**: `module_name` was required for almost every tool but often missing from documented required parameters
- **Solution Implemented**: Added `module_name` to all tool schemas as explicitly required parameter with descriptions
- **Fixed Tools**: 
  - `create_entity` - Now requires module_name
  - `create_association` - Now requires module_name
  - `create_multiple_entities` - Now requires module_name
  - `create_multiple_associations` - Now requires module_name
  - `create_domain_model_from_schema` - Now requires module_name
  - `delete_model_element` - Now requires module_name
  - `generate_overview_pages` - Now requires module_name
  - `diagnose_associations` - Module_name is optional
- **Commit**: (current)

#### 4. Parameter Validation Timing
- **Status**: 🟡 High Priority
- **Issue**: Tools fail mid-execution rather than validating parameters upfront
- **Impact**: Wasted operations, unclear error messages, potential partial state changes
- **Fix Required**: Validate all parameters before beginning operations

### Missing Functionality

#### 5. Entity Update Capability
- **Status**: 🟠 Medium Priority
- **Issue**: Can create or delete entities, but cannot add attributes to existing entities
- **Current Workaround**: Delete and recreate entire entity
- **Impact**: Data loss risk, workflow inefficiency
- **Required Tools**:
  - `add_attribute_to_entity` - Add new attributes to existing entities
  - `update_attribute` - Modify existing attribute properties
  - `remove_attribute` - Delete specific attributes without removing entity

#### 6. Lightweight Discovery Tools
- **Status**: 🟠 Medium Priority
- **Issue**: Full domain model reads are heavy; need quick discovery options
- **Required Tools**:
  - `list_entities` - List entity names across all/specific modules without full details
  - `list_associations` - List associations without complete relationship graphs
  - `entity_exists` - Quick existence check without loading full entity
  - `get_entity_summary` - Lightweight entity info (name, attributes, type only)

#### 7. Microflow Tool Exposure
- **Status**: 🟠 Medium Priority
- **Issue**: Microflow tools appear in `list_available_tools` but aren't exposed through MCP interface
- **Impact**: Advertised functionality is unusable
- **Affected Tools**:
  - `create_microflow`
  - `create_microflow_activities`
  - `list_microflows`
  - `read_microflow_details`
- **Fix Required**: Register microflow tools in MCP server initialization

### Enhanced Capabilities

#### 8. Batch Operation Error Handling
- **Status**: 🟢 Low Priority
- **Issue**: `create_multiple_entities` doesn't report which entities succeeded/failed in partial failures
- **Impact**: Unclear state after batch operations, difficult rollback
- **Required Enhancement**:
  ```json
  {
    "success": false,
    "partial_success": true,
    "succeeded": ["Entity1", "Entity3"],
    "failed": [
      {"entity": "Entity2", "error": "Template not found"},
      {"entity": "Entity4", "error": "Invalid attribute type"}
    ],
    "total": 4,
    "success_count": 2,
    "failure_count": 2
  }
  ```

#### 9. Validation-Only Modes
- **Status**: 🟢 Low Priority
- **Issue**: No way to test entity/association creation without committing changes
- **Required Enhancement**: Add `dry_run` or `validate_only` parameter to creation tools
- **Benefits**: Safe testing, parameter validation without side effects

#### 10. Project Context Tool
- **Status**: 🟢 Low Priority
- **Issue**: No tool to get current project information for orientation
- **Required Tool**: `get_project_info`
  ```json
  {
    "project_name": "MyApp",
    "project_version": "10.24.2",
    "modules": ["Module1", "Module2"],
    "default_module": "Module1",
    "project_path": "C:\\Mendix Projects\\MyApp"
  }
  ```

### Documentation Issues

#### 11. Parameter Documentation Clarity
- **Status**: 🟡 High Priority
- **Issue**: Unclear which parameters are truly required vs optional (especially `module_name`)
- **Fix Required**:
  - Review all tool schemas for accurate `required` arrays
  - Document default behaviors when optional parameters omitted
  - Add parameter examples to each tool description
  - Specify module_name requirements explicitly in every tool

#### 12. Error Message Quality
- **Status**: 🟡 High Priority
- **Issue**: Generic error messages don't guide users to solutions
- **Required Enhancement**:
  - Include parameter name in validation errors
  - Suggest valid values for enumeration parameters
  - Provide troubleshooting hints in error responses
  - Reference related tools for common workflows

### Activity Insertion Order Bug

#### ✅ FIXED (November 2025)
- **Issue**: Activities appeared in reverse/random order in microflows
- **Root Cause**: `GetAllMicroflowActivities()` returns activities in undefined order per API docs
- **Solution**: Always use `TryInsertAfterStart` for sequential insertion
- **Status**: Fixed and documented in `BUG_FIX_INSERTION_ORDER.md`

## Development

### Dependencies

- **.NET 8.0** - Target framework
- **Mendix.StudioPro.ExtensionsAPI** - Mendix Studio Pro integration
- **Microsoft.AspNetCore** - HTTP server functionality
- **System.Text.Json** - JSON serialization
- **Eto.Forms** - UI framework

### Building from Source

```powershell
# Clone the repository
git clone https://github.com/rperdiga/MCPExtension.git
cd MCPExtension

# Build the solution
dotnet build

# The extension will be deployed to the configured Mendix project
```

### Extending the Framework

To add new MCP tools:

1. **Create Tool Implementation**
   - Add a new method in `MendixDomainModelTools` or `MendixAdditionalTools`
   - Implement the tool logic with proper error handling
   - Return appropriate response objects

2. **Register the Tool**
   - Add tool registration in `MendixMcpServer.RegisterTools()`
   - Use the pattern: `_mcpServer.RegisterTool("tool_name", async (JsonObject parameters) => { ... })`

3. **Add Parameter Schema**
   - Define the input schema in `McpServer.GetToolInputSchema()`
   - Include all required and optional parameters
   - Specify parameter types, descriptions, and validation rules
   - Add enumeration values for choice parameters

4. **Add Tool Description**
   - Add comprehensive description in `McpServer.GetToolDescription()`
   - Explain what the tool does and its key capabilities
   - Mention any special requirements or dependencies

5. **Update Documentation**
   - Add examples to the `debug_info` tool output in `MendixAdditionalTools.cs`
   - Include usage patterns and common parameter combinations
   - Document any template requirements for entity-related tools

#### Example Tool Addition

```csharp
// 1. Tool Implementation (in MendixDomainModelTools.cs)
public async Task<object> CreateCustomEntity(JsonObject parameters)
{
    var entityName = parameters["entity_name"]?.ToString();
    // Implementation logic here
    return new { success = true, entityName = entityName };
}

// 2. Tool Registration (in MendixMcpServer.cs)
_mcpServer.RegisterTool("create_custom_entity", async (JsonObject parameters) => 
{
    var result = await domainModelTools.CreateCustomEntity(parameters);
    return (object)result;
});

// 3. Parameter Schema (in McpServer.cs - GetToolInputSchema)
"create_custom_entity" => new
{
    type = "object",
    properties = new
    {
        entity_name = new { 
            type = "string", 
            description = "Name of the custom entity to create" 
        },
        custom_type = new { 
            type = "string", 
            @enum = new[] { "type1", "type2" },
            description = "Type of custom entity" 
        }
    },
    required = new[] { "entity_name", "custom_type" }
},

// 4. Tool Description (in McpServer.cs - GetToolDescription)
"create_custom_entity" => "Create a custom entity with specialized properties and behavior",
```

## License

This project is experimental and provided as-is for research and development purposes.

## Contributing

This is an experimental project. Feel free to submit issues and enhancement requests.

## Support

For issues and questions:
1. Check the debug logs first
2. Use the built-in diagnostic tools (`debug_info`, `get_last_error`)
3. Submit issues with detailed error information and steps to reproduce
