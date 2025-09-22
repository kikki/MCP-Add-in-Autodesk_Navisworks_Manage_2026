#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ErrorCode,
  ListToolsRequestSchema,
  McpError,
} from '@modelcontextprotocol/sdk/types.js';

 

class NavisworksMCPServer {
  constructor() {
    this.server = new Server(
      { name: 'waabe-navisworks-mcp', version: '1.0.0' },
      { capabilities: { tools: {} } }
    );

    this.navisworksApiUrl = this.getNavisworksApiUrl();
    this.setupToolHandlers();
  }

   
  getNavisworksApiUrl() {
    const port = process.env.NAVISWORKS_API_PORT || '1234';
    return `http://localhost:${port}`;
  }

   
  async rpc(method, params = {}) {
    const body = { id: String(Date.now()), method, params };
    const resp = await fetch(`${this.navisworksApiUrl}/rpc`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (!resp.ok) throw new Error(`HTTP ${resp.status}: ${resp.statusText}`);
    const data = await resp.json();
    if (!data || typeof data.ok !== 'boolean') {
      throw new Error('Ungültige RPC-Antwort (kein ok-Feld).');
    }
    if (!data.ok) {
      const code = data?.error?.code ?? 'UNKNOWN';
      const msg = data?.error?.msg ?? 'Unbekannter Fehler';
      throw new McpError(ErrorCode.InternalError, `RPC-Fehler ${code}: ${msg}`);
    }
    return data.data;
  }

   
  normalizeTokens(v) {
    if (Array.isArray(v)) return v.map(x => String(x ?? '').trim()).filter(Boolean);
    if (typeof v === 'string') {
      return v
        .split(/[,;\r\n]/g)
        .map(s => s.trim())
        .filter(Boolean);
    }
    return [];
  }

   
  toDelimitedString(v) {
    const arr = this.normalizeTokens(v);
    return arr.join(',');
  }

   
  setupToolHandlers() {
     
    this.server.setRequestHandler(ListToolsRequestSchema, async () => {
      return {
        tools: [
          {
            name: 'get_model_overview',
            description: 'Gibt eine Übersicht über die geladenen Modelle.',
            inputSchema: { type: 'object', properties: {} }
          },

          {
            name: 'get_element_count_by_category',
            description: 'Zählt Elemente einer Kategorie. scope="all" (Default) oder Liste aus model canonical_id / model name (Komma, Semikolon, Zeilenumbruch).',
            inputSchema: {
              type: 'object',
              properties: {
                category: { type: 'string', description: 'z. B. IfcSpace, Doors, Windows' },
                scope:    { type: 'string', description: '"all" oder Liste von IDs/Namen, getrennt durch , ; oder Zeilenumbrüche', default: 'all' }
              },
              required: ['category']
            }
          },

          {
            name: 'get_property_distribution_by_category',
            description: 'Übersicht aller Modelle mit Property-Kategorien und Zählungen.',
            inputSchema: { type: 'object', properties: {} },
            output_schema: {
              type: 'object',
              properties: {
                category: { type: 'string' },
                count: { type: 'integer' },
                scope: { type: 'string' },
                success: { type: 'boolean' },
                message: { type: 'string' },
                models: {
                  type: 'array',
                  items: {
                    type: 'object',
                    properties: {
                      modelId: { type: 'string' },
                      categories: {
                        type: 'array',
                        items: {
                          type: 'object',
                          properties: {
                            category: { type: 'string' },
                            properties: {
                              type: 'array',
                              items: {
                                type: 'object',
                                properties: {
                                  property: { type: 'string' },
                                  count: { type: 'integer' }
                                },
                                required: ['property', 'count']
                              }
                            }
                          },
                          required: ['category', 'properties']
                        }
                      }
                    },
                    required: ['modelId', 'categories']
                  }
                }
              },
              required: ['category', 'count', 'scope', 'success', 'models']
            }
          },

          {
            name: 'list_properties_for_item',
            description: 'Eigenschaften für Item ermitteln.',
            inputSchema: {
              type: 'object',
              properties: { canonical_id: { type: 'string' } },
              required: ['canonical_id']
            }
          },

          {
            name: 'list_items_to_property',
            description: 'Listet Items nach Kategorie/Property; optional mit Modell- und Value-Filter (Teilstring, >=, <=, Regex). modelFilter und scope akzeptieren IDs oder Modellnamen; mehrere Tokens per , ; \\n.',
            inputSchema: {
              type: 'object',
              properties: {
                category:   { type: 'string' },
                property:   { type: 'string' },
                scope:      { type: 'string',  description: '"all" oder Liste von model canonical_id / model name', default: 'all' },
                modelFilter:{
                  oneOf: [
                    { type: 'string', description: 'Ein Token oder kommagetrennte Liste' },
                    { type: 'array',  items: { type: 'string' }, description: 'Liste von Tokens' }
                  ]
                },
                valueFilter:{ type: 'string' },
                ignoreCase: { type: 'boolean', default: true },
                maxResults: { type: 'number' }
              },
              required: ['category','property']
            }
          },

          { name: 'clear_selection', description: 'Leert Selektion.', inputSchema: { type: 'object', properties: {} } },

          { name: 'get_current_selection_snapshot', description: 'Aktuelle Auswahl als Liste.', inputSchema: { type: 'object', properties: {} } },

          {
            name: 'apply_selection',
            description: 'Setzt die Selektion. Input: canonical_id[].',
            inputSchema: {
              type: 'object',
              properties: {
                canonical_id: { type: 'array', items: { type: 'string' } },
                keepExistingSelection: { type: 'boolean', default: true }
              },
              required: ['canonical_id']
            }
          },
          {
            name: 'run_simple_clash',
            description: 'Führt einen einfachen Hard-Clash zwischen zwei Scopes aus. Input: scopeA, scopeB, tolerance_m?, test_name?',
            inputSchema: {
              type: 'object',
              properties: {
                scopeA:      { type: 'string', description: '"all" oder Liste von IDs/Namen (getrennt per , ; \\n)', default: 'all' },
                scopeB:      { type: 'string', description: '"all" oder Liste von IDs/Namen (getrennt per , ; \\n)', default: 'all' },
                tolerance_m: { type: 'number', description: 'Toleranz in Metern', default: 0.01 },
                test_name:   { type: 'string',  description: 'Anzeigename des Tests', default: 'MCP API Test' }
              }
            }
          },
          { name: 'get_units_and_tolerances', description: 'Längen/Flächen/Volumen-Einheiten + Toleranzen.', inputSchema: { type: 'object', properties: {} } },
        ],
      };
    });

     
    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args } = request.params;

      try {
        switch (name) {
           
          case 'get_model_overview':       return await this.t_model_overview();
          case 'get_units_and_tolerances': return await this.t_simple('get_units_and_tolerances');
          case 'get_property_distribution_by_category': return await this.t_property_distribution_by_category();

          case 'list_items_to_property': {
            const category = typeof args?.category === 'string' ? args.category.trim() : '';
            const property = typeof args?.property === 'string' ? args.property.trim() : '';
            if (!category) throw new McpError(ErrorCode.InvalidParams, 'category fehlt oder ist leer.');
            if (!property) throw new McpError(ErrorCode.InvalidParams, 'property fehlt oder ist leer.');

             
            const scopeRaw     = (typeof args?.scope === 'string' && args.scope.trim()) ? args.scope.trim() : 'all';
            const scope        = scopeRaw === 'all' ? 'all' : this.toDelimitedString(scopeRaw);
            const modelFilter  = (args?.modelFilter !== undefined) ? this.toDelimitedString(args.modelFilter) : undefined;

            const valueFilter  = typeof args?.valueFilter === 'string' ? args.valueFilter : undefined;
            const ignoreCase   = typeof args?.ignoreCase === 'boolean' ? args.ignoreCase : true;
            const maxResults   = (typeof args?.maxResults === 'number' && isFinite(args.maxResults)) ? args.maxResults : undefined;

            return await this.t_list_items_to_property({ category, property, scope, modelFilter, valueFilter, ignoreCase, maxResults });
          }

          case 'get_element_count_by_category': {
            const cat = typeof args?.category === 'string' ? args.category.trim() : '';
            if (!cat) throw new McpError(ErrorCode.InvalidParams, 'category fehlt oder ist leer.');
            const scopeRaw = (typeof args?.scope === 'string' && args.scope.trim()) ? args.scope.trim() : 'all';
            const scope    = scopeRaw === 'all' ? 'all' : this.toDelimitedString(scopeRaw);
            return await this.t_count_by_category(cat, scope);
          }

          case 'list_properties_for_item': {
            const id = args?.item_id ?? args?.canonical_id; // fallback für altes schema
            return await this.t_list_properties_for_item(id);
          };

           
          case 'clear_selection':                return await this.t_simple('clear_selection');
          case 'get_current_selection_snapshot': return await this.t_simple('get_current_selection_snapshot');

          case 'apply_selection': {
            const ids = Array.isArray(args?.canonical_id) ? args.canonical_id : [];
            if (ids.length === 0 || !ids.every(v => typeof v === 'string')) {
              throw new McpError(ErrorCode.InvalidParams, 'canonical_id[] (string) erforderlich.');
            }
            const keep = (typeof args?.keepExistingSelection === 'boolean') ? args.keepExistingSelection : true;
            return await this.t_apply_selection(ids, keep);
          };
           
          case 'run_simple_clash': {
            const scopeAraw   = (typeof args?.scopeA === 'string') ? args.scopeA : 'all';
            const scopeBraw   = (typeof args?.scopeB === 'string') ? args.scopeB : 'all';
            const tolerance_m = (typeof args?.tolerance_m === 'number' && isFinite(args.tolerance_m)) ? args.tolerance_m : 0.01;
            const test_name   = (typeof args?.test_name === 'string' && args.test_name.trim()) ? args.test_name.trim() : 'MCP API Test';

            return await this.t_run_simple_clash(scopeAraw, scopeBraw, tolerance_m, test_name);
          };

          default:
            throw new McpError(ErrorCode.MethodNotFound, `Unbekanntes Tool: ${name}`);
        }
      } catch (error) {
        if (error instanceof McpError) throw error;
        throw new McpError(ErrorCode.InternalError, `Fehler bei Tool-Ausführung: ${error.message}`);
      }
    });
  }

   
  async t_list_items_to_property(a) {
    const dto = await this.rpc('list_items_to_property', {
      Category:    a.category,
      Property:    a.property,
      Scope:       a.scope,         
      ModelFilter: a.modelFilter,   
      ValueFilter: a.valueFilter,
      IgnoreCase:  a.ignoreCase,
      MaxResults:  a.maxResults
    });
    return { content: [{ type: 'text', text: '```json\n' + JSON.stringify(dto, null, 2) + '\n```' }] };
  }

  async t_simple(method) {
    const data = await this.rpc(method, {});
    return { content: [{ type: 'text', text: '```json\n' + JSON.stringify(data, null, 2) + '\n```' }] };
  }

  async t_model_overview() {
    const overview = await this.rpc('get_model_overview', {});

    const modelsCount = overview?.ModelsCount ?? 0;
    const total       = overview?.TotalElements ?? 0;
    const docTitle    = overview?.DocumentTitle || 'Unbenannt';

    const details = Array.isArray(overview?.Models) ? overview.Models : [];
    const hist    = overview?.categories_histogram || {};

    const modelLines = details.map((m, i) => {
      const model_name = m?.FileName || m?.DisplayName || '(unbenannt)';
      const typ  = m?.SourceFileName || '';     // ".ifc", ".rvt", ...
      const cid  = m?.canonical_id || '';
      const kids = Number.isFinite(m?.ChildrenCount) ? m.ChildrenCount : 0;
      const desc = Number.isFinite(m?.DescendantsCount) ? m.DescendantsCount : 0;
      const parent_canonical_id = m?.perent_canonical_id || '';
      return `- ${i + 1}. ${model_name}${typ ? ` ${typ}` : ''}${cid ? ` — canonical_id: ${cid}` : ''}${parent_canonical_id ? ` parent_canonical_id: ${parent_canonical_id}` : ''} (direct_children: ${kids}, node_count_including_self: ${desc})`;
    });

    const histLines = Object.keys(hist).map(k => `- ${k}: ${hist[k]}`);

    const jsonModels = details.map(m => ({
      canonical_id: m?.canonical_id ?? null,
      parent_canonical_id: m?.perent_canonical_id ?? 'xx',
      name: m?.FileName || m?.DisplayName || null,
      type: m?.SourceFileName || null,
      direct_children: Number.isFinite(m?.ChildrenCount) ? m.ChildrenCount : 0,
      parent_including_self: Number.isFinite(m?.DescendantsCount) ? m.DescendantsCount : 0,
    }));

    const jsonStr = JSON.stringify(
      { modelsCount, total, document: docTitle, Models: jsonModels },
      null,
      2
    );

    const txt = `\n\`\`\`json\n${jsonStr}\n\`\`\``;
    return { content: [{ type: 'text', text: txt }] };
  }

  async t_property_distribution_by_category() {
    const dto = await this.rpc('get_property_distribution_by_category', {});

    let details = null;
    try {
      details = typeof dto?.details === 'string' ? JSON.parse(dto.details) : (dto?.details ?? null);
    } catch {
      details = null;
    }

    const models = details
      ? Object.entries(details).map(([modelId, categories]) => ({
          modelId,
          categories: Object.entries(categories).map(([categoryName, props]) => ({
            category: categoryName,
            properties: Object.entries(props).map(([property, count]) => ({
              property,
              count: typeof count === 'number' ? count : Number(count) || 0,
            })),
          })),
        }))
      : [];

    const payload = {
      category: dto?.category ?? '(all)',
      count: Number(dto?.count ?? 0),
      scope: dto?.scope ?? 'all',
      success: Boolean(dto?.success ?? true),
      models,
    };

    return {
      content: [
        { type: 'text', text: '```json\n' + JSON.stringify(payload, null, 2) + '\n```' },
      ],
    };
  }

  async t_count_by_category(category, scope = 'all') {
    if (!category) throw new McpError(ErrorCode.InvalidParams, 'category fehlt.');
    const dto = await this.rpc('get_element_count_by_category', { category, scope });
    return { content: [{ type: 'text', text: '```json\n' + JSON.stringify(dto, null, 2) + '\n```' }] };
  }

  async t_list_properties_for_item(item_id) {
    if (!item_id) throw new McpError(ErrorCode.InvalidParams, 'item_id fehlt.');
    const dto = await this.rpc('list_properties_for_item', { item_id });
    return { content: [{ type: 'text', text: '```json\n' + JSON.stringify(dto, null, 2) + '\n```' }] };
  }

  async t_apply_selection(canonicalIds = [], keepExistingSelection = true) {
    if (!Array.isArray(canonicalIds) || canonicalIds.length === 0) {
      throw new McpError(ErrorCode.InvalidParams, 'canonical_id[] erforderlich.');
    }

    const dto = await this.rpc('apply_selection', {
      canonical_id: canonicalIds,
      keepExistingSelection
    });

    return {
      content: [{ type: 'text', text: '```json\n' + JSON.stringify(dto, null, 2) + '\n```' }]
    };
  }

  async t_run_simple_clash(scopeAraw, scopeBraw, tolerance_m, test_name) {
    const scopeA = (scopeAraw && scopeAraw.trim() !== '' && scopeAraw.trim() !== 'all')
      ? this.toDelimitedString(scopeAraw)
      : 'all';
    const scopeB = (scopeBraw && scopeBraw.trim() !== '' && scopeBraw.trim() !== 'all')
      ? this.toDelimitedString(scopeBraw)
      : 'all';

    const dto = await this.rpc('run_simple_clash', {
      scopeA,
      scopeB,
      tolerance_m,
      test_name
    });

    return { content: [{ type: 'text', text: '```json\n' + JSON.stringify(dto, null, 2) + '\n```' }] };
  }

   
  async run() {
    const transport = new StdioServerTransport();
    await this.server.connect(transport);

    process.on('SIGINT', async () => {
      await this.server.close();
      process.exit(0);
    });
  }
}

 
const server = new NavisworksMCPServer();
server.run().catch(console.error);
