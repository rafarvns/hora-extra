import bcrypt from 'bcryptjs';
import jwt from 'jsonwebtoken';

const JWT_SECRET = process.env.JWT_SECRET || 'fallback_secret_for_dev_only';
const JWT_EXPIRES_IN = process.env.JWT_EXPIRES_IN || '1d';

class AuthService {
  /**
   * Criptografa uma senha usando bcrypt.
   */
  async hashPassword(senha: string): Promise<string> {
    const salt = await bcrypt.genSalt(10);
    return await bcrypt.hash(senha, salt);
  }

  /**
   * Compara uma senha em texto plano com um hash.
   */
  async comparePasswords(senha: string, hash: string): Promise<boolean> {
    return await bcrypt.compare(senha, hash);
  }

  /**
   * Gera um token JWT para um usuário.
   */
  generateToken(userId: string): string {
    return jwt.sign({ id: userId }, JWT_SECRET, {
      expiresIn: JWT_EXPIRES_IN as any,
    });
  }

  /**
   * Verifica a validade de um token JWT.
   */
  verifyToken(token: string): any {
    try {
      return jwt.verify(token, JWT_SECRET);
    } catch (error) {
      return null;
    }
  }
}

export default new AuthService();
