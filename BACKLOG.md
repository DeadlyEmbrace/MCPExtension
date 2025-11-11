# MCPExtension - Feature Backlog

**Last Updated**: November 11, 2025  
**Current Version**: In Development  
**Branch**: feature/add-list-modules-tool

---

## ✅ Issues Fixed During This Session

- [x] **list_modules** - Dictionary key error resolved
- [x] **Success/error reporting** - Consistent handling implemented
- [x] **module_name parameter** - Now properly required/validated
- [x] **read_domain_model null reference** - Fixed enumeration handling
- [x] **Duplicate enumeration creation** - Check-before-create logic added
- [x] **Enumeration visibility** - list_enumerations tool added
- [x] **Enumeration deletion** - Added to delete_model_element
- [x] **Entity modification** - modify_entity tool added (rename/add/remove/update attributes)
- [x] **Entity layout** - update_entity_layout tool added (multiple modes)
- [x] **Entity/association annotations** - manage_annotations tool added
- [x] **Project errors** - get_project_errors tool added (with API limitations documented)

---

## 🔴 Critical Missing Functionality

### Domain Model Annotations
**Priority**: HIGH | **Effort**: Low | **Impact**: High

- [x] **Domain model-level annotations** - ~~Currently can only manage entity/association annotations, not the module's domain model annotation text box~~
  - [x] ~~Add support for reading domain model documentation~~
  - [x] ~~Add support for setting domain model documentation~~
  - [x] ~~Add support for removing domain model documentation~~
  - **Status**: ✅ COMPLETED WITH API LIMITATION
  - **Blocked By**: Mendix Extensions API Limitation
  - **Notes**: **IMPORTANT - API LIMITATION DISCOVERED**: The Mendix Extensions API v8.0 does NOT expose domain model-level documentation. The annotation text box visible in Studio Pro's domain model diagram is part of the visual/diagram layer, not the model layer that the Extensions API provides access to. The API only supports entity and association documentation properties (`IEntity.Documentation` and `IAssociation.Documentation`). This has been documented in the tool's schema, implementation, and README. The `manage_annotations` tool now includes clear warnings when users attempt to access domain model annotations. **Resolution**: Entity and association annotations ARE fully supported and working. Domain model annotations cannot be implemented due to API limitations.

### Entity Management
**Priority**: HIGH | **Effort**: High | **Impact**: Critical

- [ ] **Generalization support** - No way to set entity inheritance (e.g., inheriting from System.User, System.FileDocument)
  - [ ] Set generalization (inherit from entity)
  - [ ] Clear generalization (remove inheritance)
  - [ ] Query generalization hierarchy
  - **Status**: ❌ BLOCKED BY API LIMITATION
  - **Blocked By**: Mendix Extensions API Limitation
  - **Notes**: **IMPORTANT - API LIMITATION DISCOVERED**: The Mendix Extensions API v8.0 does NOT expose entity generalization/inheritance functionality. The `IEntity` interface only provides these members: `DataStorageGuid` property, `AddAssociation()`, `DeleteAssociation()`, and `GetAssociations()` methods. No Generalization property, SetGeneralization method, or related inheritance APIs exist in the Extensions API documentation. This means programmatic creation of entities that inherit from System.FileDocument, System.ImageDocument, or System.User is not possible through the extension. **Impact**: Cannot create proper file storage entities (FileDocument inheritance), image entities (ImageDocument inheritance), or user entities (User inheritance) via the MCP extension. **Workaround**: Users must manually set entity generalization in Studio Pro by right-clicking the entity → Properties → Generalization. This is similar to the other visual layer limitations discovered (domain model annotations, association arrow positioning). Essential for file uploads, image handling, and user management in real-world Mendix applications.

- [ ] **Entity properties** - Cannot configure persistable vs non-persistable, image entity, file entity, etc.
  - [ ] Configure persistable/non-persistable
  - [ ] Set as image entity
  - [ ] Set as file entity
  - [ ] Set entity access rules
  - **Status**: Not Started
  - **Blocked By**: None
  - **API Research Needed**: Check if ExtensionsAPI supports these properties

- [ ] **Attribute properties** - Cannot set default values, validation rules, calculated attributes
  - [ ] Set default values for attributes
  - [ ] Add validation rules
  - [ ] Configure calculated attributes
  - [ ] Set attribute access rules
  - **Status**: Not Started
  - **Blocked By**: None
  - **API Research Needed**: ExtensionsAPI attribute property support

