import { Request, Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';

/**
 * Controller simples para monitoramento do sistema.
 */
export class HealthController extends BaseController {
  
  public getHealth = async (req: Request, res: Response, next: NextFunction) => {
    try {
      const status = {
        status: 'ok',
        timestamp: new Date().toISOString(),
        uptime: process.uptime(),
        service: 'hora-extra-backend'
      };

      return this.sendSuccess(res, status, 'API Ativa e Saudável');
    } catch (error) {
      // O tratamento de erro global cuidará disso a partir do next()
      next(error);
    }
  };
}
