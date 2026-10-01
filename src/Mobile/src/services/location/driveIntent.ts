import { kv } from '../storage/kv';

const KEY = 'opdeweg.driveIntent.v1';
let cached: boolean | null = null;

/**
 * Whether the user wants to be driving. Persisted so the headless background task — which may
 * run in a fresh JS context without any UI state — knows whether to report or shut itself down.
 */
export const driveIntent = {
  async get(): Promise<boolean> {
    cached ??= (await kv.get<boolean>(KEY)) ?? false;
    return cached;
  },
  async set(value: boolean): Promise<void> {
    cached = value;
    await kv.set(KEY, value);
  },
};
