// Round, soft-cornered icons in the style of P2E's circle icons (info, arrow-in-circle). Inline SVG: no icon font,
// no extra download. Decorative: buttons keep their text label, so icons are aria-hidden.
const PATHS = {
  arrow: <><circle cx="12" cy="12" r="10" /><path d="M8 12h8M13 8.5 16.5 12 13 15.5" /></>,
  check: <><circle cx="12" cy="12" r="10" /><path d="m7.5 12.5 3 3 6-6.5" /></>,
  info: <><circle cx="12" cy="12" r="10" /><path d="M12 11v5.5M12 7.5v.01" /></>,
  alert: <><circle cx="12" cy="12" r="10" /><path d="M12 7v6M12 16.5v.01" /></>,
  star: <><circle cx="12" cy="12" r="10" /><path d="m12 6.8 1.6 3.3 3.6.5-2.6 2.5.6 3.6-3.2-1.7-3.2 1.7.6-3.6-2.6-2.5 3.6-.5z" /></>,
  mic: <><rect x="9" y="3" width="6" height="11" rx="3" /><path d="M5.5 11a6.5 6.5 0 0 0 13 0M12 17.5V21" /></>,
  camera: <><rect x="3" y="6.5" width="18" height="13" rx="3.5" /><circle cx="12" cy="13" r="3.5" /><path d="M8.5 6.5 10 4h4l1.5 2.5" /></>,
  pencil: <><path d="M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16z" /><path d="m13.5 6.5 4 4" /></>,
  retry: <><path d="M20 12a8 8 0 1 1-2.3-5.6" /><path d="M20 4v4.5h-4.5" /></>,
  external: <><path d="M14 4h6v6M20 4l-8.5 8.5" /><path d="M18 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4" /></>,
  globe: <><circle cx="12" cy="12" r="10" /><path d="M2 12h20M12 2c3 3.5 3 16.5 0 20M12 2c-3 3.5-3 16.5 0 20" /></>,
  cap: <><path d="m2 9 10-5 10 5-10 5z" /><path d="M6 11v5c3 2.5 9 2.5 12 0v-5M22 9v5" /></>,
} as const;

export type IconName = keyof typeof PATHS;

export function Icon({ name, className = "size-6" }: { name: IconName; className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" className={`shrink-0 ${className}`}>
      {PATHS[name]}
    </svg>
  );
}
