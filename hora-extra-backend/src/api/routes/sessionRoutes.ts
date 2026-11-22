import { Router } from 'express';
import SessionController from '../controllers/SessionController.js';

const router = Router();

/** Entrada no jogo só com o nome. */
router.post('/', SessionController.create);

export default router;
