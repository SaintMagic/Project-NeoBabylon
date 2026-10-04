import { projectDisplayText } from "./transcript.mjs";

export const MAX_ACTIVITY_ENTRIES = 64;
export const MAX_ACTIVITY_TEXT_CHARACTERS = 250_000;
const MAX_ACTIVITY_TITLE_CHARACTERS = 1_200;
const MAX_ACTIVITY_DETAIL_CHARACTERS = 40_000;

export function boundActivityHistory(current) {
  if (!Array.isArray(current)) return { entries: [], truncated: false };

  let truncated = false;
  const entries = current.map((activity) => {
    const title = typeof activity.title === "string" ? activity.title : "Tool activity";
    const titleProjection = projectDisplayText(title, MAX_ACTIVITY_TITLE_CHARACTERS);
    const detail = typeof activity.detail === "string" ? activity.detail : undefined;
    const detailProjection = detail === undefined
      ? projectDisplayText("", MAX_ACTIVITY_DETAIL_CHARACTERS, activity)
      : projectDisplayText(detail, MAX_ACTIVITY_DETAIL_CHARACTERS, activity);
    const titleOmitted = Math.max(0, title.length - titleProjection.text.length);
    const detailOmitted = detailProjection.omittedCharacters ?? 0;
    const entryTruncated = titleOmitted > 0 || detailProjection.displayTruncated === true;
    if (entryTruncated) truncated = true;

    return {
      ...activity,
      title: titleProjection.text,
      ...(detail === undefined ? {} : { detail: detailProjection.text }),
      ...(entryTruncated ? {
        displayTruncated: true,
        omittedCharacters: titleOmitted + detailOmitted,
        sourceRetained: activity.sourceRetained === true,
        upstreamTruncated: activity.upstreamTruncated === true,
      } : {}),
    };
  });

  let totalCharacters = entries.reduce(
    (total, activity) => total + activity.title.length + (activity.detail?.length ?? 0),
    0,
  );
  while (entries.length > MAX_ACTIVITY_ENTRIES
    || (totalCharacters > MAX_ACTIVITY_TEXT_CHARACTERS && entries.length > 1)) {
    const removed = entries.shift();
    totalCharacters -= removed.title.length + (removed.detail?.length ?? 0);
    truncated = true;
  }

  return { entries, truncated };
}
