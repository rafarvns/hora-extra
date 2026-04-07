import express, { Request, Response, NextFunction } from 'express';
import { createServer } from 'http';
import { SocketManager } from './sockets/SocketManager.js';
import apiRouter from './api/routes/index.js';
import { errorHandler } from './middleware/errorHandler.js';
import logger from './utils/Logger.js';

const app = express();
const httpServer = createServer(app);
const PORT = process.env.PORT || 3000;

// 1. Middlewares Globais
app.use(express.json());

// Middleware simples para log de requisições HTTP
app.use((req: Request, res: Response, next: NextFunction) => {
  const { method, url } = req;
  const start = Date.now();

  res.on('finish', () => {
    const duration = Date.now() - start;
    const { statusCode } = res;
    logger.info(`${method} ${url} ${statusCode} - ${duration}ms`, { module: 'HTTP' });
  });

  next();
});

// 2. Setup Sockets
const socketManager = SocketManager.initialize(httpServer);
logger.info('Socket.IO inicializado.', { module: 'SOCKET' });

// 3. Rotas da API REST (/api/v1/...)
app.use('/api', apiRouter);

// 4. Root (Quick check)
app.get('/', (req: Request, res: Response) => {
  res.send('Hora Extra Backend is running. Use /api/health for status.');
});

// 5. Middleware de Tratamento de Erros (sempre ao final!)
app.use(errorHandler);

// Inicializar Servidor
httpServer.listen(PORT, () => {
  logger.info(`Hora Extra Backend rodando em http://localhost:${PORT}`, { module: 'SERVER' });
  logger.info(`Verifique o status em http://localhost:${PORT}/api/health`, { module: 'HEALTH' });
});

