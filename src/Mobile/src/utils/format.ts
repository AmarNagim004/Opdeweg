/** Geschatte, privacyvriendelijke afstand in Nederlandse notatie: "< 50 m", "420 m", "1 km", "1,1 km". */
export function formatDistance(meters: number | null | undefined): string {
  if (meters == null) {
    return 'dichtbij';
  }

  if (meters < 50) {
    return '< 50 m';
  }

  if (meters < 1000) {
    return `${Math.round(meters / 10) * 10} m`;
  }

  return `${(meters / 1000).toFixed(1).replace(/\.0$/, '').replace('.', ',')} km`;
}

/** Whole minutes elapsed since an ISO timestamp (never negative). */
export function minutesSince(fromIso: string | null | undefined, now = Date.now()): number {
  return fromIso ? Math.max(0, Math.floor((now - Date.parse(fromIso)) / 60_000)) : 0;
}

/** Ritduur: "12 min", "2 uur", "2 uur 15 min". */
export function formatElapsedMinutes(minutes: number): string {
  if (minutes < 60) {
    return `${minutes} min`;
  }

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours} uur` : `${hours} uur ${rest} min`;
}

export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return '?';
  }

  const first = parts[0]?.[0] ?? '';
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
  return (first + last).toUpperCase();
}

const AVATAR_HUES = [12, 38, 160, 190, 215, 260, 300, 340];

/** Stable, anonymous avatar colour derived from a pseudonymous id. */
export function avatarHue(id: string): number {
  let hash = 0;
  for (let i = 0; i < id.length; i++) {
    hash = (hash * 31 + id.charCodeAt(i)) | 0;
  }

  return AVATAR_HUES[Math.abs(hash) % AVATAR_HUES.length] ?? 200;
}
