// The back end applies opening hours in Africa/Johannesburg (no daylight saving, always UTC+2)
export const SA_TIME_ZONE = 'Africa/Johannesburg';
export const SA_OFFSET = '+02:00';
export const SA_OFFSET_PIPE = '+0200';

export function todayInSouthAfrica(): string {
  return new Date().toLocaleDateString('en-CA', { timeZone: SA_TIME_ZONE });
}
