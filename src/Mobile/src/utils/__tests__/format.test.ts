import { avatarHue, formatDistance, formatElapsed, initials, pluralize } from '../format';

describe('format', () => {
  it('formats approximate distances', () => {
    expect(formatDistance(null)).toBe('nearby');
    expect(formatDistance(20)).toBe('< 50 m');
    expect(formatDistance(423)).toBe('420 m');
    expect(formatDistance(1100)).toBe('1.1 km');
  });

  it('formats elapsed drive time', () => {
    const now = Date.parse('2026-10-01T12:00:00Z');
    expect(formatElapsed('2026-10-01T11:59:30Z', now)).toBe('just now');
    expect(formatElapsed('2026-10-01T11:48:00Z', now)).toBe('12 min');
    expect(formatElapsed('2026-10-01T10:00:00Z', now)).toBe('2 h');
  });

  it('derives initials and stable avatar colours', () => {
    expect(initials('alex de vries')).toBe('AV');
    expect(initials('Sam')).toBe('S');
    expect(avatarHue('d123')).toBe(avatarHue('d123'));
  });

  it('pluralizes', () => {
    expect(pluralize(1, 'driver')).toBe('1 driver');
    expect(pluralize(4, 'driver')).toBe('4 drivers');
  });
});
