import { Router } from 'express';
import guestController from '../controllers/GuestController.js';

const router = Router();

/**
 * Rotas de acesso Guest (sem autenticação prévia)
 */

// POST /auth/guest → cria identidade guest e retorna JWT + roomId
router.post('/', guestController.joinAsGuest);

export default router;
