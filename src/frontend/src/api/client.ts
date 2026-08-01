const getRuntimeApiBaseUrl = (): string => {
  const w = window as unknown as { __VITE_API_URL__?: string };
  return typeof w.__VITE_API_URL__ === 'string' ? w.__VITE_API_URL__ : '';
};

const runtimeApiBaseUrl = getRuntimeApiBaseUrl();

// During local `vite dev`, the placeholder token won't be replaced.
// In that case we want to fall back to relative `/api/*` requests.
const normalizedApiBaseUrl = runtimeApiBaseUrl === '__VITE_API_URL__' ? '' : runtimeApiBaseUrl;

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  // Node's fetch (used by Vitest/undici) rejects relative URLs.
  // In a browser environment this still resolves correctly.
  const origin = window.location?.origin ?? '';
  const url = normalizedApiBaseUrl ? `${normalizedApiBaseUrl}${path}` : `${origin}${path}`;
  const res = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {})
    }
  });

  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(text || `Request failed (${res.status})`);
  }

  return (await res.json()) as T;
}

export const api = {
  async convert(input: { fromCurrency: string; toCurrency: string; amount: number }): Promise<any> {
    return request<any>('/api/conversions', {
      method: 'POST',
      body: JSON.stringify(input)
    });
  },
  async listConversions(input: { limit: number }): Promise<any[]> {
    const url = `/api/conversions?limit=${encodeURIComponent(String(input.limit))}`;
    return request<any[]>(url);
  }
};
