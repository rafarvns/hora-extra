import { Router } from 'express';
import healthRoutes from './healthRoutes.js';
import authRoutes from './authRoutes.js';
import roomRoutes from './roomRoutes.js';

const router = Router();

/**
 * Agregador central de rotas REST
 */
router.use('/health', healthRoutes);
router.use('/auth', authRoutes);
router.use('/rooms', roomRoutes);

export default router;
