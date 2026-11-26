// Which integrations are real and which are simulated, checked when the page loads.
import { integrationStatus } from "../../lib/status";

export const dynamic = "force-dynamic";

export default async function Status() {
  const integrations = await integrationStatus();
  return (
    <main className="mx-auto max-w-2xl space-y-4 p-4">
      <h1 className="text-3xl text-brand">SkillLens integrations</h1>
      <p>Real = a working API or model call. Simulated = a stand-in for the demo. Checked live when this page loads.</p>
      <ul className="space-y-3">
        {integrations.map((i) => (
          <li key={i.name} className={i.mode === "real" ? "card-brand" : "card-alert"}>
            <p className="font-bold">
              {i.name}: {i.mode === "real" ? "Real" : "Simulated"}
              {i.healthy === false && " (unreachable now)"}
              {i.healthy === true && " (reachable)"}
            </p>
            <p className="text-sm">{i.detail}</p>
          </li>
        ))}
      </ul>
    </main>
  );
}