- [ ] **Index management** - Cannot create database indexes on attributes
  - [ ] Create single-attribute indexes
  - [ ] Create composite indexes
  - [ ] Remove indexes
  - [ ] List existing indexes
  - **Status**: Not Started
  - **Blocked By**: None
  - **API Research Needed**: Index creation API availability

### Association Management
**Priority**: MEDIUM | **Effort**: Medium | **Impact**: High

- [ ] **Association properties** - Cannot configure delete behavior, owner settings beyond basic creation
  - [ ] Set delete behavior (cascade, prevent, keep)
  - [ ] Configure ownership (both sides)
  - [ ] Set association access rules
  - **Status**: Not Started
  - **Blocked By**: None
  - **Notes**: Current create_association has basic creation only

- [ ] **Association validation** - Better error messages when associations fail
  - [ ] Pre-validate association rules before creation
  - [ ] Provide actionable error messages
  - [ ] Suggest fixes for common issues
  - **Status**: Not Started
  - **Blocked By**: None

### Enumeration Management
**Priority**: MEDIUM | **Effort**: Medium | **Impact**: Medium

- [ ] **Enumeration captions** - Currently shows "Mendix.Modeler.ExtensionLoader.ModelProxies.Texts.TextProxy" instead of actual caption text
  - [ ] Fix TextProxy display in list_enumerations
  - [ ] Show actual caption text
  - [ ] Support multi-language captions
  - **Status**: Not Started
  - **Blocked By**: None
  - **Notes**: ⚠️ Known Issue - API returns proxy objects instead of text

- [ ] **Enumeration images** - Cannot set icons for enum values
  - [ ] Set images for enumeration values
  - [ ] List available images
  - [ ] Remove images from values
  - **Status**: Not Started
  - **Blocked By**: None
  - **API Research Needed**: Check ExtensionsAPI image support

- [ ] **Update enumeration** - Cannot modify existing enumerations (add/remove/rename values)
  - [ ] Add values to existing enumeration
  - [ ] Remove values from enumeration
  - [ ] Rename enumeration values
  - [ ] Reorder enumeration values
  - **Status**: Not Started
  - **Blocked By**: None
  - **Notes**: Currently can only create and delete entire enumerations

---

## 🟡 Microflow Functionality Gaps

### Activity Management
**Priority**: HIGH | **Effort**: Very High | **Impact**: Critical

- [ ] **All activity types** - Currently only supports Create Object and Change Object
  
  **Retrieve Activities**:
  - [ ] Retrieve from database
  - [ ] Retrieve by association
  - [ ] Retrieve first
  
  **Database Activities**:
  - [ ] Commit
  - [ ] Delete
  - [ ] Rollback
  
  **UI Activities**:
  - [ ] Show page
  - [ ] Show message
  - [ ] Validation feedback
  - [ ] Download file
  
  **Integration Activities**:
  - [ ] Call microflow
  - [ ] Call web service
  - [ ] Call REST service
  
  **Logic Activities**:
  - [ ] Loop (iterate over list)
  - [ ] Decision (exclusive split)
  - [ ] Merge
  - [ ] Continue
  - [ ] Break
  
  **List Activities**:
  - [ ] Aggregate list (sum, count, avg, etc.)
  - [ ] Change list (add, remove, clear)
  - [ ] List operation
  
  **Document Activities**:
  - [ ] Generate document
  - [ ] Import/export XML
  
  **Status**: Not Started  
  - **Blocked By**: API research for each activity type
  - **Notes**: This is the largest gap in functionality

- [ ] **Activity positioning** - Cannot specify exact positions, only sequential insertion
  - [ ] Set X,Y coordinates for activities
  - [ ] Auto-layout algorithms
  - [ ] Snap to grid
  - **Status**: Not Started
  - **Blocked By**: None
  - **API Research Needed**: Check if ExtensionsAPI supports positioning

- [ ] **Activity connections** - Cannot control flow lines between activities
  - [ ] Specify sequence flows
  - [ ] Add conditional flows (from decision)
  - [ ] Configure flow labels
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Error handling** - Cannot add error handlers to activities
  - [ ] Add error handler to activity
  - [ ] Configure error flow
  - [ ] Set error handling type
  - **Status**: Not Started
  - **Blocked By**: None

