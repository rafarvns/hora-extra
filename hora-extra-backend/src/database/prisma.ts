import { PrismaClient } from '@prisma/client';

/**
 * Singleton instance of PrismaClient.
 * This ensures that only one database connection is open at a time.
 */
const prisma = new PrismaClient({
  log: process.env.NODE_ENV === 'development' ? ['query', 'error', 'warn'] : ['error'],
});

export default prisma;
