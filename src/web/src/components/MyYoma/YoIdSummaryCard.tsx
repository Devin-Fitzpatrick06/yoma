import Link from "next/link";
import type { PathwayProfile, YoIdSummary } from "~/api/models/pathway";
import { BACKGROUND_LABELS } from "~/lib/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// What the youth has done so far, from YoID: the same facts as the YoID dashboard's Passport,
// Skills and Wallet cards, condensed onto one purple card at the top of My Yoma.
// ─────────────────────────────────────────────────────────────────────────────

const MAX_SKILLS = 8;

const Chip: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <li className="max-w-full truncate rounded-full bg-white/15 px-2.5 py-0.5 text-xs">
    {children}
  </li>
);

const Stat: React.FC<{ value: React.ReactNode; label: string }> = ({
  value,
  label,
}) => (
  // the label comes first for screen readers; the number reads first on screen
  <div className="flex min-w-0 flex-col-reverse justify-end rounded-lg bg-white/10 px-2.5 py-2">
    <dt className="text-xs leading-tight opacity-85">{label}</dt>
    <dd className="font-nunito text-lg font-black tabular-nums md:text-xl">
      {value}
    </dd>
  </div>
);

const YoIdSummaryCard: React.FC<{
  yoId: YoIdSummary;
  profile: PathwayProfile;
}> = ({ yoId, profile }) => {
  const skills = yoId.skills.slice(0, MAX_SKILLS);
  const moreSkills = yoId.skills.length - skills.length;

  return (
    <section
      aria-labelledby="yoid-summary-name"
      className="bg-purple grid grid-cols-1 gap-5 rounded-xl p-5 text-white shadow md:grid-cols-[1fr_1.6fr]"
    >
      <div className="flex min-w-0 flex-col gap-2">
        <p className="text-[11px] tracking-widest uppercase opacity-80">
          Your YoID
        </p>
        <h2
          id="yoid-summary-name"
          className="font-nunito truncate text-xl font-black"
        >
          {yoId.displayName}
        </h2>
        <ul className="flex flex-wrap gap-1.5" aria-label="Your answers">
          <Chip>{BACKGROUND_LABELS[profile.background]}</Chip>
          <Chip>{profile.countryName ?? "Anywhere"}</Chip>
          <Chip>{profile.hoursPerWeek} h a week</Chip>
        </ul>
        <Link
          href="/yoid"
          className="mt-auto self-start text-xs font-semibold underline underline-offset-2 hover:opacity-80 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
        >
          Open your YoID
        </Link>
      </div>

      <div className="flex min-w-0 flex-col gap-2">
        <p className="text-[11px] tracking-widest uppercase opacity-80">
          What you have done so far
        </p>
        {skills.length > 0 ? (
          <ul className="flex flex-wrap gap-1.5" aria-label="Your skills">
            {skills.map((skill) => (
              <Chip key={skill}>{skill}</Chip>
            ))}
            {moreSkills > 0 && <Chip>+{moreSkills} more</Chip>}
          </ul>
        ) : (
          <p className="text-sm opacity-85">
            Skills from courses you complete will appear here.
          </p>
        )}
        <dl className="mt-1 grid grid-cols-3 gap-2">
          <Stat
            value={yoId.completedOpportunities.toLocaleString("en")}
            label={
              yoId.completedOpportunities === 1
                ? "Opportunity completed"
                : "Opportunities completed"
            }
          />
          <Stat
            value={yoId.credentials.toLocaleString("en")}
            label={
              yoId.credentials === 1
                ? "Credential on YoID"
                : "Credentials on YoID"
            }
          />
          <Stat
            value={
              yoId.zltoBalance === null
                ? "–"
                : yoId.zltoBalance.toLocaleString("en")
            }
            label="ZLTO earned"
          />
        </dl>
      </div>
    </section>
  );
};

export default YoIdSummaryCard;
