# MCP_Client

The `MCP_Client` folder provides the **client-side service** of the MCP Add-in for Autodesk Navisworks Manage.  
It contains the manifest, icons, and definition files required to describe and run the MCP service.

---

## Purpose
- Provides the **service interface** for Navisworks.  
- Defines available tools and configuration through `manifest.json`.  
- Can be exported and tested as a **DXT extension**.  
- Includes restrictions defined in the manifest – the structure must be followed exactly.  


```

---

## Workflow with DXT

You can validate, pack, and sign the extension using the `dxt` CLI:

1. **Validate the manifest**
   ```bash
   dxt validate manifest.json
   ```

2. **Pack the extension**
   ```bash
   dxt pack . waabe-navisworks-mcp.dxt
   ```

3. **(Optional) Sign the extension**  
   For testing purposes, you can use a self-signed signature:
   ```bash
   dxt sign waabe-navisworks-mcp.dxt --self-signed
   ```

---

## Testing the Client Service

The client can also be tested outside Navisworks using the MCP inspector:

```bash
npx @modelcontextprotocol/inspector node server/index.js args...
```

- `server.js` starts the service automatically on **port 1234** (default).  
- This allows validation of the manifest and service behavior before integration into Navisworks.  

---

## Key Idea
`MCP_Client` is both the **entry point** for describing MCP services (via manifest)  
and a **packaging target** (DXT extension).  
It enables local development, validation, and testing of the MCP Add-in  
independent of the Navisworks bundle deployment process.
