/**
 * Helper para padronizar respostas de sucesso da API.
 */
export class ApiResponse {
  public static success<T>(data: T, message: string = 'Sucesso', statusCode: number = 200) {
    return {
      success: true,
      message,
      data,
      statusCode
    };
  }

  public static created<T>(data: T, message: string = 'Criado com sucesso') {
    return this.success(data, message, 201);
  }
}
