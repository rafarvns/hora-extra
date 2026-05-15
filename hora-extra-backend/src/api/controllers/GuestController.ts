import { Request, Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

/**
 * GuestController: Endpoint público para acesso anônimo (guest).
 * Não requer autenticação — gera um JWT temporário sem cadastro.
 */
export class GuestController extends BaseController {

    /**
     * POST /api/auth/guest
     * Cria uma identidade guest e retorna JWT + roomId da sala compartilhada.
     */
    public joinAsGuest = async (req: Request, res: Response, next: NextFunction): Promise<void> => {
        try {
            const guestService = ServiceFactory.getGuestService();
            const result = await guestService.joinAsGuest();

            logger.info(`Novo guest criado via HTTP: ${result.guestId}`, { module: 'GUEST_AUTH' });

            this.sendCreated(res, result, 'Acesso guest criado com sucesso');
        } catch (err) {
            next(err);
        }
    };
}

export default new GuestController();
