import { Response, NextFunction } from 'express';
import { AuthRequest } from '../../types/AuthRequest.js';
import authMiddleware from '../../middleware/authMiddleware.js';

/**
 * Decorator (Annotation) para proteger métodos de controladores que exigem autenticação.
 * 
 * Uso:
 * 
 * @Authorize()
 * public async meuMetodo(req: AuthRequest, res: Response, next: NextFunction) {
 *    // req.jogadorId está disponível aqui
 * }
 * 
 * Nota: Este decorator funciona apenas com métodos de classe padrão (não arrow functions).
 */
export function Authorize() {
  return function (target: any, propertyKey: string, descriptor: PropertyDescriptor) {
    const originalMethod = descriptor.value;

    if (!originalMethod) {
      throw new Error(`@Authorize só pode ser usado em métodos de classe.`);
    }

    descriptor.value = function (req: AuthRequest, res: Response, next: NextFunction) {
      try {
        // Executa a lógica de autenticação do middleware
        authMiddleware.authenticate(req, res, () => {
          // Se o middleware chamar next(), ele executa o método original
          return originalMethod.apply(this, [req, res, next]);
        });
      } catch (err) {
        // Captura ApiErrors do middleware e envia para o errorHandler global
        next(err);
      }
    };

    return descriptor;
  };
}
