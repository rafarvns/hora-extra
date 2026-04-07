import express, { Request, Response } from 'express';
import { createServer } from 'http';
import { SocketManager } from './sockets/SocketManager.js';
import apiRouter from './api/routes/index.js';
import { errorHandler } from './middleware/errorHandler.js';

const app = express();
const httpServer = createServer(app);
const PORT = process.env.PORT || 3000;

// 1. Middlewares Globais
app.use(express.json());

// 2. Setup Sockets
const socketManager = SocketManager.initialize(httpServer);
console.log(`[SOCKET] Socket.IO inicializado.`);

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
  console.log(`[SERVER] Hora Extra Backend rodando em http://localhost:${PORT}`);
  console.log(`[HEALTH] Verifique o status em http://localhost:${PORT}/api/health`);
});