### Microflow Properties
**Priority**: MEDIUM | **Effort**: Medium | **Impact**: Medium

- [ ] **Return type configuration** - Cannot modify return types after creation
  - [ ] Change microflow return type
  - [ ] Set return value
  - [ ] Configure end event
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Security settings** - Cannot configure allowed roles
  - [ ] Set allowed roles
  - [ ] Configure microflow security
  - [ ] Apply module roles
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Documentation** - Cannot add descriptions to microflows
  - [ ] Set microflow documentation
  - [ ] Add parameter descriptions
  - [ ] Set activity annotations
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Parameters** - Cannot add/remove/modify input parameters
  - [ ] Add input parameters
  - [ ] Remove parameters
  - [ ] Rename parameters
  - [ ] Change parameter types
  - **Status**: Not Started
  - **Blocked By**: None

---

## 🟢 Data & Testing

### Sample Data Generation
**Priority**: MEDIUM | **Effort**: Medium | **Impact**: Medium

- [ ] **Enhanced save_data** - Current implementation needs improvements
  - [ ] Better validation feedback
  - [ ] Support for file/image entities
  - [ ] Auto-generation of realistic data based on attribute types
  - [ ] Bulk data generation (generate N records)
  - [ ] Relationship handling (create associated objects)
  - **Status**: Not Started
  - **Blocked By**: None
  - **Notes**: Current save_data works but limited

### Testing & Validation
**Priority**: LOW | **Effort**: Medium | **Impact**: Medium

- [ ] **Validate domain model** - Check for common issues before they cause errors
  - [ ] Check for broken associations
  - [ ] Validate attribute types
  - [ ] Check for circular references
  - [ ] Validate naming conventions
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Dry-run mode** - Preview changes without committing
  - [ ] Simulate operations
  - [ ] Show what would change
  - [ ] Validate without applying
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Undo/rollback** - Revert recent changes
  - [ ] Track change history
  - [ ] Implement undo stack
  - [ ] Rollback to checkpoint
  - **Status**: Not Started
  - **Blocked By**: None
  - **Notes**: May be complex depending on API transaction support

---

## 🔵 Page & UI Generation

### Current State Enhancement
**Priority**: MEDIUM | **Effort**: High | **Impact**: Medium

- [ ] **Enhance generate_overview_pages** - Existing tool needs expansion
  - [ ] Support for detail pages (edit forms)
  - [ ] Custom page templates
  - [ ] Layout selection
  - [ ] Widget configuration options
  - [ ] Responsive design options
  - **Status**: Not Started
  - **Blocked By**: None
  - **Notes**: Tool exists but basic functionality only

### Missing Functionality
**Priority**: MEDIUM | **Effort**: Very High | **Impact**: Medium

- [ ] **Form generation** - Create edit/new forms for entities
  - [ ] Generate data views
  - [ ] Add form fields for attributes
  - [ ] Add buttons (save, cancel, delete)
  - [ ] Configure validation
  - **Status**: Not Started
  - **Blocked By**: Page API research

- [x] **Navigation configuration** - Add pages to navigation
  - [x] Add menu items to responsive web navigation
  - [x] Add multiple pages at once
  - [x] Validate page existence
  - **Status**: ✅ IMPLEMENTED (November 2025)
  - **Tool**: `add_pages_to_navigation`
  - **Notes**: Uses `INavigationManagerService.PopulateWebNavigationWith()` API. Successfully adds pages to the responsive web navigation profile. Provides helpful error messages when pages don't exist.

- [ ] **Snippet generation** - Create reusable UI components
  - [ ] Create snippets
  - [ ] Add widgets to snippets
  - [ ] Use snippets in pages
  - **Status**: Not Started
  - **Blocked By**: Snippet API research

- [ ] **Custom widget placement** - Add specific widgets to pages
  - [ ] Data grid configuration
  - [ ] Input widget configuration
  - [ ] Button configuration
  - [ ] Container layouts
  - **Status**: Not Started
  - **Blocked By**: Widget API research

---

## 🟣 Project Structure & Management

### Module Management
**Priority**: LOW | **Effort**: Medium | **Impact**: Low

