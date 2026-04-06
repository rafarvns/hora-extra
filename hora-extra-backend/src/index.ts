import express, { Request, Response } from 'express';

const app = express();
const PORT = process.env.PORT || 3000;

// Middleware
app.use(express.json());

// Health Check Endpoint
app.get('/health', (req: Request, res: Response) => {
  res.status(200).json({
    status: 'ok',
    timestamp: new Date().toISOString(),
    uptime: process.uptime(),
    service: 'hora-extra-backend'
  });
});

// Root Endpoint (Quick check)
app.get('/', (req: Request, res: Response) => {
  res.send('Hora Extra Backend is running. Access /health for status.');
});

app.listen(PORT, () => {
  console.log(`[SERVER] Hora Extra Backend running on http://localhost:${PORT}`);
  console.log(`[HEALTH] Check status at http://localhost:${PORT}/health`);
});
