// One base date for the listings and the stand-in pages, so their dates agree.
export function dayOffset(baseDate, days) {
  const date = new Date(`${baseDate}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date;
}

export function isoDay(date) {
  return date.toISOString().slice(0, 10);
}

export function longDay(date) {
  return date.toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
}
