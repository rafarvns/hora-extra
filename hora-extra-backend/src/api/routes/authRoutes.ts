import { Router } from 'express';
import authController from '../controllers/AuthController.js';

const router = Router();

/**
 * Rotas de Autenticação
 */

// POST /auth/register
router.post('/register', authController.register);

// POST /auth/login
router.post('/login', authController.login);

export default router;