- [ ] **Create modules** - Currently can only work with existing modules
  - [ ] Create new module
  - [ ] Set module properties
  - [ ] Configure module role
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Module settings** - Configure module properties
  - [ ] Set module version
  - [ ] Configure module security
  - [ ] Set module constants
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Module dependencies** - Manage module references
  - [ ] Add module dependencies
  - [ ] Remove dependencies
  - [ ] View dependency graph
  - **Status**: Not Started
  - **Blocked By**: None

### Project Info
**Priority**: LOW | **Effort**: Low | **Impact**: Low

- [ ] **Project metadata** - Get project name, version, Mendix version
  - [ ] Get project info
  - [ ] Get Mendix version
  - [ ] Get project settings
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Runtime settings** - View/configure constants, scheduled events
  - [ ] List constants
  - [ ] Set constant values
  - [ ] List scheduled events
  - [ ] Configure scheduled events
  - **Status**: Not Started
  - **Blocked By**: API research

- [ ] **Security settings** - Manage user roles, module security
  - [ ] List user roles
  - [ ] Create user roles
  - [ ] Configure module roles
  - [ ] Set entity access
  - **Status**: Not Started
  - **Blocked By**: Security API research

---

## ⚪ Advanced Features

### Version Control Integration
**Priority**: LOW | **Effort**: High | **Impact**: Low

- [ ] **Git operations** - Commit changes with messages
  - [ ] Check git status
  - [ ] Stage changes
  - [ ] Commit with message
  - [ ] Push changes
  - **Status**: Not Started
  - **Blocked By**: Git integration feasibility
  - **Notes**: May be out of scope for ExtensionsAPI

- [ ] **Change tracking** - List uncommitted changes
  - [ ] List modified files
  - [ ] Show change diff
  - [ ] Discard changes
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Branch management** - If applicable
  - [ ] List branches
  - [ ] Switch branches
  - [ ] Create branches
  - **Status**: Not Started
  - **Blocked By**: Git integration feasibility

### Import/Export
**Priority**: LOW | **Effort**: Medium | **Impact**: Low

- [ ] **Export domain model** - To JSON/XML for backup
  - [ ] Export to JSON
  - [ ] Export to XML
  - [ ] Include relationships
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Import domain model** - From standard formats
  - [ ] Import from JSON
  - [ ] Import from XML
  - [ ] Merge with existing model
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Module packages** - Export/import .mpk files
  - [ ] Export module to .mpk
  - [ ] Import .mpk to project
  - [ ] Validate .mpk structure
  - **Status**: Not Started
  - **Blocked By**: Module package API research

### Performance & Optimization
**Priority**: LOW | **Effort**: Medium | **Impact**: Medium

- [ ] **Batch operations** - Better performance for multiple changes
  - [ ] Batch entity creation
  - [ ] Batch attribute updates
  - [ ] Progress reporting
  - **Status**: Not Started
  - **Blocked By**: None
  - **Notes**: Current operations are sequential

- [ ] **Transaction support** - Group related changes
  - [ ] Begin transaction
  - [ ] Commit transaction
  - [ ] Rollback on error
  - **Status**: Not Started
  - **Blocked By**: ExtensionsAPI transaction support
  - **Notes**: May already be handled by API

- [ ] **Async operations** - For long-running tasks
  - [ ] Async tool execution
  - [ ] Progress callbacks
  - [ ] Cancellation support
  - **Status**: Not Started
  - **Blocked By**: MCP async support

---

## 📚 Documentation & Developer Experience

### Tool Documentation
**Priority**: MEDIUM | **Effort**: Low | **Impact**: High

- [ ] **Parameter examples** - More comprehensive examples in tool responses
  - [ ] Add examples to all tool schemas
  - [ ] Document common patterns
  - [ ] Add troubleshooting tips
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Common patterns** - Document typical workflows
  - [ ] CRUD setup workflow
  - [ ] User management setup
  - [ ] File upload setup
  - [ ] Microflow patterns
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Error codes** - Map Mendix error codes to solutions
  - [ ] Document common errors
  - [ ] Add resolution steps
  - [ ] Link to Mendix docs
  - **Status**: Not Started
  - **Blocked By**: None

### Response Quality
**Priority**: LOW | **Effort**: Low | **Impact**: Medium

