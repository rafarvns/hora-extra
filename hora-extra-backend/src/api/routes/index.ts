import { Router } from 'express';
import healthRoutes from './healthRoutes.js';
import sessionRoutes from './sessionRoutes.js';
import roomRoutes from './roomRoutes.js';

const router = Router();

/**
 * Agregador central de rotas REST
 */
router.use('/health', healthRoutes);
router.use('/session', sessionRoutes);
router.use('/rooms', roomRoutes);

export default router;
