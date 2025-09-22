# waabe_navi_mcp_server

This project provides the **functional core** of the MCP Add-in.  
It acts as the **RPC server** and executes all MCP operations inside Autodesk Navisworks.

---

## Purpose
The server is the backend of the MCP architecture.  
It receives requests from the MCP Client (JavaScript/Bridge layer), translates them into Navisworks API calls, and returns structured results.  
It ensures that model queries, property access, selections, and clash tests can be triggered programmatically and are exposed as stable RPC endpoints.

---

## Responsibilities

### Contracts
- Define all **data transfer objects (DTOs)** used between client and server.  
- Examples:
  - `ElementCountDto { category, count, scope }`  
  - `ClashSummaryDto { success, test_name, results, message }`  
  - `PropertyDto { category, name, type, value }`  
- Ensure strongly typed and predictable responses for all RPC calls.

### Controllers
- Map **RPC calls to backend services**.  
- Each RPC method is represented by a controller endpoint, e.g.:
  - `get_model_overview` → Collects loaded submodels and returns an overview DTO  
  - `get_element_count_by_category` → Delegates to the counting service  
  - `list_properties_for_item` → Retrieves all properties for a given model item  
  - `apply_selection` → Programmatically selects items in Navisworks  
  - `run_simple_clash` → Starts a clash detection run and returns a structured result list

### Services
- Contain the **business logic** that directly interacts with the Navisworks API.  
- Responsibilities include:
  - Iterating through `RootItems` and their `Descendants` to collect elements.  
  - Accessing and normalizing **VariantData** (text, numbers, lengths, areas, volumes, booleans).  
  - Implementing **selection handling** with reentrancy guards (`IsAutoSelectionInProgress`) to prevent recursive crashes.  
  - Executing clash tests using the **Navisworks Clash Engine** and summarizing results.  

### Infrastructure
- Provides the **dynamic dispatch system** for RPC calls:
  - `BackendResolver` decides whether a request goes through `FallbackBackend` or `ReflectionBackend`.  
  - `FallbackBackend` → contains predefined implementations for core functions like element counting.  
  - `ReflectionBackend` → resolves calls dynamically via Reflection based on `IWaabeNavisworksBackend`.  
- Ensures that new functions can be added quickly without breaking the overall system.  
- Integrates shared utilities:
  - Logging (`LogHelper` → Markdown logs with severity emojis)  
  - Service registry (`ServiceRegistry` → for global dependency resolution)

---

## Project Architecture

The overall processing chain can be illustrated with the example of **Count by Category**:

Bridge Layer (Client)
t_count_by_category(category, scope)
↓

RPC Layer
"get_element_count_by_category"
↓

Backend Resolver
↓

Generic fallback system
↓

Uses IWaabeNavisworksBackend
↓

Invokes methods dynamically via Reflection
↓

Backends
↓

FallbackBackend → GetElementCountByCategoryAsync
↓

ReflectionBackend → GetElementCountByCategoryAsync
↓

DTO Layer
ElementCountDto { category, count, scope }


This layered approach applies to all other functions as well (properties, selection, clash).

---

## Key Idea
`waabe_navi_mcp_server` is the **brain** of the MCP Add-in.  
It transforms abstract client calls into concrete Navisworks operations and returns structured results.  
By separating **Contracts, Controllers, Services, and Infrastructure**, it achieves:
- Stable external RPC endpoints  
- Flexible internal resolution via fallback/reflection  
- Strongly typed results for reliable client integration  