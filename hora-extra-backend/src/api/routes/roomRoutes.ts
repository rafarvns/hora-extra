import { Router } from 'express';
import RoomController from '../controllers/RoomController.js';

const router = Router();

/**
 * Rotas de salas (Lobby)
 */
router.get('/', RoomController.list);
router.post('/', RoomController.create);

export default router;
