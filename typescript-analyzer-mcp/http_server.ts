#!/usr/bin/env node
import express from 'express';
import cors from 'cors';
import swaggerUi from 'swagger-ui-express';
import { RegisterRoutes } from './build/routes.js';
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const swaggerDocument = JSON.parse(
  readFileSync(join(__dirname, 'build', 'swagger.json'), 'utf-8')
);

const app = express();
const SERVER_PORT = 7302;

app.use(cors());
app.use(express.json());

// Serve OpenAPI spec at /description (compatible with DirectoryMcp)
app.get('/description', (req, res) => {
  res.json(swaggerDocument);
});

// Serve Swagger UI at /docs
app.use('/docs', swaggerUi.serve, swaggerUi.setup(swaggerDocument));

// Register auto-generated routes
RegisterRoutes(app);

app.listen(SERVER_PORT, () => {
  console.log(`TypeScript Analyzer HTTP Server starting on port ${SERVER_PORT}`);
  console.log(`OpenAPI documentation available at: http://localhost:${SERVER_PORT}/description`);
  console.log(`Swagger UI available at: http://localhost:${SERVER_PORT}/docs`);
});
