/** Exponential backoff with full jitter, capped. */
export function backoffDelay(attempt: number, baseMs = 1000, maxMs = 30_000): number {
  const exp = Math.min(maxMs, baseMs * 2 ** Math.max(0, attempt));
  return Math.round(exp / 2 + Math.random() * (exp / 2));
}
