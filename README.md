## This Proof of Concept was carried out to better understand the **effort and potential of MCP**.

---

## Object of Investigation
The object of this Proof of Concept was **Autodesk Navisworks Manage 2026**.  
This software was selected because:  
- It can open and process a wide range of file formats (e.g. IFC)  
- MCP has so far rarely been applied in this environment  
- The Navisworks 2026 API was an **unfamiliar context**, making it ideal to evaluate accessibility and implementation effort  

The project was carried out with a trial version, which limited the timeframe to 30 days.  
The actual implementation lasted about 21 days.  

---
## Project Structure
The repository is organized into four main components: 


- MCP_Client/ → Client definition (manifest, service, DXT export workflow)
- MCP_Server/ → Visual Studio solution (UI ribbon, backend RPC server, shared infrastructure)
- generated_MCP_Client/ → Generated DXT extension (importable into Claude)
- generated_MCP_Server/ → Generated Navisworks bundle (ZIP, deployable in Navisworks)

This modular setup ensures a clear separation between **definition (Client)**, **execution (Server)**, and **packaged artifacts (Generated)**.  

---
## Purpose and Goals
This Proof of Concept was carried out to better understand the **effort and potential of MCP**.  

The MCP interface aims to create a **standardized bridge between humans, AI, and existing software**.  
This opens up new application fields:  
- Information can be queried directly from models  
- Reports can be generated automatically  
- Workflows can be enhanced with AI support  

The aim was to evaluate the **implementation effort** and the **technical accessibility** of an MCP integration in an unfamiliar environment.  

---
 
## Use Cases Explored
Two representative use cases were implemented:  
1. **Querying information** from the model  
2. **Running a small clash detection**  

The objective was not to create a stable product, but to **explore technical possibilities**.  

---
## Time Allocation
The distribution of effort was as follows:  
- **40%** → Researching API and COM examples  
- **30%** → Building the core infrastructure (Ribbon, bundle structure, `server.js`, `manifest.json`)  
- **10%** → Implementing use cases using the API  
- **20%** → Implementing use cases using COM  

---

## Key Findings
- **Read operations** were comparatively easy to implement  
- **Write operations** required greater effort and often caused crashes  
- The **MCP documentation** proved to be accessible and understandable, even for beginners  
- The **iterative infrastructure development** (Ribbon, bundles, manifest) provided valuable insights into the current state of the MCP standard  

---
## Implications
This Proof of Concept demonstrates that MCP can significantly improve the **technical accessibility of model-based information** – particularly when working with standardized formats such as **IFC**.  

Processes like information queries or report generation can be automated independently of the file format.  
MCP therefore opens new opportunities, especially for **inexperienced users**, to access file information within complex software.  

---

## Open Questions
Despite its potential, the integration of AI applications into existing software landscapes remains limited by the **capabilities provided by software vendors**.  

A key open question is whether MCP can, in the future, also support the **operation of the software itself**.  
Such an approach could help users **learn software functions interactively**.  
Currently, however, the focus – as in this Proof of Concept – remains primarily on the **file level**.

 