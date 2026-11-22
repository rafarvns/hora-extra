import { Request, Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';

/**
 * SessionController: entrada no jogo só com o nome (sem cadastro).
 */
export class SessionController extends BaseController {
    /**
     * POST /api/session  { nome } → { token, playerId, nome }
     */
    public create = async (req: Request, res: Response, next: NextFunction): Promise<void> => {
        try {
            const result = ServiceFactory.getSessionService().create(req.body?.nome);
            this.sendCreated(res, result, 'Sessão criada.');
        } catch (err) {
            next(err);
        }
    };
}

export default new SessionController();
