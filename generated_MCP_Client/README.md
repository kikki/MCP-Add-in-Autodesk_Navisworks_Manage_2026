# generated_MCP_Client

This folder contains the **generated DXT extension** of the MCP Client.  
The `.dxt` file is produced through the workflow described in the `MCP_Client` project and can be imported into Claude or other MCP-compatible environments.

```

## Purpose
- Stores the packaged **DXT extension** generated from the MCP Client.  
- Provides a ready-to-use artifact for testing and integration.  
- Allows Claude to access the MCP service as defined by the client manifest.

```

## Files
- `waabe-navisworks-mcp.dxt` → Generated DXT extension file

```

## Usage in Claude
The DXT file can be imported directly into **Claude** to make the MCP Client service available as tools:

1. Open **Claude Desktop**.  
2. Go to **Settings → Extensions / Tools**.  
3. Add the file `waabe-navisworks-mcp.dxt` from this folder.  
4. After loading, the tools defined in the manifest (e.g., `get_model_overview`, `get_element_count_by_category`, …) are available for interaction.  

```

## Key Idea
This folder acts as the **export target** for the MCP Client.  
It contains the `.dxt` extension that can be directly embedded into Claude,  
enabling access to the Navisworks MCP service as a set of AI-usable tools.