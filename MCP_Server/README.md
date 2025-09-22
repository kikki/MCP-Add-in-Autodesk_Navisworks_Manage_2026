# MCP_Server

The `MCP_Server` folder contains the **C# solution** of the MCP Add-in for Autodesk Navisworks Manage.  
It was created with **Visual Studio 2022** and targets **.NET Framework 4.8**.

---

## Build & Deployment

- Each project defines a **Post-Build event**.  
- After a successful build, the system automatically creates a **Navisworks bundle** in the Autodesk ApplicationPlugins directory of the current user:  


%APPDATA%\Autodesk\ApplicationPlugins\waabe_navi_mcp.bundle\

- The bundle contains the compiled DLLs and the `PackageContents.xml` required by Navisworks.  
- ⚠️ The **`.bundle` folder extension is mandatory** – Navisworks will only recognize and load add-ins if the folder name ends with `.bundle`.


### Debugging
- Debugging directly with `F5` (Start with Debugger) does not work with Navisworks.  
- Use **Ctrl+F5 (Start without Debugging)** to launch Navisworks Manage with the MCP Add-in loaded.

---

## Version Scope
- The Add-in was developed and tested **exclusively with Autodesk Navisworks Manage 2026**.  
- Other versions of Navisworks have **not** been considered.  

---

## Bundle Structure
- The created bundle follows the **Navisworks bundle template starting with 2025**.  
- Numerous changes were introduced to adapt ribbon definitions and DLL loading to the 2026 environment.  
- Ribbon layout and command mapping are modeled according to the current Navisworks 2026 design.

---

## Structure of the Solution

MCP_Server/
│
├─ waabe_navi_mcp/ → UI integration (ribbon & commands)
├─ waabe_navi_mcp_server/ → Backend logic (RPC server, controllers, services)
└─ waabe_navi_shared/ → Shared infrastructure (logging, registry, ribbon constants)

- **Solution file**: `waabe_navi_mcp.sln`  
- **IDE**: Visual Studio 2022  
- **Framework**: .NET Framework 4.8  

---

## Key Idea
The `MCP_Server` solution is built on a **modular architecture**:

- **UI (waabe_navi_mcp)** → presents commands to the user.  
- **Backend (waabe_navi_mcp_server)** → executes RPC calls against the Navisworks API.  
- **Shared (waabe_navi_shared)** → provides logging, service registry, and ribbon constants used by both.  

This ensures a consistent, extensible foundation: new functions can be added in the backend and exposed in the UI without breaking the overall structure.
