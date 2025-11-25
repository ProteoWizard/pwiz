# TODO: Replace Panorama JSON Dynamic Parsing with Typed Models

**Status**: Backlog  
**Priority**: Medium  
**Complexity**: Medium  
**Created**: 2025-11-24

## Problem

Panorama API responses are currently parsed using `dynamic` and `JObject`, leading to:
- **Brittle code**: No compile-time validation; typos in property names fail at runtime
- **Poor discoverability**: IntelliSense doesn't show available fields
- **Maintenance burden**: Schema changes require manual search for all usage sites
- **Error-prone refactoring**: No way to track all consumers of a JSON field

### Current Implementation Example

```csharp
// PanoramaServerConnector.cs (SkylineBatch)
dynamic jsonObject = JObject.Parse(responseText);
if (jsonObject.errors != null)  // No compile-time check; silent null if schema changes
{
    foreach (dynamic error in jsonObject.errors)
    {
        errorList.Add(error.exception.ToString());  // Runtime exception if structure differs
    }
}
```

**Issues demonstrated**:
- `jsonObject.errors` could be misspelled as `jsonObject.error` without compiler warning
- `error.exception` assumes nested structure; fails silently if LabKey changes response format
- No documentation of expected schema (what other fields exist? Are they optional?)

## Proposed Solution

### Phase 1: Define Typed Response Models

Create POCOs (Plain Old CLR Objects) matching Panorama/LabKey API schema:

```csharp
// Example: PanoramaApi/Models/PanoramaErrorResponse.cs
public class PanoramaErrorResponse
{
    [JsonProperty("errors")]
    public List<PanoramaError> Errors { get; set; }
}

public class PanoramaError
{
    [JsonProperty("exception")]
    public string Exception { get; set; }

    [JsonProperty("exceptionClass")]
    public string ExceptionClass { get; set; }
}

// Example: PanoramaApi/Models/PanoramaFolderInfo.cs
public class PanoramaFolderInfo
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("effectivePermissions")]
    public List<string> EffectivePermissions { get; set; }
}
```

### Phase 2: Replace Dynamic Parsing

**Before**:
```csharp
dynamic jsonObject = JObject.Parse(responseText);
if (jsonObject.errors != null)
{
    foreach (dynamic error in jsonObject.errors)
        errorList.Add(error.exception.ToString());
}
```

**After**:
```csharp
var response = JsonConvert.DeserializeObject<PanoramaErrorResponse>(responseText);
if (response?.Errors != null)
{
    foreach (var error in response.Errors)
        errorList.Add(error.Exception);  // Compile-time validated, IntelliSense supported
}
```

### Phase 3: Leverage LabKey API Documentation

**Resources**:
- LabKey JavaScript API docs: https://www.labkey.org/Documentation/wiki-page.view?name=viewAPIs
- LabKey Server API reference: https://www.labkey.org/download/clientapi_docs/javascript-api/
- Panorama-specific endpoints (may require Panorama team coordination)

**Approach**:
1. Identify all JSON endpoints currently consumed (folder info, file lists, permissions, etc.)
2. Document expected response schema from LabKey docs or live testing
3. Generate C# models (manual or via JSON-to-C# tools like json2csharp.com)
4. Add XML doc comments referencing LabKey API endpoint and version

### Phase 4: Incremental Migration

**Priority order** (highest impact first):
1. **Error responses**: Standardize error handling across all Panorama calls
2. **Folder/file queries**: Most frequently used; high value for discoverability
3. **Permission checks**: Security-critical; benefit from explicit schema validation
4. **Metadata queries**: Lower frequency; migrate when touching related code

**Strategy**:
- Replace one endpoint at a time
- Keep dynamic fallback for unknown/optional fields during transition
- Add unit tests validating deserialization (mock JSON responses)

## Benefits

- **Type safety**: Compiler catches schema mismatches, typos, null reference issues
- **IntelliSense**: Developers see available fields, reducing need to reference docs
- **Refactoring confidence**: "Find All References" works; IDE shows all consumers
- **Self-documenting**: Model classes serve as schema reference with XML docs
- **Maintainability**: Schema changes have explicit impact analysis via compilation errors

## Example: Error Handling Improvement

### Current Fragility
```csharp
// Silent failure if response changes from {errors:[]} to {error:"message"}
dynamic jsonObject = JObject.Parse(responseText);
if (jsonObject.errors != null) { /* ... */ }
```

### Typed Robustness
```csharp
var response = JsonConvert.DeserializeObject<PanoramaErrorResponse>(responseText);
if (response?.Errors?.Any() == true)  // Explicit null checks; compiler-enforced
{
    // Clear schema expectation; easy to add alternative error formats
}
```

## Risks & Mitigations

**Risk**: LabKey schema changes break deserialization  
**Mitigation**: 
- Use `[JsonProperty(Required = Required.Default)]` for optional fields
- Add deserialization error handling with fallback to dynamic parsing
- Version API models if LabKey introduces breaking changes

**Risk**: Over-engineering for rarely-used endpoints  
**Mitigation**: Prioritize high-frequency endpoints; leave infrequently-used dynamic parsing as-is

**Risk**: Model drift from actual API responses  
**Mitigation**: Add integration tests with live Panorama instance; version models with API endpoint URLs in docs

## Implementation Checklist

- [ ] Audit all `JObject.Parse` / `dynamic` usage for Panorama responses
- [ ] Prioritize endpoints by usage frequency (error handling → folder queries → permissions → metadata)
- [ ] Create `PanoramaApi/Models` directory in CommonUtil or PanoramaClient
- [ ] Define models for top 3 endpoints (error responses, folder info, file lists)
- [ ] Add XML doc comments with LabKey API endpoint references
- [ ] Replace dynamic parsing in SkylineBatch `PanoramaServerConnector`
- [ ] Replace dynamic parsing in AutoQC (if applicable)
- [ ] Add unit tests for deserialization (mock JSON fixtures)
- [ ] Document model versioning strategy (tie to LabKey API versions)
- [ ] (Optional) Contact Panorama team for official schema definitions

## Related Work

- **Current error handling improvement** (Skyline/work/20251124_batch_tools_warning_cleanup): Added URL context to IOException; typed models would further strengthen this
- **TODO-consolidate_test_utilities.md**: Shared test fixtures for mock Panorama responses would support model testing
- **PanoramaClient shared library**: Natural home for typed models (or new `PanoramaApi.Models` namespace)

## References

- Current usage: `PanoramaServerConnector.cs` (SkylineBatch), similar patterns in AutoQC
- LabKey docs: https://www.labkey.org/Documentation/wiki-page.view?name=apis
- JSON.NET attributes: https://www.newtonsoft.com/json/help/html/SerializationAttributes.htm
- Discussion context: Error handling improvements during ReSharper warning cleanup identified dynamic parsing brittleness
