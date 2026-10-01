/* eslint-disable no-console */
type Level = 'debug' | 'info' | 'warn' | 'error';

/** Tiny structured logger. Never pass coordinates or tokens to it. */
function log(level: Level, event: string, data?: Record<string, unknown>) {
  if (!__DEV__ && level === 'debug') {
    return;
  }

  const line = data ? `${event} ${JSON.stringify(data)}` : event;
  (level === 'error' ? console.error : level === 'warn' ? console.warn : console.log)(`[opdeweg] ${line}`);
}

export const logger = {
  debug: (event: string, data?: Record<string, unknown>) => log('debug', event, data),
  info: (event: string, data?: Record<string, unknown>) => log('info', event, data),
  warn: (event: string, data?: Record<string, unknown>) => log('warn', event, data),
  error: (event: string, data?: Record<string, unknown>) => log('error', event, data),
};
