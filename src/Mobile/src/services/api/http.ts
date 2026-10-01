import { appConfig } from '../config';
import { tokenManager } from '../auth/tokenManager';
import type { ProblemDetails } from '../../types/api';

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isNetworkError(): boolean {
    return this.status === 0;
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'DELETE';
  body?: unknown;
  auth?: boolean;
  signal?: AbortSignal;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, auth = true } = options;

  const send = async (token: string | null) => {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), appConfig.requestTimeoutMs);
    options.signal?.addEventListener('abort', () => controller.abort());
    try {
      return await fetch(`${appConfig.apiUrl}${path}`, {
        method,
        headers: {
          Accept: 'application/json',
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
        signal: controller.signal,
      });
    } catch {
      throw new ApiError('No connection to Opdeweg.', 0, 'network_error');
    } finally {
      clearTimeout(timeout);
    }
  };

  let token = auth ? await tokenManager.getAccessToken() : null;
  if (auth && !token) {
    throw new ApiError('Please sign in again.', 401, 'unauthenticated');
  }

  let response = await send(token);
  if (auth && response.status === 401) {
    token = await tokenManager.getAccessToken(true);
    if (!token) {
      throw new ApiError('Please sign in again.', 401, 'unauthenticated');
    }

    response = await send(token);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const json: unknown = text ? safeParse(text) : undefined;

  if (!response.ok) {
    const problem = (json ?? {}) as ProblemDetails;
    throw new ApiError(problem.title ?? `Request failed (${response.status}).`, response.status, problem.code ?? `http_${response.status}`);
  }

  return json as T;
}

function safeParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}
