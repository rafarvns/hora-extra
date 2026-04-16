import { execSync } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * Orchestrates database setup for local development.
 * Supports switching between MySQL and SQLite based on USE_SQLITE environment variable.
 */
async function setupDatabase() {
  const useSqlite = process.env.USE_SQLITE === 'true';
  const schemaPath = path.join(__dirname, '../prisma/schema.prisma');
  const backupSchemaPath = path.join(__dirname, '../prisma/schema.prisma.bak');
  
  console.log(`[DB-SETUP] Mode: ${useSqlite ? 'SQLite' : 'MySQL'}`);

  try {
    const originalSchema = fs.readFileSync(schemaPath, 'utf8');

    if (useSqlite) {
      console.log('[DB-SETUP] Configuring for SQLite...');
      
      // Validation: Check if it's already sqlite to avoid redundant generation
      if (originalSchema.includes('provider = "sqlite"')) {
        console.log('[DB-SETUP] Schema already configured for SQLite.');
      } else {
        // Backup
        fs.writeFileSync(backupSchemaPath, originalSchema);
        
        // Transform
        let newSchema = originalSchema.replace(
          /provider\s*=\s*"mysql"/,
          'provider = "sqlite"'
        );
        
        // SQLite doesn't support some MySQL specific things, 
        // but this schema is simple enough. 
        // If there were @db.VarChar(255), we'd need to strip them.
        
        fs.writeFileSync(schemaPath, newSchema);
        console.log('[DB-SETUP] schema.prisma updated to SQLite.');
      }

      // Ensure DATABASE_URL is set for SQLite if not provided for it
      if (!process.env.DATABASE_URL || !process.env.DATABASE_URL.startsWith('file:')) {
        process.env.DATABASE_URL = 'file:./dev.db';
        console.log('[DB-SETUP] Overriding DATABASE_URL to file:./dev.db');
      }

      console.log('[DB-SETUP] Generating Prisma Client...');
      execSync('npx prisma generate', { stdio: 'inherit', env: process.env });

      console.log('[DB-SETUP] Syncing database structure (db push)...');
      execSync('npx prisma db push', { stdio: 'inherit', env: process.env });
      
    } else {
      console.log('[DB-SETUP] Ensuring MySQL configuration...');
      
      if (originalSchema.includes('provider = "sqlite"')) {
        if (fs.existsSync(backupSchemaPath)) {
          console.log('[DB-SETUP] Restoring MySQL schema from backup.');
          fs.copyFileSync(backupSchemaPath, schemaPath);
        } else {
          console.log('[DB-SETUP] Warning: No backup found, trying to manual revert.');
          let restoredSchema = originalSchema.replace(
            /provider\s*=\s*"sqlite"/,
            'provider = "mysql"'
          );
          fs.writeFileSync(schemaPath, restoredSchema);
        }
      }

      console.log('[DB-SETUP] Generating Prisma Client (MySQL)...');
      execSync('npx prisma generate', { stdio: 'inherit', env: process.env });
      
      // We don't run migrations automatically for MySQL to avoid accidental data loss 
      // or connection errors if the user hasn't started Docker yet.
      console.log('[DB-SETUP] MySQL ready. Run "npm run db:migrate" if needed.');
    }

  } catch (error) {
    console.error('[DB-SETUP] Error during database setup:', error);
    process.exit(1);
  }
}

setupDatabase();
