import { avatarHue, formatDistance, formatElapsedMinutes, initials, minutesSince } from '../format';
import { nl } from '../../i18n/nl';

describe('format (nl)', () => {
  it('formats approximate distances the Dutch way', () => {
    expect(formatDistance(null)).toBe('dichtbij');
    expect(formatDistance(20)).toBe('< 50 m');
    expect(formatDistance(423)).toBe('420 m');
    expect(formatDistance(1000)).toBe('1 km');
    expect(formatDistance(1100)).toBe('1,1 km');
  });

  it('formats drive duration', () => {
    const now = Date.parse('2026-10-01T12:00:00Z');
    expect(minutesSince('2026-10-01T11:48:00Z', now)).toBe(12);
    expect(formatElapsedMinutes(12)).toBe('12 min');
    expect(formatElapsedMinutes(120)).toBe('2 uur');
    expect(formatElapsedMinutes(135)).toBe('2 uur 15 min');
    expect(nl.drive.bodyActive(0)).toBe('Net vertrokken');
    expect(nl.drive.bodyActive(12)).toBe('12 min onderweg');
  });

  it('pluralizes rijder/rijders', () => {
    expect(nl.nearby.count(1)).toBe('1 rijder in de buurt');
    expect(nl.nearby.count(3)).toBe('3 rijders in de buurt');
    expect(nl.voice.listening(1)).toBe('1 rijder luistert mee');
    expect(nl.voice.listening(2)).toBe('2 rijders luisteren mee');
  });

  it('derives initials and stable avatar colours', () => {
    expect(initials('alex de vries')).toBe('AV');
    expect(initials('Sam')).toBe('S');
    expect(avatarHue('d123')).toBe(avatarHue('d123'));
  });
});
