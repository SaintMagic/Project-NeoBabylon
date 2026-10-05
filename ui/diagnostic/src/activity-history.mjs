import { projectDisplayText } from "./transcript.mjs";

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

  return { entries, truncated };
}
