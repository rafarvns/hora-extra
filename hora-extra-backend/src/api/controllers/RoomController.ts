import { Request, Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';
import { ApiError } from '../../core/ApiError.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

const prisma = ServiceFactory.getPrismaClient();

/**
 * Controller responsável pela gerência de salas (Lobby).
 */
export class RoomController extends BaseController {
  /**
   * Lista todas as salas abertas.
   * GET /rooms
   */
  public list = async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    try {
      const rooms = await prisma.room.findMany({
        where: { status: 'OPEN' },
        orderBy: { createdAt: 'desc' }
      });

      this.sendSuccess(res, rooms, 'Lista de salas recuperada com sucesso.');
    } catch (error) {
      next(error);
    }
  }

  /**
   * Cria uma nova sala.
   * POST /rooms
   */
  public create = async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    try {
      const { nome, hostId } = req.body;

      if (!nome || !hostId) {
        throw ApiError.badRequest('NOME e HOSTID são obrigatórios.');
      }

      const room = await prisma.room.create({
        data: {
          nome,
          hostId,
          playerCount: 1, // O host já entra na sala
          maxPlayers: 4,
          status: 'OPEN'
        }
      });

      logger.info(`Nova sala criada: ${room.nome} (Host: ${hostId})`, { 
        module: 'ROOM', 
        roomId: room.id 
      });

      this.sendCreated(res, room, 'Sala criada com sucesso!');
    } catch (error) {
      next(error);
    }
  }
}

export default new RoomController();
