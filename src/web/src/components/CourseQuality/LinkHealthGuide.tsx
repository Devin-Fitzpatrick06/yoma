import {
  LINK_HEALTH_EXPLANATIONS,
  LINK_HEALTH_LABELS,
  LINK_HEALTH_STATUSES,
} from "./LinkHealth.helpers";
import { LinkHealthChip } from "./LinkHealthResults";

// What each link health status means, and where the standalone checker for other platforms is.

const LinkHealthGuide: React.FC = () => (
  <section
    aria-labelledby="link-health-guide-heading"
    className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm md:p-5"
  >
    <h2
      id="link-health-guide-heading"
      className="font-nunito text-lg font-black text-black"
    >
      What the results mean
    </h2>
    <dl className="grid gap-2 sm:grid-cols-2">
      {LINK_HEALTH_STATUSES.map((status) => (
        <div key={status} className="flex items-start gap-2">
          <dt className="w-32 shrink-0">
            <LinkHealthChip status={status} />
            <span className="sr-only">: </span>
          </dt>
          <dd className="text-gray-dark min-w-0 text-sm break-words">
            {LINK_HEALTH_EXPLANATIONS[status]}
          </dd>
        </div>
      ))}
    </dl>
    <p className="text-gray-dark text-sm">
      {LINK_HEALTH_LABELS.Blocked} and {LINK_HEALTH_LABELS.Refused} are not
      counted as dead links. Every link is fetched by the Yoma API with timeouts
      and pacing per site; nothing is fetched from your browser.
    </p>
    <p className="text-gray-dark text-sm">
      Checking another platform without Yoma? The standalone command-line
      checker reads a .txt, .csv or sitemap and writes a CSV or Markdown report:{" "}
      <code className="bg-gray-light rounded px-1 text-xs break-all">
        node scripts/link-health/check-links.mjs
      </code>{" "}
      (see the script&apos;s help for its options).
    </p>
  </section>
);

export default LinkHealthGuide;
