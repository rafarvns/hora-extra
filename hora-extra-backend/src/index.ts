import express, { Request, Response, NextFunction } from 'express';
import { createServer } from 'http';
import { UdpSocketManager } from './sockets/UdpSocketManager.js';
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

// 2. Setup Sockets (UDP)
const UDP_PORT = Number(process.env.UDP_PORT) || 3001;
const udpSocketManager = UdpSocketManager.initialize(UDP_PORT);
logger.info(`UDP Socket inicializado na porta ${UDP_PORT}`, { module: 'UDP_SOCKET' });

// 3. Rotas da API REST (/api/v1/...)
app.use('/api', apiRouter);

// 4. Root (Quick check)
app.get('/', (req: Request, res: Response) => {
  res.send('Hora Extra Backend is running. Use /api/health for status.');
});

// 5. Middleware de Tratamento de Erros (sempre ao final!)
app.use(errorHandler);

// Inicializar Servidor
const HOST = '0.0.0.0'; // Escuta em todas as interfaces de rede locales
httpServer.listen(Number(PORT), HOST, () => {
  logger.info(`Hora Extra Backend rodando em http://${HOST}:${PORT}`, { module: 'SERVER' });
  logger.info(`Verifique o status em http://localhost:${PORT}/api/health`, { module: 'HEALTH' });
});

