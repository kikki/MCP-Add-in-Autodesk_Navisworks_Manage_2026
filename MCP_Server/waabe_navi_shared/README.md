# waabe_navi_shared

This project contains the **shared infrastructure** used by both the UI (`waabe_navi_mcp`) and the backend (`waabe_navi_mcp_server`).

---

## Purpose
`waabe_navi_shared` exists because the MCP Add-in is designed as a **modular system**.  
Instead of duplicating common logic in each project, this shared project provides a central place for cross-cutting functionality.  
All other projects reference it to ensure consistency – for example, they all use the same logging mechanism.

---

## Responsibilities
- **LogHelper**  
  - Central logging component.  
  - Writes all events into a Markdown log file (`waabe_navi_log.md`).  
  - Uses emojis for severity levels: ℹ️ info, ⚠️ warning, ❌ error, ✅ success, 🔍 detail.  
  - Provides structured output for easier debugging and analysis.  
  - Every project in the solution can call into this logger.

- **ServiceRegistry**  
  - Acts as a simple dependency injection mechanism.  
  - Registers services at startup and makes them available across all modules.  
  - Ensures that controllers and services can resolve dependencies without tight coupling.

- **WaabeRibbon**  
  - Defines constants for ribbon tabs, panels, and button identifiers.  
  - Guarantees consistent naming and structure of the UI components in `waabe_navi_mcp`.  
  - Prevents duplication of identifiers between UI and backend.

---

## Key Idea
`waabe_navi_shared` is the **toolbox of the MCP Add-in**.  
It exists because the project is modular by design:  
- **UI**, **backend**, and future extensions all build on the same shared foundation.  
- Common functionality (like logging, service resolution, and ribbon constants) is implemented once and reused everywhere.  
- This guarantees consistency and reduces maintenance effort across the entire solution.