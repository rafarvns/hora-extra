import { Response } from 'express';
import { ApiResponse } from './ApiResponse.js';

/**
 * Superclasse para controladores, facilitando respostas padronizadas.
 */
export abstract class BaseController {
  
  protected sendSuccess<T>(res: Response, data: T, message?: string, status: number = 200) {
    return res.status(status).json(ApiResponse.success(data, message, status));
  }

  protected sendCreated<T>(res: Response, data: T, message?: string) {
    return this.sendSuccess(res, data, message, 201);
  }

  // Erros serão capturados pelo catch do controller and passados para o next()
}