- [ ] **Consistent formatting** - All responses use similar structure
  - [ ] Standardize success messages
  - [ ] Standardize error messages
  - [ ] Use consistent JSON structure
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Progress indicators** - For operations that modify multiple items
  - [ ] Show step progress (1/5, 2/5, etc.)
  - [ ] Estimated time remaining
  - [ ] Detailed step descriptions
  - **Status**: Not Started
  - **Blocked By**: None

- [ ] **Warnings** - Alert about potential issues
  - [ ] Breaking changes warning
  - [ ] Data loss warnings
  - [ ] Performance impact warnings
  - **Status**: Not Started
  - **Blocked By**: None

---

## 🎯 Immediate Priorities (Next Sprint)

### Sprint 1: Core Entity & Domain Model Enhancements
**Target**: Complete critical domain model gaps

1. **Domain model-level annotations** - ~~Complete the annotation system~~
   - Effort: N/A
   - Priority: ~~HIGH~~ COMPLETED WITH API LIMITATION
   - Status: ✅ Entity/association annotations working. Domain model annotations blocked by API limitation.

2. **Entity generalization support** - ~~Essential for System.User inheritance~~
   - Effort: N/A
   - Priority: ~~HIGH~~ BLOCKED BY API
   - Status: ❌ API does not expose generalization functionality. Manual workaround required.

3. **Association properties** - Configure delete behavior and ownership
   - Effort: 1-2 days
   - Priority: MEDIUM
   - Dependencies: None

4. **Enumeration captions** - Display actual text instead of proxy class names
   - Effort: 1 day
   - Priority: MEDIUM
   - Dependencies: API research

### Sprint 2: Microflow Activity Expansion
**Target**: Add essential microflow activities

5. **More microflow activities** - At minimum: Retrieve, Commit, Delete, Show page, Decision
   - Effort: 5-7 days
   - Priority: HIGH
   - Dependencies: API research for each activity type

6. **Better error messages** - More actionable feedback when operations fail
   - Effort: 2 days
   - Priority: HIGH
   - Dependencies: None

---

## 📊 Metrics & Progress

### Overall Progress
- **Total Items**: 50+ feature areas
- **Completed**: 11 issues fixed this session
- **In Progress**: 0
- **Not Started**: 50+

### Priority Breakdown
- **HIGH Priority**: 8 items
- **MEDIUM Priority**: 12 items
- **LOW Priority**: 30+ items

### Effort Estimates
- **Quick Wins** (1-2 days): 10 items
- **Medium Effort** (3-5 days): 15 items
- **Large Projects** (1-2 weeks): 25+ items

---

## 🗂️ Appendix

### API Research Needed
The following items require investigation of Mendix StudioPro ExtensionsAPI capabilities:

1. Entity property configuration (persistable, image, file)
2. Attribute properties (default values, validation, calculated)
3. Database index management
4. Enumeration image support
5. Activity positioning and flow control
6. All microflow activity types
7. Page and widget manipulation
8. Security configuration
9. Module package operations
10. Transaction support

### Known API Limitations

**📄 See [API_LIMITATIONS.md](API_LIMITATIONS.md) for comprehensive documentation of all discovered limitations.**

Summary of key limitations:
- **Entity generalization/inheritance**: Cannot set entity inheritance (FileDocument, ImageDocument, User) - **HIGH IMPACT**
- **Domain model annotations**: Domain model-level documentation not exposed (only entity/association)
- **Association arrow positioning**: Visual layer not accessible through Extensions API
- **Entity configuration properties**: Cannot set persistability, access rules programmatically
- **Attribute properties**: Cannot set default values, validation rules, calculated attributes
- **Enumeration captions**: Returns TextProxy objects instead of actual text
- **Project errors**: Limited error information available through ExtensionsAPI

### Related Documents
- `README.md` - Complete tool documentation and usage examples
- `CurrentStatus.md` - Current implementation status
- `COMPILATION_FIX_SUMMARY.md` - Build and compilation notes
- **`API_LIMITATIONS.md`** - Comprehensive documentation of all API limitations and workarounds

---

**Notes**:
- This backlog is a living document and will be updated as features are completed
- Priority and effort estimates may change based on API research findings
- Some features may be blocked by ExtensionsAPI limitations
- User feedback will influence priority ordering
