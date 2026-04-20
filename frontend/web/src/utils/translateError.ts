import i18n from '../i18n/i18n';

type MaybeApiError =
  | {
      errorCode?: string | null;
      errorMessage?: string | null;
      message?: string | null;
      errors?: string[] | null;
    }
  | string
  | Error
  | unknown
  | null
  | undefined;

/**
 * Convert a backend failure payload or thrown error into a localised string.
 * Preference order:
 * 1. `errors.<errorCode>` translation (stable key contract with backend).
 * 2. `errorMessage` / `message` coming from the server.
 * 3. First item of `errors[]`.
 * 4. `errors.generic` fallback.
 */
export function translateError(err: MaybeApiError, fallbackKey = 'errors.generic'): string {
  if (err === null || err === undefined) return i18n.t(fallbackKey);

  if (typeof err === 'string') return err;

  const payload = err as { errorCode?: string; errorMessage?: string; message?: string; errors?: string[] };

  if (payload.errorCode) {
    const key = `errors.${payload.errorCode}`;
    const translated = i18n.t(key);
    if (translated && translated !== key) return translated;
  }

  if (payload.errorMessage) return payload.errorMessage;
  if (payload.message) return payload.message;
  if (payload.errors && payload.errors.length > 0) return payload.errors[0];

  return i18n.t(fallbackKey);
}

/** Parse a `fetch` Response.body (failed) and surface a translated message. */
export async function translateFetchError(response: Response): Promise<string> {
  let payload: MaybeApiError = null;
  try {
    payload = (await response.clone().json()) as MaybeApiError;
  } catch {
    try {
      const text = await response.clone().text();
      if (text) payload = text;
    } catch {
      payload = null;
    }
  }
  if (payload) return translateError(payload);
  if (response.status === 401) return i18n.t('errors.unauthorized');
  if (response.status === 404) return i18n.t('errors.notFound');
  return i18n.t('errors.generic');
}
