import { destination, distanceMeters, headingDifference } from '../geo';

describe('geo', () => {
  const origin = { latitude: 52.3791, longitude: 4.9003 };

  it.each([50, 999, 1000, 1101])('round-trips %d m', (meters) => {
    expect(distanceMeters(origin, destination(origin, 37, meters))).toBeCloseTo(meters, 6);
  });

  it('wraps headings', () => {
    expect(headingDifference(350, 10)).toBe(20);
    expect(headingDifference(0, 180)).toBe(180);
  });
});
