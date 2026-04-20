import { Request, Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';
import { ApiError } from '../../core/ApiError.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

const authService = ServiceFactory.getAuthService();
const prisma = ServiceFactory.getPrismaClient();

/**
 * Controller responsável pela autenticação dos jogadores.
 */
export class AuthController extends BaseController {
  /**
   * Registra um novo jogador.
   * POST /auth/register
   */
  public register = async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    try {
      const { nome, email, senha } = req.body;

      if (!nome || !email || !senha) {
        throw ApiError.badRequest('NOME, EMAIL e SENHA são obrigatórios.');
      }

      // Verifica se o jogador já existe
      const playerExists = await prisma.player.findUnique({
        where: { email },
      });

      if (playerExists) {
        logger.warn(`Tentativa de registro com e-mail já existente: ${email}`, { module: 'AUTH' });
        throw ApiError.badRequest('Este e-mail já está cadastrado.');
      }

      // Hash da senha
      const hashedSenha = await authService.hashPassword(senha);

      // Cria o jogador no banco
      const player = await prisma.player.create({
        data: {
          nome,
          email,
          senha: hashedSenha,
        },
      });

      logger.info(`Novo jogador registrado: ${player.email} (ID: ${player.id})`, { 
        module: 'AUTH', 
        playerId: player.id 
      });

      // Gera o token
      const token = authService.generateToken(player.id);

      this.sendCreated(res, {
        jogador: {
          id: player.id,
          nome: player.nome,
          email: player.email,
        },
        token,
      }, 'Jogador cadastrado com sucesso!');
    } catch (error) {
      next(error);
    }
  }

  /**
   * Realiza o login de um jogador.
   * POST /auth/login
   */
  public login = async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    try {
      const { email, senha } = req.body;

      if (!email || !senha) {
        throw ApiError.badRequest('EMAIL e SENHA são obrigatórios.');
      }

      // Busca o jogador pelo e-mail
      const player = await prisma.player.findUnique({
        where: { email },
      });

      if (!player) {
         logger.warn(`Tentativa de login com e-mail inexistente: ${email}`, { module: 'AUTH' });
         throw ApiError.unauthorized('E-mail ou senha inválidos.');
      }

      // Compara a senha
      const isPasswordValid = await authService.comparePasswords(senha, player.senha);

      if (!isPasswordValid) {
         logger.warn(`Senha incorreta para o jogador: ${email}`, { module: 'AUTH' });
         throw ApiError.unauthorized('E-mail ou senha inválidos.');
      }

      logger.info(`Login realizado com sucesso: ${player.email} (ID: ${player.id})`, { 
        module: 'AUTH', 
        playerId: player.id 
      });

      // Gera o token
      const token = authService.generateToken(player.id);

      this.sendSuccess(res, {
        jogador: {
          id: player.id,
          nome: player.nome,
          email: player.email,
          nivel: player.nivel,
          xp: player.xp
        },
        token,
      }, 'Login realizado com sucesso!');
    } catch (error) {
      next(error);
    }
  }
}

export default new AuthController();
