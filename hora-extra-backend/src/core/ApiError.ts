/**
 * Classe customizada para erros da API REST.
 * Permite capturar falhas específicas com o código de status HTTP correspondente.
 */
export class ApiError extends Error {
  public readonly statusCode: number;

  constructor(message: string, statusCode: number = 400) {
    super(message);
    this.name = 'ApiError';
    this.statusCode = statusCode;
    Object.setPrototypeOf(this, ApiError.prototype);
  }

  public static badRequest(msg: string) {
    return new ApiError(msg, 400);
  }

  public static unauthorized(msg: string = 'Não autorizado') {
    return new ApiError(msg, 401);
  }

  public static forbidden(msg: string = 'Acesso negado') {
    return new ApiError(msg, 403);
  }

  public static notFound(msg: string = 'Recurso não encontrado') {
    return new ApiError(msg, 404);
  }

  public static internal(msg: string = 'Erro interno no servidor') {
    return new ApiError(msg, 500);
  }
}
