import ApiClient from "~/lib/axiosClient";
import type {
  LinkHealthReport,
  LinkHealthRequest,
} from "../models/courseQuality";

// Ziqi iteration 5: link health for URLs from any platform (Yoma, YouthHub, 1MiO, …).

/** 200 links with per-host pacing can take a few minutes. */
export const LINK_HEALTH_TIMEOUT_MS = 600_000; // 10 minutes

/** POST /courseQuality/links/check (Admin): at most 200 URLs per call. */
export const checkLinkHealth = async (
  request: LinkHealthRequest,
): Promise<LinkHealthReport> => {
  const instance = await ApiClient;
  const { data } = await instance.post<LinkHealthReport>(
    "/courseQuality/links/check",
    request,
    { timeout: LINK_HEALTH_TIMEOUT_MS },
  );
  return data;
};
