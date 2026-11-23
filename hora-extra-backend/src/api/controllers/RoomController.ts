import { Request, Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';

/**
 * Controller das salas de jogo. As salas vivem em memória (RoomRegistry); quem entra
 * de fato é o CONN do UDP com o roomId.
 */
export class RoomController extends BaseController {
  /**
   * GET /rooms → [{ id, nome, jogadores, max, emAndamento }]
   */
  public list = async (_req: Request, res: Response, next: NextFunction): Promise<void> => {
    try {
      this.sendSuccess(res, ServiceFactory.getRoomRegistry().list(), 'Lista de salas.');
    } catch (error) {
      next(error);
    }
  };

  /**
   * POST /rooms { nome } → { id, nome }
   */
  public create = async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    try {
      const room = ServiceFactory.getRoomRegistry().create(req.body?.nome);
      this.sendCreated(res, { id: room.id, nome: room.nome }, 'Sala criada.');
    } catch (error) {
      next(error);
    }
  };
}

export default new RoomController();
