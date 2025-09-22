# generated_MCP_Server

This folder contains the **generated bundle ZIP** of the MCP Server.  
The ZIP archive is created through the `MCP_Server` Visual Studio projects and represents the deployable Navisworks Add-in.

```

## Purpose
- Stores the generated **Navisworks bundle** as a ZIP file.  
- Provides the complete Add-in package (DLLs, `PackageContents.xml`, resources).  
- Can be deployed directly into Autodesk Navisworks Manage.

```

## Deployment

1. Extract the ZIP archive from this folder.  
2. Copy the extracted folder into the Autodesk ApplicationPlugins directory of your user profile:  

%APPDATA%\Autodesk\ApplicationPlugins\

4. On the next start of **Navisworks Manage 2026**, the MCP Add-in will be available.

```

## How it is generated
- The bundle is automatically created by the **Post-Build events** in the `MCP_Server` solution.  
- Each project copies its compiled DLLs and the `PackageContents.xml` into the bundle structure.  
- The result is packaged into the ZIP file stored here.

```

## What the bundle does
- Registers the MCP Add-in with Navisworks (via `PackageContents.xml`).  
- Loads the compiled DLLs to provide:
- The **Ribbon UI** (buttons to start/stop server, info dialogs).  
- The **RPC server** for model queries, selections, and clash detection.  
- The **shared infrastructure** for logging and service registry.  

```

## Key Idea
This folder holds the **ready-to-deploy Add-in bundle**.  
By extracting it into the Navisworks ApplicationPlugins folder, you can immediately load and use the MCP Add-in in Navisworks Manage 2026.