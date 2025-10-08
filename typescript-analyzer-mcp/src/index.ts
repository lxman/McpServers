#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  Tool,
} from '@modelcontextprotocol/sdk/types.js';
import { TypeScriptTools } from './tools/TypeScriptTools.js';

// Create the TypeScript tools instance
const tsTools = new TypeScriptTools();

// Define the available tools
const tools: Tool[] = [
  {
    name: 'analyze_code',
    description: 'Analyze TypeScript code for errors, warnings, and diagnostics using Roslyn',
    inputSchema: {
      type: 'object',
      properties: {
        code: {
          type: 'string',
          description: 'TypeScript code to analyze',
        },
        filePath: {
          type: 'string',
          description: 'Optional file path for context',
        },
      },
      required: ['code'],
    },
  },
  {
    name: 'get_symbols',
    description: 'Get all symbols (classes, methods, properties, etc.) from TypeScript code',
    inputSchema: {
      type: 'object',
      properties: {
        code: {
          type: 'string',
          description: 'TypeScript code to analyze',
        },
        filePath: {
          type: 'string',
          description: 'Optional file path for context',
        },
        filter: {
          type: 'string',
          description: "Optional filter: 'class', 'interface', 'function', 'method', 'property', 'enum', or 'all'",
        },
      },
      required: ['code'],
    },
  },
  {
    name: 'get_type_info',
    description: 'Get type information at a specific position in TypeScript code',
    inputSchema: {
      type: 'object',
      properties: {
        code: {
          type: 'string',
          description: 'TypeScript code to analyze',
        },
        line: {
          type: 'number',
          description: 'Line number (1-based)',
        },
        column: {
          type: 'number',
          description: 'Column number (1-based)',
        },
        filePath: {
          type: 'string',
          description: 'Optional file path for context',
        },
      },
      required: ['code', 'line', 'column'],
    },
  },
  {
    name: 'format_code',
    description: 'Format TypeScript code using Roslyn formatting rules',
    inputSchema: {
      type: 'object',
      properties: {
        code: {
          type: 'string',
          description: 'TypeScript code to format',
        },
        filePath: {
          type: 'string',
          description: 'Optional file path for context',
        },
      },
      required: ['code'],
    },
  },
  {
    name: 'calculate_metrics',
    description: 'Calculate code metrics including cyclomatic complexity, lines of code, and more',
    inputSchema: {
      type: 'object',
      properties: {
        code: {
          type: 'string',
          description: 'TypeScript code to analyze',
        },
        filePath: {
          type: 'string',
          description: 'Optional file path for context',
        },
      },
      required: ['code'],
    },
  },
];

// Create the MCP server
const server = new Server(
  {
    name: 'typescript-analyzer-mcp',
    version: '1.0.0',
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

// Handle list tools request
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools,
  };
});

// Handle tool call requests
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  if (!args) {
    throw new Error('Arguments are required');
  }

  try {
    switch (name) {
      case 'analyze_code': {
        const result = tsTools.analyzeCode({
          code: args.code as string,
          fileName: args.filePath as string | undefined,
        });
        return {
          content: [
            {
              type: 'text',
              text: JSON.stringify(result, null, 2),
            },
          ],
        };
      }

      case 'get_symbols': {
        const result = tsTools.getSymbols({
          code: args.code as string,
          fileName: args.filePath as string | undefined,
          filter: args.filter as any,
        });
        return {
          content: [
            {
              type: 'text',
              text: JSON.stringify(result, null, 2),
            },
          ],
        };
      }

      case 'get_type_info': {
        const result = tsTools.getTypeInfo({
          code: args.code as string,
          line: args.line as number,
          column: args.column as number,
          fileName: args.filePath as string | undefined,
        });
        return {
          content: [
            {
              type: 'text',
              text: JSON.stringify(result, null, 2),
            },
          ],
        };
      }

      case 'format_code': {
        const result = tsTools.formatCode({
          code: args.code as string,
          fileName: args.filePath as string | undefined,
        });
        return {
          content: [
            {
              type: 'text',
              text: JSON.stringify(result, null, 2),
            },
          ],
        };
      }

      case 'calculate_metrics': {
        const result = tsTools.calculateMetrics({
          code: args.code as string,
          fileName: args.filePath as string | undefined,
        });
        return {
          content: [
            {
              type: 'text',
              text: JSON.stringify(result, null, 2),
            },
          ],
        };
      }

      default:
        throw new Error(`Unknown tool: ${name}`);
    }
  } catch (error) {
    return {
      content: [
        {
          type: 'text',
          text: JSON.stringify(
            {
              success: false,
              error: error instanceof Error ? error.message : 'Unknown error occurred',
            },
            null,
            2
          ),
        },
      ],
      isError: true,
    };
  }
});

// Start the server
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  
  console.error('TypeScript Analyzer MCP Server running on stdio');
}

main().catch((error) => {
  console.error('Fatal error:', error);
  process.exit(1);
});