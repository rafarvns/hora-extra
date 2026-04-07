import { Request, Response, NextFunction } from 'express';
import { ApiError } from '../core/ApiError.js';

/**
 * Middleware central para tratamento de erros.
 * Todas as requisições que caem em erro passam por aqui para garantir uma resposta JSON padronizada.
 */
export const errorHandler = (
  err: Error,
  req: Request,
  res: Response,
  next: NextFunction
) => {
  // Se for um erro conhecido da nossa API
  if (err instanceof ApiError) {
    return res.status(err.statusCode).json({
      success: false,
      error: {
        message: err.message,
        statusCode: err.statusCode
      }
    });
  }

  // Logs do erro para debugging no servidor
  console.error(`[INTERNAL_ERROR] ${err.stack}`);

  // Para erros desconhecidos, retornar 500 Generic Error
  return res.status(500).json({
    success: false,
    error: {
      message: 'Ocorreu um erro interno inesperado no servidor.',
      statusCode: 500
    }
  });
};
