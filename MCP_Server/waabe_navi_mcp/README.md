# waabe_navi_mcp

This project integrates the MCP Add-in into the **Navisworks user interface**.  
It provides the ribbon interface where users can directly access MCP functionality.

## Purpose
The project is responsible for exposing MCP functionality in Navisworks through a dedicated ribbon tab.  
Each tab is handled separately and can be extended depending on the use case.  
The project defines the button structure, their placement, and their registration inside Navisworks.

## Responsibilities
- Current state: Implements the **MCP ribbon** with buttons for  
  - starting/stopping the server (with manual port selection)  
  - an information button for user guidance.  
- Connects user interactions to backend logic by linking button events to commands.  
- Uses `PackageContents.xml` to register the add-in and its DLLs with Navisworks.

## Key Idea
This project provides the **UI for navigation** inside Navisworks.  
It uses the `PackageContents.xml` to register the add-in with Navisworks,  
defining ribbon tabs, button groups, command IDs, and associated icons.  
Through this XML, the DLL is linked into the Navisworks environment and  
the MCP ribbon becomes available to the user.
