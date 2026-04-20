import i18n from '../i18n/i18n';

const LANG_TO_LOCALE: Record<string, string> = {
  mk: 'mk-MK',
  sr: 'sr-RS',
  sq: 'sq-AL',
  en: 'en-GB',
};

function activeLocale(): string {
  const lang = (i18n.language || 'mk').split('-')[0];
  return LANG_TO_LOCALE[lang] ?? LANG_TO_LOCALE.mk;
}

export function formatNumber(
  value: number | null | undefined,
  options: Intl.NumberFormatOptions = {}
): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '';
  return new Intl.NumberFormat(activeLocale(), options).format(value);
}

export function formatQuantity(value: number | null | undefined, decimals = 2): string {
  return formatNumber(value, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
}

export function formatInteger(value: number | null | undefined): string {
  return formatNumber(value, { maximumFractionDigits: 0 });
}

export function formatCurrency(
  value: number | null | undefined,
  currency: string | null | undefined
): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '';
  const code = (currency ?? '').trim().toUpperCase();
  if (!/^[A-Z]{3}$/.test(code)) {
    return formatQuantity(value, 2);
  }
  try {
    return new Intl.NumberFormat(activeLocale(), {
      style: 'currency',
      currency: code,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  } catch {
    return `${formatQuantity(value, 2)} ${code}`;
  }
}

export function formatPercent(value: number | null | undefined, decimals = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '';
  return `${formatNumber(value, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })}%`;
}

function toDate(input: Date | string | number | null | undefined): Date | null {
  if (input === null || input === undefined || input === '') return null;
  const d = input instanceof Date ? input : new Date(input);
  return Number.isNaN(d.getTime()) ? null : d;
}

export function formatDate(input: Date | string | number | null | undefined): string {
  const d = toDate(input);
  if (!d) return '';
  return new Intl.DateTimeFormat(activeLocale(), {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(d);
}

export function formatDateTime(input: Date | string | number | null | undefined): string {
  const d = toDate(input);
  if (!d) return '';
  return new Intl.DateTimeFormat(activeLocale(), {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

export function formatTime(input: Date | string | number | null | undefined): string {
  const d = toDate(input);
  if (!d) return '';
  return new Intl.DateTimeFormat(activeLocale(), {
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

export function formatRelativeDate(input: Date | string | number | null | undefined): string {
  const d = toDate(input);
  if (!d) return '';
  const diffMs = d.getTime() - Date.now();
  const absMs = Math.abs(diffMs);
  const sec = Math.round(diffMs / 1000);
  const min = Math.round(diffMs / 60_000);
  const hr = Math.round(diffMs / 3_600_000);
  const day = Math.round(diffMs / 86_400_000);
  const rtf = new Intl.RelativeTimeFormat(activeLocale(), { numeric: 'auto' });
  if (absMs < 60_000) return rtf.format(sec, 'second');
  if (absMs < 3_600_000) return rtf.format(min, 'minute');
  if (absMs < 86_400_000) return rtf.format(hr, 'hour');
  if (absMs < 2_592_000_000) return rtf.format(day, 'day');
  return formatDate(d);
}
