import { Router } from 'express';
import { HealthController } from '../controllers/HealthController.js';

const router = Router();
const healthController = new HealthController();

/**
 * Endpoints: /health
 */
router.get('/', healthController.getHealth);

export default router;
