import winston from 'winston';
import 'winston-daily-rotate-file';

const { combine, timestamp, printf, colorize, align } = winston.format;

/**
 * Filtro customizado para incluir o nível do log em letras maiúsculas e com padding.
 */
const customFormat = printf(({ level, message, timestamp, module, ...metadata }) => {
  const moduleTag = module ? `[${module}] ` : '';
  let metaString = '';
  if (Object.keys(metadata).length > 0) {
    metaString = ` ${JSON.stringify(metadata)}`;
  }
  return `${timestamp} ${level}: ${moduleTag}${message}${metaString}`;
});

/**
 * Configuração do Logger utilizando Winston.
 * Suporta múltiplos transportes (Console e Arquivo rotativo).
 */
const logger = winston.createLogger({
  level: process.env.LOG_LEVEL || 'info',
  format: combine(
    timestamp({ format: 'YYYY-MM-DD HH:mm:ss' }),
    customFormat
  ),
  transports: [
    // 1. Console Transport (Colorizado para desenvolvimento)
    new winston.transports.Console({
      format: combine(
        colorize({ all: true }),
        customFormat
      ),
    }),
    // 2. File Transport (Rotativo para persistência)
    new winston.transports.DailyRotateFile({
      filename: 'logs/application-%DATE%.log',
      datePattern: 'YYYY-MM-DD',
      zippedArchive: true,
      maxSize: '20m',
      maxFiles: '14d',
      format: combine(
        timestamp({ format: 'YYYY-MM-DD HH:mm:ss' }),
        customFormat
      ),
    }),
    // 3. Error File Transport (Apenas erros em um arquivo separado)
    new winston.transports.DailyRotateFile({
      level: 'error',
      filename: 'logs/error-%DATE%.log',
      datePattern: 'YYYY-MM-DD',
      zippedArchive: true,
      maxSize: '10m',
      maxFiles: '30d',
    }),
  ],
});

export default logger;
