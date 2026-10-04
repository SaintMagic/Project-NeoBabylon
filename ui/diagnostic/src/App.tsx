import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type Dispatch, type ReactNode, type SetStateAction } from "react";
import { Button } from "@astryxdesign/core/Button";
import { IconButton } from "@astryxdesign/core/IconButton";
import { listenToHost, requestHost, type HostNotification, type HostOperation } from "./bridge";
import { appendAssistantDelta, boundTranscript, finishAssistantStreams, markAssistantTurnFailed, projectDisplayText, upsertAssistantMessage, type ChatMessage, type DisplayMetadata } from "./transcript.mjs";
import { boundActivityHistory, type ActivityHistoryEntry } from "./activity-history.mjs";
import { matchesLateCommandCompletion, mayStopCommand, requestCommandStop } from "./command-stop.mjs";
import { createAssistantDeltaBatcher, type AssistantDeltaBatcher } from "./stream-batcher.mjs";
import { filterSavedThreads, mergeHistoryPages, savedHistoryState, savedThreadLabel, savedThreadSearchEmptyMessage, savedThreadUpdatedAt } from "./history.mjs";
import { classifyActivityOutcome, createLiveTurnStatusMessage, resolveLiveTurnState, restoreSavedActivities, restoreTurnOutcome, restoreVisibleTranscript } from "./restoration.mjs";
import { approvalPresentation, invalidateApprovalReview } from "./approvals.mjs";
import { executionPolicyPresentation } from "./execution-policy.mjs";
import { hideFullAccessNotice, isFullAccessNoticeHidden } from "./full-access-notice.mjs";
import {
  clearPendingSubmission,
  draftKey,
  loadDraft,
  reconcilePendingSubmission,
  saveDraft,
  savePendingSubmission,
} from "./drafts.mjs";
import { classifyActiveTask, classifyActiveTaskListing, clearActiveTask, readActiveTask, saveActiveTask } from "./active-task.mjs";
import { handleDialogKeyDown } from "./modal-focus.mjs";
import { nextListboxIndex } from "./model-picker.mjs";
import { completionLimit, parseOutputOverride } from "./output-cap.mjs";
import { handleThreadContextMenuEvent, renameSavedThread, validateThreadName } from "./thread-actions.mjs";
import { canSelectCapability, confirmedResumeCapability, sameCapabilityRecord, selectCapabilityForNextTurn } from "./model-switch.mjs";
import { initialReasoningSelection, parseReasoningSelection, reasoningOptions, type ReasoningSelection } from "./reasoning-selection.mjs";
import { ReasoningSelector } from "./reasoning-selector";
import { canCompactContext, requestContextCompaction } from "./context-compaction.mjs";
import { useTaskTelemetry } from "./use-task-telemetry";
import { TaskTelemetryPanel, type CompactFeedback } from "./task-telemetry-panel";
import { bindingActionContext, candidateContract, confirmBindingTransition, confirmDisabledStageResponse, createSerialRequestQueue, currentReviewRecord, parseCandidateFilePage, parsePreparedBindingHistory, parsePreparedBindingToolIds, parseReviewHistory, reviewNoteError, stageBindingContext, verifyStageRecovery, type BindingAction, type CandidateFilePage, type CandidateReviewRecord, type DisabledStageResult, type PreparedBindingRecord, type StageBindingContext } from "./generated-tool-review.mjs";
import { GeneratedToolActivationPanel } from "./generated-tool-activation";
import { GeneratedToolReviewComparison } from "./generated-tool-review-comparison";
import { ProtectedRecordRecoveryPanel } from "./protected-record-recovery";
import { reduceTurnReview, restoreSavedTurnReview, type TurnReview } from "./turn-review.mjs";
import toolCapabilityCatalogSource from "../../../docs/release/NEOBABYLON_TOOL_CAPABILITY_CATALOG.json?raw";
import { buildCapabilityCatalogView, filterCapabilityCatalog, selectedToolQualifications } from "./tool-capability-catalog.mjs";
import type { Appearance } from "./appearance.mjs";
import "./app.css";

type JsonRecord = Record<string, unknown>;
type CandidateHostOperation = "listGeneratedToolCandidates" | "readGeneratedToolCandidateFileRange" | "listGeneratedToolReviewHistory" | "recordGeneratedToolReview" | "rejectGeneratedToolCandidate" | "listGeneratedToolPreparedBindingHistory" | "listGeneratedToolPreparedBindingToolIds" | "readGeneratedToolPreparedBindingCurrent" | "prepareGeneratedToolDisabledBinding" | "revokeGeneratedToolPreparedBinding" | "cleanupGeneratedToolPreparedBinding" | "stageGeneratedToolDisabledMcp" | "getGeneratedToolActivationStatus" | "activateGeneratedTool" | "revokeGeneratedToolActivation" | "listGeneratedToolActivationHistory" | "readGeneratedToolReviewComparison";
type CandidateHostRequest = (operation: CandidateHostOperation, payload?: JsonRecord) => Promise<JsonRecord>;
type CapabilityEntry = { sourceFile: string; capabilityRecord: JsonRecord };
const toolCapabilityCatalog = JSON.parse(toolCapabilityCatalogSource) as JsonRecord;
type OutputInspectionLocation = { threadId: string; turnId: string; itemId: string; itemType: string; title: string };
type OutputInspectionPage = { itemType: string; offset: number; totalCharacters: number; text: string; nextOffset: number; hasMore: boolean; upstreamTruncated: boolean };
type ActivityEntry = ActivityHistoryEntry;
type LateCommandNotificationBinding = { requestId: string; threadId: string; turnId: string; itemId: string };
const CommandStopActionContext = createContext<((activity: ActivityEntry) => Promise<void>) | null>(null);
type ApprovalRequest = {
  requestId: number;
  approvalInstanceId: string;
  method: string;
  params: JsonRecord;
  reviewPreview?: JsonRecord;
  reviewInvalidated?: boolean;
};
type SavedThread = { id: string; preview: string; name?: string; forkedFromId?: string; modelProvider: string; model: string; cwd?: string; updatedAt: number };
type ProjectEntry = { workspacePath: string; name: string; available: boolean };
type DraftView = { key: string | null; text: string };
type PendingSubmission = {
  requestId: string;
  key: string;
  sourceKey: string;
  workspace: string;
  providerId: string;
  modelIdentifier: string;
  threadId: string;
  previousTurnId: string | null;
  text: string;
  accepted: boolean;
};
type TurnState = "idle" | "starting" | "running" | "completed" | "interrupted" | "failed" | "unknown";

const field = (object: unknown, name: string): unknown => {
  if (!object || typeof object !== "object") return undefined;
  const source = object as JsonRecord;
  return source[name] ?? source[`${name[0]?.toUpperCase()}${name.slice(1)}`];
};
const hasField = (object: unknown, name: string): boolean => {
  if (!object || typeof object !== "object" || Array.isArray(object)) return false;
  const source = object as JsonRecord;
  return Object.hasOwn(source, name) || Object.hasOwn(source, `${name[0]?.toUpperCase()}${name.slice(1)}`);
};
const hasUnambiguousField = (object: unknown, name: string): boolean => {
  if (!object || typeof object !== "object" || Array.isArray(object)) return false;
  const source = object as JsonRecord;
  return Object.hasOwn(source, name) !== Object.hasOwn(source, `${name[0]?.toUpperCase()}${name.slice(1)}`);
};
const currentField = (object: unknown, name: string): unknown => {
  if (!object || typeof object !== "object" || Array.isArray(object)) return undefined;
  const source = object as JsonRecord;
  if (Object.hasOwn(source, name)) return source[name];
  return source[`${name[0]?.toUpperCase()}${name.slice(1)}`];
};
const hasProjectRecordRecoveryStatus = (value: unknown): boolean =>
  hasField(value, "projectRegistryResolved") || hasField(value, "executionBlocked") || hasField(value, "recoveryBlock");
const projectRecordRecoveryResolved = (value: unknown): boolean =>
  hasUnambiguousField(value, "projectRegistryResolved") && currentField(value, "projectRegistryResolved") === true
  && hasUnambiguousField(value, "executionBlocked") && currentField(value, "executionBlocked") === false
  && hasUnambiguousField(value, "recoveryBlock") && currentField(value, "recoveryBlock") === null;
const projectRecordRecoveryUnconfirmed = (message: string): JsonRecord => ({
  projectRegistryResolved: false,
  executionBlocked: true,
  executionBlockedReason: "projectRegistryRecoveryStatusUnconfirmed",
  recoveryBlock: {
    code: "projectRecordRecoveryStatusUnconfirmed",
    recordKey: "Projects",
    blocksExecution: true,
    blocksProjectWrites: true,
    message,
  },
});
const textValue = (value: unknown): string | undefined => typeof value === "string" && value.trim() ? value : undefined;
const recordValue = (value: unknown): JsonRecord | undefined => value && typeof value === "object" && !Array.isArray(value) ? value as JsonRecord : undefined;
const commandIdentityKey = (threadId: string, turnId: string, itemId: string) => JSON.stringify([threadId, turnId, itemId]);
const observation = (capability: JsonRecord | null, name: string) => {
  const value = recordValue(field(capability, name));
  return { state: textValue(field(value, "state")) ?? "Unknown", value: field(value, "value"), evidence: textValue(field(value, "evidenceSource")) ?? "Evidence source not recorded" };
};
const modelName = (capability: JsonRecord | null) => textValue(field(capability, "modelIdentifier")) ?? "Unknown model";
const providerName = (capability: JsonRecord | null) => textValue(field(capability, "providerDisplayName")) ?? "Unknown provider";
const displayValue = (value: unknown): string => {
  if (value === null || value === undefined) return "Unknown";
  if (["string", "number", "boolean"].includes(typeof value)) return String(value);
  if (Array.isArray(value)) return value.map(displayValue).join(" · ");
  return JSON.stringify(value);
};
const numberFormat = (value: unknown): string => typeof value === "number" && Number.isFinite(value) ? new Intl.NumberFormat("en-US").format(value) : "Unknown";
const contextTokens = (value: unknown): string => typeof value === "number" && Number.isFinite(value) ? `${numberFormat(value)} tokens` : "Unknown";
const latestTurnId = (turns: unknown): string | null => {
  if (!Array.isArray(turns)) return null;
  return textValue(field(recordValue(turns[0]), "id")) ?? null;
};
const latestUserText = (messages: ChatMessage[]): string | null => {
  for (let index = messages.length - 1; index >= 0; index -= 1) {
    if (messages[index].role === "user") return messages[index].text;
  }
  return null;
};
const pendingRecoveryWarning = (status: string): string | null => status === "unresolved" || status === "identityMismatch"
  ? "A prior send could not be matched safely to saved task history. Check this conversation before resending."
  : null;
const capabilityBindingWarning = (reason: unknown): string => ({
  capabilityBindingMissing: "This saved conversation has no recorded capability identity. It is open as history only; sending and forking are disabled. Start a new task to execute with the selected model.",
  capabilityBindingUnavailable: "The saved conversation’s capability identity could not be verified. Its history is read-only; the binding was not changed.",
  capabilityRecordChanged: "This conversation was created with a different capability record. Its history is read-only; select a matching record or start a new task.",
} as Record<string, string>)[String(reason)] ?? "This saved conversation is available as history only because its execution identity could not be verified.";
const shortPath = (value: unknown): string => {
  const path = textValue(value);
  return path ? path.replaceAll("/", "\\").split("\\").filter(Boolean).slice(-2).join("\\") : "Unavailable";
};

function Icon({ name, size = 18 }: { name: string; size?: number }) {
  const paths: Record<string, ReactNode> = {
    plus: <><path d="M12 5v14" /><path d="M5 12h14" /></>,
    search: <><circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" /></>,
    folder: <path d="M3.5 7.5a2 2 0 0 1 2-2h4l2 2h7a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z" />,
    chat: <><path d="M20.5 11.5a7.5 7.5 0 0 1-8 7.5 8.4 8.4 0 0 1-4-.9L4 20l1.3-3.7a7 7 0 0 1-1.3-4.3 7.5 7.5 0 0 1 8-7.5 7.5 7.5 0 0 1 7.5 7Z" /><path d="M8 11h8M8 14h5" /></>,
    chevron: <path d="m7 10 5 5 5-5" />,
    chevronRight: <path d="m9 18 6-6-6-6" />,
    arrow: <><path d="M5 12h14" /><path d="m13 6 6 6-6 6" /></>,
    stop: <rect x="6" y="6" width="12" height="12" rx="2" />,
    spark: <><path d="m12 3 1.7 5.3L19 10l-5.3 1.7L12 17l-1.7-5.3L5 10l5.3-1.7L12 3Z" /><path d="m19 15 .8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8L19 15Z" /></>,
    shield: <><path d="M12 3 19 6v5c0 4.5-3 8-7 10-4-2-7-5.5-7-10V6l7-3Z" /><path d="m9 12 2 2 4-4" /></>,
    pulse: <path d="M3 12h4l2-6 4 12 2-6h6" />,
    settings: <><circle cx="12" cy="12" r="3" /><path d="m19.4 15 .1.1 1.1 1.8-1.7 1.7-1.8-1.1-.1-.1a7.8 7.8 0 0 1-2 .8l-.5 2.1h-2.4l-.5-2.1a7.8 7.8 0 0 1-2-.8l-.1.1-1.8 1.1L6 16.9l1.1-1.8.1-.1a7.8 7.8 0 0 1-.8-2L4.3 12.5v-2.4l2.1-.5a7.8 7.8 0 0 1 .8-2l-.1-.1L6 5.7 7.7 4l1.8 1.1.1.1a7.8 7.8 0 0 1 2-.8l.5-2.1h2.4l.5 2.1a7.8 7.8 0 0 1 2 .8l.1-.1L19 4l1.7 1.7-1.1 1.8-.1.1a7.8 7.8 0 0 1 .8 2l2.1.5v2.4l-2.1.5a7.8 7.8 0 0 1-.9 2Z" /></>,
    terminal: <><path d="m4 7 5 5-5 5" /><path d="M12 17h8" /></>,
    check: <path d="m5 12 4 4L19 6" />,
    alert: <><path d="M10.3 4.2 2.7 17.4A1.8 1.8 0 0 0 4.2 20h15.6a1.8 1.8 0 0 0 1.5-2.6L13.7 4.2a2 2 0 0 0-3.4 0Z" /><path d="M12 9v4M12 16h.01" /></>,
    info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 8h.01" /></>,
    close: <><path d="m6 6 12 12M18 6 6 18" /></>,
    fork: <><circle cx="6" cy="4" r="2" /><circle cx="18" cy="7" r="2" /><circle cx="6" cy="20" r="2" /><path d="M6 6v12M18 9a7 7 0 0 1-7 7H8" /></>,
    more: <><circle cx="5" cy="12" r="1" /><circle cx="12" cy="12" r="1" /><circle cx="19" cy="12" r="1" /></>,
    sun: <><circle cx="12" cy="12" r="4" /><path d="M12 2v2m0 16v2M4.93 4.93l1.42 1.42m11.3 11.3 1.42 1.42M2 12h2m16 0h2M4.93 19.07l1.42-1.42m11.3-11.3 1.42-1.42" /></>,
    moon: <path d="M20.6 14.1A8.5 8.5 0 0 1 9.9 3.4 8.7 8.7 0 1 0 20.6 14.1Z" />,
  };
  return <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">{paths[name] ?? paths.info}</svg>;
}

export default function App({ appearance, onAppearanceChange }: { appearance: Appearance; onAppearanceChange: (appearance: Appearance) => void }) {
  const [runtime, setRuntime] = useState<JsonRecord | null>(null);
  const [capability, setCapability] = useState<JsonRecord | null>(null);
  const [capabilities, setCapabilities] = useState<CapabilityEntry[]>([]);
  const [savedThreads, setSavedThreads] = useState<SavedThread[]>([]);
  const [threadNamesById, setThreadNamesById] = useState<Record<string, string>>({});
  const [threadSearch, setThreadSearch] = useState("");
  const [threadListError, setThreadListError] = useState<string | null>(null);
  const [threadListWarning, setThreadListWarning] = useState<string | null>(null);
  const [historyNextCursor, setHistoryNextCursor] = useState<string | null>(null);
  const [refreshingThreads, setRefreshingThreads] = useState(false);
  const [loadingOlderThreads, setLoadingOlderThreads] = useState(false);
  const [reconnectWarning, setReconnectWarning] = useState<string | null>(null);
  const [restoredTurnWarning, setRestoredTurnWarning] = useState<string | null>(null);
  const [reconnecting, setReconnecting] = useState(true);
  const [messages, setMessagesState] = useState<ChatMessage[]>([]);
  const setMessages: Dispatch<SetStateAction<ChatMessage[]>> = useCallback((update) => {
    setMessagesState((current) => boundTranscript(typeof update === "function" ? update(current) : update));
  }, []);
  const [outputInspection, setOutputInspection] = useState<OutputInspectionLocation | null>(null);
  const [outputInspectionPage, setOutputInspectionPage] = useState<OutputInspectionPage | null>(null);
  const [outputInspectionLoading, setOutputInspectionLoading] = useState(false);
  const [outputInspectionError, setOutputInspectionError] = useState<string | null>(null);
  const [historyTruncated, setHistoryTruncated] = useState(false);
  const [savedOutputsTruncated, setSavedOutputsTruncated] = useState(false);
  const [activityState, setActivityState] = useState<{ entries: ActivityEntry[]; truncated: boolean }>({ entries: [], truncated: false });
  const activities = activityState.entries;
  const activitiesTruncated = activityState.truncated;
  const lateCommandBindingsRef = useRef(new Map<string, LateCommandNotificationBinding>());
  const setActivities: Dispatch<SetStateAction<ActivityEntry[]>> = useCallback((update) => {
    setActivityState((current) => {
      const isReplacement = typeof update !== "function";
      if (isReplacement) lateCommandBindingsRef.current.clear();
      const candidate = typeof update === "function" ? update(current.entries) : update;
      const bounded = boundActivityHistory(candidate);
      return {
        entries: bounded.entries,
        truncated: bounded.truncated || (!isReplacement && current.truncated),
      };
    });
  }, []);
  const [approvalRequests, setApprovalRequests] = useState<ApprovalRequest[]>([]);
  const [resolvingApprovalId, setResolvingApprovalId] = useState<number | null>(null);
  const [approvalError, setApprovalError] = useState<{ requestId: number; message: string } | null>(null);
  const [draftView, setDraftView] = useState<DraftView>({ key: null, text: "" });
  const [draftStorageError, setDraftStorageError] = useState<string | null>(null);
  const [threadId, setThreadId] = useState<string | null>(null);
  const [threadExecutionEligible, setThreadExecutionEligible] = useState(false);
  const [turnState, setTurnState] = useState<TurnState>("idle");
  const [startupError, setStartupError] = useState<string | null>(null);
  const [turnError, setTurnError] = useState<string | null>(null);
  const [switchingModel, setSwitchingModel] = useState(false);
  const [openingThreadId, setOpeningThreadId] = useState<string | null>(null);
  const [forkingThread, setForkingThread] = useState(false);
  const [creatingNewTask, setCreatingNewTask] = useState(false);
  const [changingProject, setChangingProject] = useState(false);
  const [projectError, setProjectError] = useState<string | null>(null);
  const [showModelMenu, setShowModelMenu] = useState(false);
  const [advancedOptionsOpen, setAdvancedOptionsOpen] = useState(false);
  const [capabilityDiagnosticsWarning, setCapabilityDiagnosticsWarning] = useState<string | null>(null);
  const [threadContextMenu, setThreadContextMenu] = useState<{ threadId: string; left: number; top: number } | null>(null);
  const [renameTarget, setRenameTarget] = useState<{ threadId: string; currentName: string } | null>(null);
  const [renameDraft, setRenameDraft] = useState("");
  const [renamePendingThreadId, setRenamePendingThreadId] = useState<string | null>(null);
  const [renameError, setRenameError] = useState<string | null>(null);
  const [activeCapabilityIndex, setActiveCapabilityIndex] = useState(0);
  const [showDetails, setShowDetails] = useState(true);
  const [diagnosticsOpen, setDiagnosticsOpenState] = useState(false);
  const [protectedRecordRecoveryOpen, setProtectedRecordRecoveryOpen] = useState(false);
  const [refreshingRecordRecovery, setRefreshingRecordRecovery] = useState(false);
  const [recordRecoveryRefreshMessage, setRecordRecoveryRefreshMessage] = useState<string | null>(null);
  const [unapprovedToolsOpen, setUnapprovedToolsOpen] = useState(false);
  const [unapprovedToolsLoading, setUnapprovedToolsLoading] = useState(false);
  const [unapprovedToolsError, setUnapprovedToolsError] = useState<string | null>(null);
  const [unapprovedTools, setUnapprovedTools] = useState<JsonRecord[]>([]);
  // Owned above the drawer/cards so a reload, reorder, or close cannot erase an unknown Stage outcome.
  const [unresolvedStage, setUnresolvedStage] = useState<{ target: StageBindingContext & { toolId: string }; message: string } | null>(null);
  const [stageRecoveryNotice, setStageRecoveryNotice] = useState<string | null>(null);
  const [outputOverride, setOutputOverride] = useState("");
  const [reasoningSelection, setReasoningSelection] = useState<ReasoningSelection | null>(null);
  const [compactingContext, setCompactingContext] = useState(false);
  const [compactFeedback, setCompactFeedback] = useState<CompactFeedback | null>(null);
  const [reviewOpen, setReviewOpen] = useState(false);
  const [turnReview, setTurnReview] = useState<TurnReview | null>(null);
  const [fullAccessNoticeDismissed, setFullAccessNoticeDismissed] = useState(isFullAccessNoticeHidden);
  const [fullAccessNoticeStorageError, setFullAccessNoticeStorageError] = useState(false);
  const [credentialCaptured, setCredentialCaptured] = useState(false);
  const [streamEventCount, setStreamEventCount] = useState(0);
  const streamEventCountRef = useRef(0);
  const composerRef = useRef<HTMLTextAreaElement>(null);
  const conversationRef = useRef<HTMLElement>(null);
  const modelSelectRef = useRef<HTMLButtonElement>(null);
  const modelListRef = useRef<HTMLDivElement>(null);
  const threadMenuItemRef = useRef<HTMLButtonElement>(null);
  const threadMenuOpenerRef = useRef<HTMLElement | null>(null);
  const renameDialogRef = useRef<HTMLElement>(null);
  const renameInputRef = useRef<HTMLInputElement>(null);
  const renameReturnFocusRef = useRef<HTMLElement | null>(null);
  const outputOptionsOpenerRef = useRef<HTMLButtonElement>(null);
  const diagnosticsOpenerRef = useRef<HTMLElement | null>(null);
  const unapprovedToolsOpenerRef = useRef<HTMLElement | null>(null);
  const unapprovedToolsRequestRef = useRef(0);
  const candidateRequestQueueRef = useRef<ReturnType<typeof createSerialRequestQueue> | null>(null);
  if (candidateRequestQueueRef.current === null) candidateRequestQueueRef.current = createSerialRequestQueue();
  const reviewOpenerRef = useRef<HTMLButtonElement | null>(null);
  const outputInspectionOpenerRef = useRef<HTMLElement | null>(null);
  const threadSearchRef = useRef<HTMLInputElement>(null);
  const activeRequestRef = useRef<string | null>(null);
  const compactRequestRef = useRef<{ requestId: string; threadId: string; identityKey: string; epoch: number } | null>(null);
  const pendingSubmissionRef = useRef<PendingSubmission | null>(null);
  const latestTurnIdRef = useRef<string | null>(null);
  const outputInspectionRequestRef = useRef(0);
  const threadListGenerationRef = useRef(0);
  const threadRefreshCountRef = useRef(0);
  const assistantDeltaBatcherRef = useRef<AssistantDeltaBatcher | null>(null);
  if (assistantDeltaBatcherRef.current === null) {
    assistantDeltaBatcherRef.current = createAssistantDeltaBatcher({
      schedule: (callback, delayMs) => window.setTimeout(callback, delayMs),
      cancel: (timer) => window.clearTimeout(timer),
      onBatch: (batch) => {
        setMessages((current) => batch.reduce(
          (next, item) => appendAssistantDelta(next, item.id, item.delta, item.displayMetadata), current));
        setStreamEventCount(streamEventCountRef.current);
      },
    });
  }

  function setDiagnosticsOpen(open: boolean) {
    if (open) {
      diagnosticsOpenerRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      setDiagnosticsOpenState(true);
      return;
    }
    setDiagnosticsOpenState(false);
    const opener = diagnosticsOpenerRef.current;
    requestAnimationFrame(() => {
      if (opener?.isConnected && !opener.closest("[inert]")) opener.focus();
    });
  }

  const requestCandidateHost: CandidateHostRequest = (operation, payload = {}) =>
    candidateRequestQueueRef.current!(() => requestHost<JsonRecord>(operation, payload));

  async function loadUnapprovedTools() {
    const request = ++unapprovedToolsRequestRef.current;
    setUnapprovedToolsError(null);
    setUnapprovedToolsLoading(true);
    try {
      const result = await requestCandidateHost("listGeneratedToolCandidates");
      if (request !== unapprovedToolsRequestRef.current) return;
      const candidates = field(result, "candidates");
      if (field(result, "attributedTo") !== "NeoBabylon.Host"
          || !Array.isArray(candidates)
          || candidates.some((item) => !recordValue(item) || field(item, "state") !== "unapproved" || !textValue(field(item, "toolId")))
          || new Set(candidates.map((item) => textValue(field(item, "toolId")))).size !== candidates.length) {
        throw new Error("The host did not return a valid unapproved candidate listing.");
      }
      setUnapprovedTools(candidates as JsonRecord[]);
    } catch (error) {
      if (request === unapprovedToolsRequestRef.current) {
        setUnapprovedToolsError(error instanceof Error ? error.message : "Unapproved candidates could not be listed.");
      }
    } finally {
      if (request === unapprovedToolsRequestRef.current) setUnapprovedToolsLoading(false);
    }
  }

  function openUnapprovedTools() {
    unapprovedToolsOpenerRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setUnapprovedTools([]);
    setUnapprovedToolsOpen(true);
    void loadUnapprovedTools();
  }

  function closeUnapprovedTools() {
    unapprovedToolsRequestRef.current += 1;
    setUnapprovedToolsOpen(false);
    const opener = unapprovedToolsOpenerRef.current;
    requestAnimationFrame(() => {
      if (opener?.isConnected && !opener.closest("[inert]")) opener.focus();
    });
  }

  function toggleAppearance() {
    const nextAppearance = appearance === "dark" ? "light" : "dark";
    onAppearanceChange(nextAppearance);
  }

  async function refreshThreads(cursor?: string) {
    const generation = threadListGenerationRef.current;
    threadRefreshCountRef.current += 1;
    setRefreshingThreads(true);
    try {
      const result = await requestHost<JsonRecord>("listThreads", cursor ? { cursor } : undefined);
      if (generation !== threadListGenerationRef.current) return null;
      const entries = field(result, "threads");
      const historyQuery = recordValue(field(result, "historyQuery"));
      const unresolvedBookmarks = field(historyQuery, "unresolvedForkBookmarkCount");
      setThreadListWarning(typeof unresolvedBookmarks === "number" && unresolvedBookmarks > 0
        ? `${unresolvedBookmarks} saved branch ${unresolvedBookmarks === 1 ? "bookmark could not" : "bookmarks could not"} be verified. Those entries are hidden until App Server identity can be confirmed.`
        : null);
      const projected: SavedThread[] = Array.isArray(entries) ? entries.flatMap((entry) => {
        const thread = recordValue(entry);
        const id = textValue(field(thread, "id"));
        if (!thread || !id) return [];
        return [{
          id,
          preview: savedThreadLabel({
            preview: textValue(field(thread, "preview")),
            forkedFromId: textValue(field(thread, "forkedFromId")),
          }),
          name: textValue(field(thread, "name")),
          forkedFromId: textValue(field(thread, "forkedFromId")),
          modelProvider: textValue(field(thread, "modelProvider")) ?? "Unknown",
          model: textValue(field(thread, "model")) ?? "Unknown",
          cwd: textValue(field(thread, "cwd")),
          updatedAt: savedThreadUpdatedAt(field(thread, "updatedAt")),
        }];
      }) : [];
      setSavedThreads((current) => mergeHistoryPages(cursor ? current : [], projected));
      const nextCursor = textValue(field(result, "nextCursor")) ?? null;
      setHistoryNextCursor(nextCursor);
      setThreadListError(null);
      return { threads: projected, nextCursor };
    } catch (error) {
      if (generation !== threadListGenerationRef.current) return null;
      const message = error instanceof Error ? error.message : "Saved App Server history is unavailable.";
      if (cursor) setThreadListWarning(`Older conversations could not be loaded: ${message}`);
      else {
        setThreadListWarning(null);
        setThreadListError(message);
      }
      return null;
    } finally {
      threadRefreshCountRef.current -= 1;
      if (threadRefreshCountRef.current === 0) setRefreshingThreads(false);
    }
  }

  async function refreshProtectedRecordRecoveryStatus(): Promise<string> {
    if (refreshingRecordRecovery) return "A host recovery-status refresh is already in progress.";
    setRefreshingRecordRecovery(true);
    try {
      const runtimeResult = await requestHost<JsonRecord>("getRuntimeStatus");
      const returnedProjectionComplete = ["projectRegistryResolved", "executionBlocked", "recoveryBlock"]
        .every((name) => hasUnambiguousField(runtimeResult, name));
      const returnedResolved = projectRecordRecoveryResolved(runtimeResult);
      setRuntime((current) => ({
        ...(current ?? {}),
        ...runtimeResult,
        ...(!returnedProjectionComplete ? projectRecordRecoveryUnconfirmed(
          "The latest Host runtime-status response omitted one or more required project-recovery fields. Normal operations remain paused until a complete current projection is returned.",
        ) : {}),
      }));
      if (!returnedProjectionComplete || !returnedResolved) {
        return !returnedProjectionComplete
          ? "The latest Host runtime-status response omitted one or more required project-recovery fields. Model and project operations remain blocked until a complete current projection is returned."
          : `The host has not resolved project-record recovery (projectRegistryResolved=${String(field(runtimeResult, "projectRegistryResolved"))}, executionBlocked=${String(field(runtimeResult, "executionBlocked"))}, recoveryBlock=${textValue(field(recordValue(field(runtimeResult, "recoveryBlock")), "code")) ?? "present"}); model and project operations remain blocked.`;
      }

      setStartupError(null);
      let diagnosticRefreshNote = "";
      try {
        const diagnosticsResult = await requestHost<JsonRecord>("getDiagnostics");
        setRuntime((current) => current ? { ...current, capabilityRecordPath: field(diagnosticsResult, "capabilityRecordPath") } : current);
        setCapability(recordValue(field(diagnosticsResult, "capabilityRecord")) ?? null);
        setCredentialCaptured(field(diagnosticsResult, "providerCredentialCaptured") === true);
      } catch (error) {
        diagnosticRefreshNote = ` Runtime diagnostic details could not be refreshed: ${error instanceof Error ? error.message : "unknown host error"}.`;
      }
      const history = await refreshThreads();
      return `The host reports projectRegistryResolved=true, executionBlocked=false, and recoveryBlock=null. Runtime project metadata and saved conversations were refreshed${history ? "" : " where available"}.${diagnosticRefreshNote}`;
    } catch (error) {
      const message = `Could not confirm current project-record recovery status: ${error instanceof Error ? error.message : "unknown host error"}. Normal operations remain paused until the Host returns a complete resolved projection.`;
      setRuntime((current) => ({ ...(current ?? {}), ...projectRecordRecoveryUnconfirmed(message) }));
      return `Could not refresh host recovery status: ${error instanceof Error ? error.message : "unknown host error"}. Model and project operations remain blocked until the host reports projectRegistryResolved=true, executionBlocked=false, and recoveryBlock=null.`;
    } finally {
      setRefreshingRecordRecovery(false);
    }
  }

  async function loadOlderThreads() {
    if (!historyNextCursor || loadingOlderThreads || isBusy) return;
    setLoadingOlderThreads(true);
    try { await refreshThreads(historyNextCursor); }
    finally { setLoadingOlderThreads(false); }
  }

  useEffect(() => {
    let mounted = true;
    void requestHost<JsonRecord>("getRuntimeStatus").then(async (runtimeResult) => {
      if (!mounted) return;
      setRuntime(runtimeResult);
      const [diagnosticsAttempt, catalogAttempt] = await Promise.allSettled([
        requestHost<JsonRecord>("getDiagnostics"),
        requestHost<JsonRecord>("listCapabilities"),
      ]);
      if (!mounted) return;
      let selectedCapability = recordValue(field(runtimeResult, "capabilityRecord")) ?? null;
      if (diagnosticsAttempt.status === "fulfilled") {
        const diagnosticsResult = diagnosticsAttempt.value;
        setRuntime((current) => current ? { ...current, capabilityRecordPath: field(diagnosticsResult, "capabilityRecordPath") } : current);
        selectedCapability = recordValue(field(diagnosticsResult, "capabilityRecord")) ?? selectedCapability;
        setCapability(selectedCapability);
        setCredentialCaptured(field(diagnosticsResult, "providerCredentialCaptured") === true);
      } else {
        setReconnectWarning(`Runtime status is available, but capability diagnostics could not be loaded: ${diagnosticsAttempt.reason instanceof Error ? diagnosticsAttempt.reason.message : "unknown host error"}.`);
      }
      if (catalogAttempt.status === "fulfilled") {
        const entries = field(catalogAttempt.value, "records");
        setCapabilities(Array.isArray(entries) ? entries.flatMap((entry) => {
          const value = recordValue(entry);
          const item = recordValue(field(value, "capabilityRecord"));
          const sourceFile = textValue(field(value, "sourceFile"));
          return item && sourceFile ? [{ sourceFile, capabilityRecord: item }] : [];
        }) : []);
      } else {
        setCapabilities([]);
      }
      const page = await refreshThreads();
      if (!mounted || !page) return;
      if (hasProjectRecordRecoveryStatus(runtimeResult) && !projectRecordRecoveryResolved(runtimeResult)) {
        setReconnectWarning("The host has not reported projectRegistryResolved=true, executionBlocked=false, and recoveryBlock=null. The saved task was not reopened; no turn was started or replayed.");
        return;
      }
      try {
        const pointer = readActiveTask(window.localStorage);
        if (!pointer) return;
        const selection = {
          workspace: textValue(field(runtimeResult, "selectedWorkspace")),
          providerId: textValue(field(selectedCapability, "providerId")),
          modelIdentifier: textValue(field(selectedCapability, "modelIdentifier")),
        };
        const eligibility = classifyActiveTaskListing(pointer, selection, page.threads);
        if (eligibility !== "ready" && eligibility !== "threadMissing") {
          setReconnectWarning({
            workspaceMismatch: "The previous task belongs to another project. Select it from saved history; no project was changed automatically.",
            providerMismatch: "The previous task uses a different provider. Select its exact capability record before reopening it.",
            modelMismatch: "The previous task uses a different model. Select its exact capability record before reopening it.",
            threadMissing: "The previous task is not in the currently available saved history. No turn was replayed.",
            threadMismatch: "The saved task identity does not match the recorded project/provider/model. It was not reopened.",
            readOnly: "The saved task is available as history only; its capability identity must be verified before execution.",
          }[eligibility] ?? "The saved task identity could not be verified. No turn was replayed.");
          return;
        }
        setOpeningThreadId(pointer.threadId);
        try {
          const resumed = await requestHost<JsonRecord>("resumeThread", { threadId: pointer.threadId });
          if (!mounted) return;
          const frozenCapability = confirmedResumeCapability(resumed, { threadId: pointer.threadId, providerId: pointer.providerId, modelIdentifier: pointer.modelIdentifier });
          const executionEligible = field(resumed, "executionEligible") === true && frozenCapability !== null;
          if (frozenCapability) {
            setCapability(frozenCapability);
            setReasoningSelection(initialReasoningSelection(frozenCapability));
          }
          const resumedIdentity = classifyActiveTask(pointer, selection, [{
            id: textValue(field(resumed, "threadId")) ?? "",
            cwd: textValue(field(resumed, "cwd")),
            modelProvider: textValue(field(resumed, "modelProvider")) ?? "",
            model: textValue(field(resumed, "model")) ?? "",
          }], executionEligible);
          if (resumedIdentity !== "ready" && resumedIdentity !== "readOnly") throw new Error("The App Server resume response did not match the saved task identity.");
          const resumedTurns = field(resumed, "turns");
          const restoredMessages = restoreVisibleTranscript(resumedTurns, pointer.threadId);
          latestTurnIdRef.current = latestTurnId(resumedTurns);
          let draftRecovery: ReturnType<typeof reconcilePendingSubmission> = { status: "none" };
          if (executionEligible) {
            try {
              draftRecovery = reconcilePendingSubmission(window.localStorage, {
                workspace: pointer.workspace,
                providerId: pointer.providerId,
                modelIdentifier: pointer.modelIdentifier,
                threadId: pointer.threadId,
                latestTurnId: latestTurnId(resumedTurns),
                latestUserText: latestUserText(restoredMessages),
              });
            } catch {
              draftRecovery = { status: "unresolved" };
            }
          }
          setThreadId(pointer.threadId);
          setThreadExecutionEligible(executionEligible);
          setMessages(restoredMessages);
          setActivities(restoreSavedActivities(field(resumed, "savedOutputs"), pointer.threadId));
          setSavedOutputsTruncated(field(resumed, "savedOutputsTruncated") === true);
          setTurnReview(restoreSavedTurnReview(field(resumed, "savedReviews"), pointer.threadId));
          if (eligibility === "threadMissing") {
            const firstUser = restoredMessages.find((message) => message.role === "user")?.text;
            setSavedThreads((current) => current.some((thread) => thread.id === pointer.threadId) ? current : [{
              id: pointer.threadId,
              preview: firstUser?.slice(0, 90) ?? "Saved task",
              modelProvider: pointer.providerId,
              model: pointer.modelIdentifier,
              cwd: pointer.workspace,
              updatedAt: 0,
            }, ...current]);
          }
          setHistoryTruncated(field(resumed, "historyTruncated") === true);
          const restoredOutcome = restoreTurnOutcome(field(resumed, "turns"));
          setTurnState(restoredOutcome.state);
          setRestoredTurnWarning(restoredOutcome.warning);
          setRuntime((current) => current ? { ...current, lastThreadStart: resumed, lastModelContextEvidence: null } : current);
          setReconnectWarning(executionEligible
            ? pendingRecoveryWarning(draftRecovery.status)
            : field(resumed, "executionEligible") === true && !frozenCapability
              ? "The host did not confirm this saved task’s exact frozen capability record. History is open read-only; current catalog controls were not substituted."
              : capabilityBindingWarning(field(resumed, "executionBlockedReason")));
        } finally {
          if (mounted) setOpeningThreadId(null);
        }
      } catch (error) {
        if (mounted) setReconnectWarning(`The previous task could not be reconnected: ${error instanceof Error ? error.message : "unknown saved-state error"}. No turn was replayed.`);
      }
    }).catch((error: unknown) => {
      if (mounted) setStartupError(error instanceof Error ? error.message : "The local host bridge did not respond.");
    }).finally(() => {
      if (mounted) setReconnecting(false);
    });
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    const unsubscribe = listenToHost((message) => {
      if (!message.stream) return;
      const compactRequest = compactRequestRef.current;
      if (compactRequest && message.requestId === compactRequest.requestId) {
        const params = recordValue(message.params);
        if (taskTelemetry.matchesIdentity(compactRequest.identityKey, compactRequest.epoch)
          && field(params, "threadId") === compactRequest.threadId) {
          taskTelemetry.notification(message);
          if (message.method === "turn/started") setCompactFeedback({ status: "pending", message: "Codex App Server: Compaction started; awaiting the host's final outcome." });
        }
        return;
      }
      if (message.requestId === activeRequestRef.current) {
        handleNotification(message);
        return;
      }

      if (message.method !== "item/completed") return;
      const params = recordValue(message.params);
      const item = recordValue(field(params, "item"));
      if (field(item, "type") !== "commandExecution") return;
      const threadId = textValue(field(params, "threadId"));
      const turnId = textValue(field(params, "turnId"));
      const itemId = textValue(field(item, "id"));
      if (!threadId || !turnId || !itemId) return;
      const key = commandIdentityKey(threadId, turnId, itemId);
      const binding = lateCommandBindingsRef.current.get(key);
      if (!binding || !matchesLateCommandCompletion(message, binding)) return;
      handleNotification(message);
    });
    return () => { unsubscribe(); assistantDeltaBatcherRef.current?.clear(); };
  }, []);

  const currentModel = modelName(capability);
  const currentProvider = providerName(capability);
  const currentProviderId = textValue(field(capability, "providerId"));
  useEffect(() => {
    if (!showModelMenu) return;
    const selectedIndex = capabilities.findIndex((entry) => sameCapabilityRecord(entry.capabilityRecord, capability));
    setActiveCapabilityIndex(selectedIndex >= 0 ? selectedIndex : 0);
    const frame = requestAnimationFrame(() => modelListRef.current?.focus());
    return () => cancelAnimationFrame(frame);
  }, [showModelMenu, capabilities, capability]);
  const providerContextAdvertised = observation(capability, "contextWindowAdvertised");
  const providerContextEffective = observation(capability, "contextWindowEffective");
  const completionCap = completionLimit(recordValue(field(capability, "maxCompletionTokensAdvertised")));
  const outputSelection = parseOutputOverride(outputOverride, completionCap);
  const reasoning = observation(capability, "reasoningControls");
  const toolCallingAdvertised = observation(capability, "toolFunctionCalling");
  const structuredOutput = observation(capability, "structuredOutput");
  const policy = recordValue(field(runtime, "executionPolicy"));
  const lastThreadAuthority = recordValue(field(recordValue(field(runtime, "lastThreadStart")), "executionAuthority"));
  const executionPolicyView = executionPolicyPresentation(policy, lastThreadAuthority);
  const dataRoot = field(runtime, "dataRoot");
  const codexHome = field(runtime, "codexHome");
  const appServerVersion = textValue(field(recordValue(field(runtime, "runtime")), "version")) ?? "Unknown";
  const routeObservation = observation(capability, "providerRoute");
  const routeLabel = textValue(field(recordValue(routeObservation.value), "endpointTag")) ?? "Route unknown";
  const selectedWorkspace = textValue(field(runtime, "selectedWorkspace")) ?? textValue(field(runtime, "fixtureWorkspace"));
  const telemetryIdentity = { workspace: selectedWorkspace ?? null, threadId, providerId: currentProviderId ?? null, modelIdentifier: textValue(field(capability, "modelIdentifier")) ?? null };
  const taskTelemetry = useTaskTelemetry(telemetryIdentity);
  const requestedReasoning = parseReasoningSelection(capability, reasoningSelection);
  const reasoningPolicy = reasoningOptions(capability);
  useEffect(() => { setCompactFeedback(null); }, [taskTelemetry.identityKey]);
  const draftScope = draftKey(selectedWorkspace, threadId);
  const draft = draftView.key === draftScope ? draftView.text : "";
  useEffect(() => {
    if (!draftScope) return;
    try {
      setDraftView({ key: draftScope, text: loadDraft(window.localStorage, draftScope) });
      setDraftStorageError(null);
    } catch {
      setDraftView({ key: draftScope, text: "" });
      setDraftStorageError("Saved drafts are unavailable in this WebView profile. Unsent text may not survive a restart.");
    }
  }, [draftScope]);
  const projects: ProjectEntry[] = Array.isArray(field(runtime, "projects"))
    ? (field(runtime, "projects") as unknown[]).flatMap((entry) => {
      const value = recordValue(entry);
      const workspacePath = textValue(field(value, "workspacePath"));
      return workspacePath ? [{ workspacePath, name: textValue(field(value, "name")) ?? shortPath(workspacePath), available: field(value, "available") === true }] : [];
    }) : [];
  const selectedProject = projects.find((project) => project.workspacePath.toLowerCase() === selectedWorkspace?.toLowerCase());
  const selectedProjectName = selectedProject?.name ?? "NeoBabylon";
  const selectedWorkspaceAvailable = field(runtime, "selectedWorkspaceAvailable") !== false;
  const recordRecoveryProjectionPresent = hasProjectRecordRecoveryStatus(runtime);
  const recordRecoveryProjection = recordValue(field(runtime, "recoveryBlock"));
  const recordRecoveryResolved = recordRecoveryProjectionPresent && projectRecordRecoveryResolved(runtime);
  const recordRecoveryBlocked = recordRecoveryProjectionPresent && !recordRecoveryResolved;
  const recordRecoveryStatus = recordRecoveryResolved ? "resolved"
    : textValue(field(recordRecoveryProjection, "code"))
      ?? textValue(field(runtime, "executionBlockedReason")) ?? "recovery status unavailable";
  const recordRecoveryKey = textValue(field(recordRecoveryProjection, "recordKey"));
  const recordRecoveryReason = textValue(field(recordRecoveryProjection, "message"));
  const isBusy = reconnecting || refreshingThreads || loadingOlderThreads || turnState === "starting" || turnState === "running" || switchingModel || openingThreadId !== null || forkingThread || creatingNewTask || changingProject || refreshingRecordRecovery || recordRecoveryBlocked || renamePendingThreadId !== null || compactingContext;
  const threadReadOnly = Boolean(threadId) && !threadExecutionEligible;

  useEffect(() => {
    if (!threadContextMenu) return;
    const frame = requestAnimationFrame(() => threadMenuItemRef.current?.focus());
    return () => cancelAnimationFrame(frame);
  }, [threadContextMenu]);

  useEffect(() => {
    if (!renameTarget) return;
    const frame = requestAnimationFrame(() => {
      renameInputRef.current?.focus();
      renameInputRef.current?.select();
    });
    return () => cancelAnimationFrame(frame);
  }, [renameTarget]);

  useEffect(() => {
    setThreadContextMenu(null);
  }, [threadId]);

  function openThreadContextMenu(savedThreadId: string, anchor: HTMLElement, opener: HTMLElement, clientX: number, clientY: number) {
    if (isBusy || renameTarget) return;
    const bounds = anchor.getBoundingClientRect();
    const left = Math.max(0, Math.min(bounds.width - 148, clientX - bounds.left - 112));
    const top = Math.max(0, Math.min(bounds.height - 26, clientY - bounds.top));
    threadMenuOpenerRef.current = opener;
    setThreadContextMenu({ threadId: savedThreadId, left, top });
  }

  function closeThreadContextMenu(restoreFocus = true) {
    setThreadContextMenu(null);
    if (restoreFocus) requestAnimationFrame(() => threadMenuOpenerRef.current?.focus());
  }

  function beginRenameThread(savedThread: SavedThread) {
    const currentName = threadNamesById[savedThread.id] ?? savedThread.name ?? savedThread.preview ?? "Untitled task";
    renameReturnFocusRef.current = threadMenuOpenerRef.current;
    setThreadContextMenu(null);
    setRenameDraft(currentName);
    setRenameError(null);
    setRenameTarget({ threadId: savedThread.id, currentName });
  }

  function closeRenameDialog() {
    if (renamePendingThreadId !== null) return;
    setRenameTarget(null);
    setRenameError(null);
    requestAnimationFrame(() => renameReturnFocusRef.current?.focus());
  }

  async function submitThreadRename(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!renameTarget || renamePendingThreadId !== null) return;
    const validated = validateThreadName(renameDraft);
    if (validated.error) {
      setRenameError(validated.error);
      return;
    }

    const capturedThreadId = renameTarget.threadId;
    setRenamePendingThreadId(capturedThreadId);
    setRenameError(null);
    try {
      const confirmation = await renameSavedThread(
        (operation, payload) => requestHost<JsonRecord>(operation as HostOperation, payload),
        capturedThreadId,
        validated.name,
      );
      setThreadNamesById((current) => ({ ...current, [confirmation.threadId]: confirmation.name }));
      setSavedThreads((current) => current.map((thread) => thread.id === confirmation.threadId
        ? { ...thread, name: confirmation.name }
        : thread));
      setRenameTarget(null);
      requestAnimationFrame(() => renameReturnFocusRef.current?.focus());
    } catch (error) {
      setRenameError(error instanceof Error ? error.message : "The host could not confirm this conversation rename.");
    } finally {
      setRenamePendingThreadId(null);
    }
  }

  function recordActiveTask(id: string, selectedIdentity?: { providerId: string; modelIdentifier: string }) {
    const providerId = selectedIdentity?.providerId ?? textValue(field(capability, "providerId"));
    const modelIdentifier = selectedIdentity?.modelIdentifier ?? textValue(field(capability, "modelIdentifier"));
    if (!selectedWorkspace || !providerId || !modelIdentifier) {
      setReconnectWarning("This task is open, but its navigation hint was not saved because exact project/provider/model identity is unavailable.");
      return;
    }
    try {
      saveActiveTask(window.localStorage, { workspace: selectedWorkspace, providerId, modelIdentifier, threadId: id });
      setReconnectWarning(null);
    } catch {
      setReconnectWarning("This task is open, but its navigation hint could not be saved locally. Reopen it from saved history after restart.");
    }
  }

  function forgetActiveTask() {
    try {
      clearActiveTask(window.localStorage);
      setReconnectWarning(null);
    } catch {
      setReconnectWarning("The previous active-task hint could not be cleared locally. It may reopen after restart; use saved history to verify the selected task.");
    }
  }

  function updateDraft(text: string) {
    if (!draftScope) return;
    setDraftView({ key: draftScope, text });
    try {
      saveDraft(window.localStorage, draftScope, text);
      setDraftStorageError(null);
    } catch {
      setDraftStorageError("This draft could not be saved locally. Keep this window open until you can send or copy it.");
    }
  }

  function acceptPendingSubmission(requestId: string) {
    const pending = pendingSubmissionRef.current;
    if (!pending || pending.requestId !== requestId || pending.accepted) return;
    pending.accepted = true;
    setOutputOverride("");
    try {
      saveDraft(window.localStorage, pending.key, "");
      if (pending.sourceKey !== pending.key && loadDraft(window.localStorage, pending.sourceKey) === pending.text) {
        saveDraft(window.localStorage, pending.sourceKey, "");
      }
      clearPendingSubmission(window.localStorage, pending.workspace, pending.threadId);
      setDraftStorageError(null);
    } catch {
      setDraftStorageError("The sent draft could not be cleared from local storage. Check history before resending after a restart.");
    }
    setDraftView((current) => current.key === pending.key || current.key === pending.sourceKey
      ? { ...current, text: "" }
      : current);
  }

  async function changeProject(operation: "addProject" | "selectProject", workspacePath?: string) {
    if (isBusy || compactRequestRef.current) return;
    if (draft.trim()) {
      setProjectError("Send or clear the unsent draft before changing projects.");
      return;
    }
    setChangingProject(true);
    setProjectError(null);
    try {
      const result = await requestHost<JsonRecord>(operation, workspacePath ? { workspacePath } : undefined);
      if (field(result, "cancelled") === true) return;
      const nextWorkspace = textValue(field(result, "selectedWorkspace"));
      if (!nextWorkspace) throw new Error("The host did not confirm the selected project.");
      const changed = nextWorkspace.toLowerCase() !== selectedWorkspace?.toLowerCase();
      if (changed) {
        setOutputOverride("");
        forgetActiveTask();
        latestTurnIdRef.current = null;
        threadListGenerationRef.current += 1;
        setSavedThreads([]);
        setHistoryNextCursor(null);
        setThreadListWarning(null);
        setThreadListError(null);
        setThreadId(null);
        setThreadExecutionEligible(false);
        setMessages([]);
        setHistoryTruncated(false);
        setActivities([]);
        setSavedOutputsTruncated(false);
        setTurnReview(null);
        setApprovalRequests([]);
        setApprovalError(null);
        setTurnState("idle");
        setTurnError(null);
        setRestoredTurnWarning(null);
        setThreadSearch("");
        setShowModelMenu(false);
      }
      setRuntime((current) => ({ ...current, ...result,
        ...(changed ? { lastThreadStart: null, lastModelContextEvidence: null } : {}),
      }));
      await refreshThreads();
      requestAnimationFrame(() => composerRef.current?.focus());
    } catch (error) {
      setProjectError(error instanceof Error ? error.message : "The project could not be selected.");
    } finally {
      setChangingProject(false);
    }
  }

  async function selectModel(entry: CapabilityEntry) {
    if (!canSelectCapability({ busy: isBusy }) || compactRequestRef.current) return;
    const providerId = textValue(field(entry.capabilityRecord, "providerId"));
    const identifier = textValue(field(entry.capabilityRecord, "modelIdentifier"));
    if (!providerId || !identifier || sameCapabilityRecord(entry.capabilityRecord, capability)) {
      setShowModelMenu(false);
      requestAnimationFrame(() => modelSelectRef.current?.focus());
      return;
    }
    setSwitchingModel(true);
    setTurnError(null);
    setCapabilityDiagnosticsWarning(null);
    try {
      const transition = await selectCapabilityForNextTurn(
        (operation, payload) => requestHost<JsonRecord>(operation, payload),
        { providerId, modelIdentifier: identifier, currentThreadId: threadId, busy: isBusy, capabilityRecord: entry.capabilityRecord },
        (accepted) => {
          threadListGenerationRef.current += 1;
          taskTelemetry.reset({ workspace: selectedWorkspace ?? null, threadId: accepted.activeThreadId, providerId, modelIdentifier: identifier });
          if (providerId === currentProviderId && identifier === currentModel) void taskTelemetry.refresh();
          setReasoningSelection(initialReasoningSelection(accepted.capabilityRecord));
          setCapability(accepted.capabilityRecord);
          setCredentialCaptured(false);
          if (accepted.activeThreadId) recordActiveTask(accepted.activeThreadId, { providerId, modelIdentifier: identifier });
          setShowModelMenu(false);
        },
      );
      if (transition.status === "blocked") return;
      if (transition.status === "diagnostics-warning") {
        setCapabilityDiagnosticsWarning(transition.warning);
      } else {
        setRuntime({ ...transition.runtimeResult, capabilityRecordPath: field(transition.diagnosticsResult, "capabilityRecordPath") });
        setCredentialCaptured(field(transition.diagnosticsResult, "providerCredentialCaptured") === true);
      }
      await refreshThreads();
      requestAnimationFrame(() => modelSelectRef.current?.focus());
    } catch (error) {
      setTurnError(error instanceof Error ? error.message : "The exact model selection could not be applied.");
    } finally {
      setSwitchingModel(false);
    }
  }

  function handleModelMenuKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape") {
      event.preventDefault();
      setShowModelMenu(false);
      requestAnimationFrame(() => modelSelectRef.current?.focus());
      return;
    }
    const nextIndex = nextListboxIndex(event.key, activeCapabilityIndex, capabilities.length);
    if (nextIndex !== null) {
      event.preventDefault();
      if (nextIndex >= 0) setActiveCapabilityIndex(nextIndex);
      return;
    }
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      if (!canSelectCapability({ busy: isBusy })) return;
      const active = capabilities[activeCapabilityIndex];
      if (active) void selectModel(active);
    }
  }

  async function openSavedThread(savedThread: SavedThread) {
    if (isBusy || compactRequestRef.current || savedThread.id === threadId) return;
    const selectedProvider = textValue(field(capability, "providerId"));
    const selectedModel = textValue(field(capability, "modelIdentifier"));

    setOpeningThreadId(savedThread.id);
    setTurnError(null);
    try {
      const result = await requestHost<JsonRecord>("resumeThread", { threadId: savedThread.id });
      const resumedId = textValue(field(result, "threadId"));
      if (resumedId !== savedThread.id) throw new Error("The App Server returned a different thread identity; the history was not opened.");
      const resumedProvider = textValue(field(result, "modelProvider"));
      const resumedModel = textValue(field(result, "model"));
      const frozenCapability = confirmedResumeCapability(result, { threadId: savedThread.id, providerId: selectedProvider ?? null, modelIdentifier: selectedModel ?? null });
      const bindingConfirmed = resumedProvider === selectedProvider && resumedModel === selectedModel && frozenCapability !== null;
      const executionEligible = field(result, "executionEligible") === true && bindingConfirmed;
      const resumeBindingWarning = field(result, "executionEligible") === true && !bindingConfirmed
        ? "The host did not confirm this saved task’s exact frozen capability record. Its history is open read-only; no model or current catalog controls were substituted."
        : null;
      if (bindingConfirmed && selectedProvider && selectedModel) {
        setCapability(frozenCapability);
        setReasoningSelection(initialReasoningSelection(frozenCapability));
        recordActiveTask(savedThread.id, { providerId: selectedProvider, modelIdentifier: selectedModel });
      }
      const turns = field(result, "turns");
      const restored = restoreVisibleTranscript(turns, savedThread.id);
      latestTurnIdRef.current = latestTurnId(turns);
      let draftRecovery: ReturnType<typeof reconcilePendingSubmission> = { status: "none" };
      if (executionEligible) {
        try {
          draftRecovery = reconcilePendingSubmission(window.localStorage, {
            workspace: selectedWorkspace ?? "",
            providerId: selectedProvider ?? "",
            modelIdentifier: selectedModel ?? "",
            threadId: savedThread.id,
            latestTurnId: latestTurnId(turns),
            latestUserText: latestUserText(restored),
          });
        } catch {
          draftRecovery = { status: "unresolved" };
        }
      }
      setThreadId(savedThread.id);
      taskTelemetry.reset({ workspace: selectedWorkspace ?? null, threadId: savedThread.id, providerId: selectedProvider ?? null, modelIdentifier: selectedModel ?? null });
      setOutputOverride("");
      setThreadExecutionEligible(executionEligible);
      setMessages(restored);
      setHistoryTruncated(field(result, "historyTruncated") === true);
      setActivities(restoreSavedActivities(field(result, "savedOutputs"), savedThread.id));
      setSavedOutputsTruncated(field(result, "savedOutputsTruncated") === true);
      setTurnReview(restoreSavedTurnReview(field(result, "savedReviews"), savedThread.id));
      const restoredOutcome = restoreTurnOutcome(field(result, "turns"));
      setTurnState(restoredOutcome.state);
      setRestoredTurnWarning(restoredOutcome.warning);
      setRuntime((current) => current ? { ...current, lastThreadStart: result, lastModelContextEvidence: null } : current);
      const recoveryWarning = pendingRecoveryWarning(draftRecovery.status);
      setReconnectWarning(executionEligible
        ? recoveryWarning
        : resumeBindingWarning ?? capabilityBindingWarning(field(result, "executionBlockedReason")));
      await refreshThreads();
      requestAnimationFrame(() => (executionEligible ? composerRef.current : conversationRef.current)?.focus());
    } catch (error) {
      setTurnError(error instanceof Error ? error.message : "The saved App Server thread could not be resumed.");
    } finally {
      setOpeningThreadId(null);
    }
  }

  async function forkCurrentThread() {
    if (!threadId || threadReadOnly || isBusy || compactRequestRef.current) return;
    const sourceThreadId = threadId;
    setForkingThread(true);
    setTurnError(null);
    try {
      const result = await requestHost<JsonRecord>("forkThread", { threadId: sourceThreadId });
      const forkedId = textValue(field(result, "threadId"));
      const bookmarkPersisted = field(result, "bookmarkPersisted") !== false;
      if (!forkedId || forkedId === sourceThreadId) {
        throw new Error("Codex App Server did not return a distinct forked thread; the current view was not changed.");
      }
      if (textValue(field(result, "attributedTo")) !== "Codex App Server"
        || field(result, "executionEligible") !== true
        || textValue(field(result, "modelProvider")) !== textValue(field(capability, "providerId"))
        || textValue(field(result, "model")) !== textValue(field(capability, "modelIdentifier"))) {
        throw new Error("The fork response did not match the selected App Server provider/model identity.");
      }
      const returnedWorkspace = textValue(field(result, "cwd"))?.replaceAll("/", "\\").replace(/\\+$/, "").toLowerCase();
      const expectedWorkspace = selectedWorkspace?.replaceAll("/", "\\").replace(/\\+$/, "").toLowerCase();
      if (!returnedWorkspace || returnedWorkspace !== expectedWorkspace) {
        throw new Error("The fork response did not match the selected NeoBabylon workspace.");
      }

      recordActiveTask(forkedId);
      setOutputOverride("");
      setThreadId(forkedId);
      setThreadExecutionEligible(true);
      const forkedTurns = field(result, "turns");
      latestTurnIdRef.current = latestTurnId(forkedTurns);
      setMessages(restoreVisibleTranscript(forkedTurns, forkedId));
      setHistoryTruncated(field(result, "historyTruncated") === true);
      setActivities([...restoreSavedActivities(field(result, "savedOutputs"), forkedId), {
        id: `fork-${forkedId}`,
        title: bookmarkPersisted ? "Conversation forked" : "Fork created · navigation not saved",
        detail: bookmarkPersisted
          ? `New App Server thread ${forkedId}; the original thread remains unchanged.`
          : `App Server created ${forkedId}, but NeoBabylon could not save its app-data bookmark: ${textValue(field(result, "bookmarkFailure")) ?? "storage error"}. It may not appear in saved history after restart.`,
        status: bookmarkPersisted ? "succeeded" : "info",
      }]);
      setSavedOutputsTruncated(field(result, "savedOutputsTruncated") === true);
      setTurnReview(restoreSavedTurnReview(field(result, "savedReviews"), forkedId));
      setTurnState("idle");
      setRestoredTurnWarning(null);
      setRuntime((current) => current ? { ...current, lastThreadStart: result, lastModelContextEvidence: null } : current);
      await refreshThreads();
      requestAnimationFrame(() => composerRef.current?.focus());
    } catch (error) {
      setTurnError(error instanceof Error ? error.message : "Codex App Server could not fork the current conversation.");
    } finally {
      setForkingThread(false);
    }
  }

  async function startNewTask() {
    if (isBusy || compactRequestRef.current) return;
    if (!threadId) {
      composerRef.current?.focus();
      return;
    }
    setCreatingNewTask(true);
    setTurnError(null);
    try {
      await requestHost<JsonRecord>("newTask");
      setOutputOverride("");
      forgetActiveTask();
      latestTurnIdRef.current = null;
      setThreadId(null);
      setThreadExecutionEligible(false);
      setMessages([]);
      setHistoryTruncated(false);
      setActivities([]);
      setSavedOutputsTruncated(false);
      setTurnReview(null);
      setTurnState("idle");
      setRestoredTurnWarning(null);
      setRuntime((current) => current ? { ...current, lastThreadStart: null, lastModelContextEvidence: null } : current);
      await refreshThreads();
      requestAnimationFrame(() => composerRef.current?.focus());
    } catch (error) {
      setTurnError(error instanceof Error ? error.message : "The previous task could not be safely closed.");
    } finally {
      setCreatingNewTask(false);
    }
  }

  async function submitTask() {
    const prompt = draft.trim();
    const submittedScope = draftScope;
    if (!prompt || !submittedScope || threadReadOnly || isBusy || compactRequestRef.current) return;
    const requestedEffort = parseReasoningSelection(capability, reasoningSelection);
    if (requestedEffort.error) { setTurnError(requestedEffort.error); return; }
    const requestedOutput = parseOutputOverride(outputOverride,
      completionLimit(recordValue(field(capability, "maxCompletionTokensAdvertised"))));
    if (requestedOutput.error) {
      setTurnError(requestedOutput.error);
      return;
    }
    setTurnError(null);
    setRestoredTurnWarning(null);
    setTurnState("starting");
    const optimisticMessageId = crypto.randomUUID();
    setMessages((current) => [...current, { id: optimisticMessageId, role: "user", text: prompt }]);
    setActivities([]);
    setSavedOutputsTruncated(false);
    setTurnReview(null);
    let currentThreadId = threadId;
    try {
      if (!currentThreadId) {
        const started = await requestHost<JsonRecord>("startThread");
        if (field(started, "executionEligible") !== true) throw new Error("The host did not confirm an executable App Server thread; the prompt was not sent.");
        const thread = recordValue(field(started, "thread"));
        const nestedThread = recordValue(field(thread, "thread"));
        currentThreadId = textValue(field(nestedThread, "id")) ?? textValue(field(thread, "threadId")) ?? null;
        if (!currentThreadId) throw new Error("Codex App Server did not return a thread identifier.");
        latestTurnIdRef.current = null;
        recordActiveTask(currentThreadId);
        setThreadId(currentThreadId);
        setThreadExecutionEligible(true);
        setRuntime((current) => current ? { ...current, lastThreadStart: started } : current);
      }
      const threadDraftScope = draftKey(selectedWorkspace, currentThreadId);
      const providerId = textValue(field(capability, "providerId"));
      const modelIdentifier = textValue(field(capability, "modelIdentifier"));
      const requestId = `turn-${crypto.randomUUID()}`;
      if (!selectedWorkspace || !threadDraftScope || !providerId || !modelIdentifier) {
        throw new Error("The exact project/provider/model identity could not be saved for turn recovery; the prompt was not sent.");
      }
      const pending: PendingSubmission = {
        requestId,
        key: threadDraftScope,
        sourceKey: submittedScope,
        workspace: selectedWorkspace,
        providerId,
        modelIdentifier,
        threadId: currentThreadId,
        previousTurnId: latestTurnIdRef.current,
        text: draft,
        accepted: false,
      };
      try {
        savePendingSubmission(window.localStorage, {
          workspace: pending.workspace,
          providerId: pending.providerId,
          modelIdentifier: pending.modelIdentifier,
          threadId: pending.threadId,
          requestId: pending.requestId,
          draftScope: pending.key,
          sourceDraftScope: pending.sourceKey,
          previousTurnId: pending.previousTurnId,
        });
      } catch {
        try { if (loadDraft(window.localStorage, threadDraftScope) !== draft) saveDraft(window.localStorage, threadDraftScope, draft); } catch { /* Keep the original new-task draft and report the storage failure. */ }
        setDraftView({ key: threadDraftScope, text: draft });
        setReconnectWarning("This prompt was not sent because its recovery state could not be saved. The draft remains in this window; check app storage before retrying.");
        setTurnError("The prompt was not sent because NeoBabylon could not save its recovery state.");
        setTurnState("failed");
        setMessages((current) => current.filter((message) => message.id !== optimisticMessageId));
        return;
      }
      pendingSubmissionRef.current = pending;
      try {
        saveDraft(window.localStorage, threadDraftScope, draft);
        setDraftStorageError(null);
      } catch {
        setDraftStorageError("The task-scoped draft copy could not be saved. The original new-task draft remains until Codex accepts the turn.");
        try { if (loadDraft(window.localStorage, threadDraftScope) !== draft) saveDraft(window.localStorage, threadDraftScope, draft); } catch { /* Recovery marker remains for a later history check. */ }
      }
      setDraftView({ key: threadDraftScope, text: draft });
      setTurnState("running");
      activeRequestRef.current = requestId;
      taskTelemetry.begin({ workspace: selectedWorkspace, threadId: currentThreadId, providerId, modelIdentifier }, requestId);
      const result = await requestHost<JsonRecord>("startTurn", {
        text: prompt,
        ...requestedEffort.wire,
        ...(requestedOutput.value === null ? {} : { maxOutputTokens: requestedOutput.value }),
      }, requestId);
      const completedState = resolveLiveTurnState(result);
      taskTelemetry.complete(requestId, result, completedState);
      assistantDeltaBatcherRef.current?.flush();
      setMessages((current) => finishAssistantStreams(current));
      acceptPendingSubmission(requestId);
      const eventType = textValue(field(result, "eventType"));
      const reply = textValue(field(result, "assistantText"));
      const assistantDisplay = recordValue(field(result, "assistantDisplay")) as DisplayMetadata | undefined;
      const assistantItemId = textValue(field(result, "assistantItemId"));
      const responseThreadId = textValue(field(result, "threadId")) ?? currentThreadId ?? undefined;
      const responseTurnId = textValue(field(result, "turnId")) ?? latestTurnIdRef.current ?? undefined;
      if (reply) upsertAssistant(reply, false, false, assistantItemId, assistantDisplay,
        responseTurnId, responseThreadId);
      const failure = recordValue(field(result, "failure"));
      const failed = Boolean(failure) || eventType === "turnFailure" || eventType === "turnCompletedWithToolFailure";
      if (failed) {
        const message = textValue(field(failure, "message")) ?? "The turn ended with a reported tool failure.";
        setTurnError(message);
        if (reply) upsertAssistant(reply, false, true, assistantItemId, assistantDisplay, responseTurnId, responseThreadId);
        else setMessages((current) => markAssistantTurnFailed(current, optimisticMessageId, responseTurnId, responseThreadId));
      }
      const marker = createLiveTurnStatusMessage(result, `${requestId}-status`);
      if (marker) setMessages((current) => [...current, marker]);
      setTurnState(completedState);
      const context = recordValue(field(result, "modelContextEvidence"));
      if (context) setRuntime((current) => current ? { ...current, lastModelContextEvidence: context } : current);
      const toolDiagnostics = field(result, "toolDiagnostics");
      if (Array.isArray(toolDiagnostics)) {
        for (const diagnostic of toolDiagnostics) {
          const item = recordValue(diagnostic);
          if (!item) continue;
          const rawExitCode = field(item, "exitCode");
          const exitCode = typeof rawExitCode === "number" && Number.isSafeInteger(rawExitCode) ? rawExitCode : undefined;
          addActivity({
            id: textValue(field(item, "itemId")) ?? textValue(field(item, "callId")) ?? crypto.randomUUID(),
            title: textValue(field(item, "toolName")) ?? textValue(field(item, "command")) ?? "Tool activity",
            detail: textValue(field(item, "output")) ?? textValue(field(recordValue(field(item, "failure")), "message")),
            status: field(item, "succeeded") === true ? "succeeded" : field(item, "succeeded") === false ? "failed" : "info",
            ...(exitCode === undefined ? {} : { exitCode }),
            ...(recordValue(field(item, "neoBabylonDisplay")) as DisplayMetadata | undefined),
            itemType: textValue(field(item, "itemType")),
            threadId: textValue(field(result, "threadId")) ?? threadId ?? undefined,
            turnId: textValue(field(result, "turnId")) ?? latestTurnIdRef.current ?? undefined,
          });
        }
      }
      const continuingCommands = field(result, "continuingCommands");
      if (Array.isArray(continuingCommands)) {
        for (const value of continuingCommands) {
          const command = recordValue(value);
          const itemId = textValue(field(command, "itemId"));
          const commandState = textValue(field(command, "commandStopState"));
          if (!command || !itemId || !["running", "finished", "notReportedActive", "unknown"].includes(commandState ?? "")) continue;
          addActivity({
            id: itemId,
            title: textValue(field(command, "command")) ?? "Command execution",
            status: commandState === "running" ? "running" : "info",
            itemType: "commandExecution",
            threadId: responseThreadId,
            turnId: responseTurnId,
            commandStopState: commandState as ActivityEntry["commandStopState"],
            commandStopAvailable: field(command, "commandStopAvailable") === true,
            commandStopDetail: textValue(field(command, "commandStopDetail")),
          });
        }
      }
      if (field(result, "additionalActiveCommandsMayBeOmitted") === true) {
        addActivity({
          id: `command-tracking-incomplete-${responseThreadId ?? threadId ?? "unknown"}-${responseTurnId ?? "unknown"}`,
          title: "Command tracking incomplete",
          detail: "App Server command notifications exceeded NeoBabylon’s per-turn tracking limit. Additional commands may still be active; no Stop action is offered without an exact confirmed command identity.",
          status: "info",
          itemType: "commandExecutionDiscovery",
          threadId: responseThreadId ?? threadId ?? undefined,
          turnId: responseTurnId ?? undefined,
        });
      }
      void refreshThreads();
    } catch (error) {
      if (activeRequestRef.current) taskTelemetry.abandon(activeRequestRef.current);
      assistantDeltaBatcherRef.current?.flush();
      setMessages((current) => finishAssistantStreams(current));
      const pending = pendingSubmissionRef.current;
      if (pending && !pending.accepted && currentThreadId) {
        const newScope = draftKey(selectedWorkspace, currentThreadId);
        if (newScope && newScope !== submittedScope) {
          try {
            if (loadDraft(window.localStorage, newScope) !== pending.text) saveDraft(window.localStorage, newScope, pending.text);
          } catch {
            setDraftStorageError("The unsent draft could not be copied to its new task. The original new-task draft is retained; copy it before closing this window if storage is unavailable.");
          }
          setDraftView({ key: newScope, text: pending.text });
        }
      }
      const message = error instanceof Error ? error.message : "The request failed before a verified response arrived.";
      setTurnError(message);
      setTurnState("failed");
      setMessages((current) => [...current, { id: crypto.randomUUID(), role: "assistant", text: message, failed: true }]);
    } finally {
      assistantDeltaBatcherRef.current?.clear();
      activeRequestRef.current = null;
      pendingSubmissionRef.current = null;
      requestAnimationFrame(() => composerRef.current?.focus());
    }
  }

  async function respondToApproval(request: ApprovalRequest, decision: string) {
    setResolvingApprovalId(request.requestId);
    setApprovalError(null);
    try {
      const reviewFingerprint = request.method === "item/fileChange/requestApproval" && decision === "accept"
        ? textValue(field(request.reviewPreview, "fingerprint"))
        : undefined;
      if (request.method === "item/fileChange/requestApproval" && decision === "accept" && !reviewFingerprint) {
        throw new Error("The exact file-change preview is unavailable; approval was not sent.");
      }
      await requestHost<JsonRecord>("respondToApproval", {
        approvalRequestId: request.requestId,
        approvalInstanceId: request.approvalInstanceId,
        decision,
        ...(reviewFingerprint ? { reviewFingerprint } : {}),
      });
      setApprovalRequests((current) => current.filter((pending) => pending.requestId !== request.requestId));
      addActivity({
        id: `approval-${request.requestId}`,
        title: decision === "accept" || decision === "grantRequestedForTurn" ? "Approval granted" : "Approval denied",
        detail: decision === "grantRequestedForTurn"
          ? "The requested permission profile was granted for this turn only."
          : decision === "accept" ? "This request was approved once; no session-level policy was added."
            : decision === "cancel" ? "The request was denied and the turn was asked to stop."
              : "The request was denied.",
        status: "info",
      });
    } catch (error) {
      setApprovalError({
        requestId: request.requestId,
        message: error instanceof Error ? error.message : "The host could not deliver this approval decision.",
      });
    } finally {
      setResolvingApprovalId(null);
    }
  }

  function upsertAssistant(
    text: string,
    streaming: boolean,
    failed = false,
    id?: string,
    displayMetadata?: DisplayMetadata,
    turnId?: string,
    targetThreadId?: string,
  ) {
    setMessages((current) => upsertAssistantMessage(
      current, text, streaming, failed, id, displayMetadata, turnId, targetThreadId ?? threadId ?? undefined));
  }

  function addActivity(activity: ActivityEntry) {
    const projectedDetail = projectDisplayText(activity.detail ?? "", 40_000, activity);
    const boundedActivity: ActivityEntry = {
      ...activity,
      detail: projectedDetail.text,
      ...(projectedDetail.displayTruncated ? {
        displayTruncated: true,
        omittedCharacters: projectedDetail.omittedCharacters,
        sourceRetained: projectedDetail.sourceRetained,
        upstreamTruncated: projectedDetail.upstreamTruncated,
      } : {}),
    };
    setActivities((current) => {
      const index = current.findIndex((item) => item.id === boundedActivity.id);
      return index < 0 ? [...current, boundedActivity] : current.map((item, itemIndex) => itemIndex === index ? boundedActivity : item);
    });
  }

  function updateCommandActivity(itemId: string, update: Partial<ActivityEntry>) {
    setActivityState((current) => ({
      ...current,
      entries: current.entries.map((entry) => entry.id === itemId ? { ...entry, ...update } : entry),
    }));
  }

  async function loadOutputRange(location: OutputInspectionLocation, offset: number) {
    const requestId = ++outputInspectionRequestRef.current;
    setOutputInspectionLoading(true);
    setOutputInspectionError(null);
    try {
      const result = await requestHost<JsonRecord>("readOutputRange", {
        threadId: location.threadId,
        turnId: location.turnId,
        itemId: location.itemId,
        offset,
      });
      if (requestId !== outputInspectionRequestRef.current) return;
      setOutputInspection(location);
      setOutputInspectionPage({
        itemType: textValue(field(result, "itemType")) ?? location.itemType,
        offset: Number(field(result, "offset")) || 0,
        totalCharacters: Number(field(result, "totalCharacters")) || 0,
        text: textValue(field(result, "text")) ?? "",
        nextOffset: Number(field(result, "nextOffset")) || 0,
        hasMore: field(result, "hasMore") === true,
        upstreamTruncated: field(result, "upstreamTruncated") === true,
      });
    } catch (error) {
      if (requestId === outputInspectionRequestRef.current) {
        setOutputInspectionError(error instanceof Error ? error.message : "The saved output range could not be read.");
      }
    } finally {
      if (requestId === outputInspectionRequestRef.current) setOutputInspectionLoading(false);
    }
  }

  function inspectOutput(location: OutputInspectionLocation) {
    outputInspectionOpenerRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setOutputInspection(location);
    setOutputInspectionPage(null);
    void loadOutputRange(location, 0);
  }

  function closeOutputInspection() {
    outputInspectionRequestRef.current += 1;
    setOutputInspection(null);
    setOutputInspectionPage(null);
    setOutputInspectionError(null);
    const opener = outputInspectionOpenerRef.current;
    requestAnimationFrame(() => {
      if (opener?.isConnected && !opener.closest("[inert]")) opener.focus();
      else if (!conversationRef.current?.closest("[inert]")) conversationRef.current?.focus();
    });
  }

  function handleNotification(event: HostNotification) {
    taskTelemetry.notification(event);
    streamEventCountRef.current += 1;
    const method = event.method;
    const params = recordValue(event.params) ?? {};
    if (method === "turn/completed") {
      const completedTurn = recordValue(field(params, "turn"));
      const completedTurnId = textValue(field(completedTurn, "id"));
      const completedThreadId = textValue(field(params, "threadId"));
      if (completedTurnId && completedThreadId) {
        setApprovalRequests((current) => current.filter((request) =>
          textValue(field(request.params, "threadId")) !== completedThreadId
          || textValue(field(request.params, "turnId")) !== completedTurnId));
      }
    }
    if (method === "turn/started" || method === "turn/diff/updated") {
      const expectedThreadId = pendingSubmissionRef.current?.threadId;
      setTurnReview((current) => reduceTurnReview(current, method, params, expectedThreadId));
    }
    if (method !== "item/agentMessage/delta" && method !== "item/assistantMessage/delta") {
      setStreamEventCount(streamEventCountRef.current);
    }
    if (method === "turn/started" && textValue(field(params, "threadId")) === pendingSubmissionRef.current?.threadId) {
      const startedTurn = recordValue(field(params, "turn"));
      latestTurnIdRef.current = textValue(field(startedTurn, "id"))
        ?? textValue(field(params, "turnId"))
        ?? latestTurnIdRef.current;
      acceptPendingSubmission(event.requestId);
    }
    if (method === "neobabylon/approvalRequested") {
      const requestId = field(params, "requestId");
      const approvalInstanceId = textValue(field(params, "approvalInstanceId"));
      const approvalMethod = textValue(field(params, "method"));
      const approvalParams = recordValue(field(params, "params"));
      const reviewPreview = recordValue(field(params, "reviewPreview"));
      const reviewInvalidated = field(params, "reviewInvalidated") === true;
      if (typeof requestId === "number" && Number.isSafeInteger(requestId) && approvalInstanceId && approvalMethod && approvalParams) {
        setApprovalRequests((current) => current.some((request) => request.requestId === requestId
          && request.approvalInstanceId === approvalInstanceId)
          ? current
          : [...current, {
            requestId,
            approvalInstanceId,
            method: approvalMethod,
            params: approvalParams,
            ...(reviewPreview ? { reviewPreview } : {}),
            ...(reviewInvalidated ? { reviewInvalidated: true } : {}),
          }]);
      }
      return;
    }
    if (method === "neobabylon/approvalReviewInvalidated") {
      const requestId = field(params, "requestId");
      const approvalInstanceId = textValue(field(params, "approvalInstanceId"));
      const previewFingerprint = textValue(field(params, "previewFingerprint"));
      if (typeof requestId === "number" && Number.isSafeInteger(requestId) && approvalInstanceId && previewFingerprint) {
        setApprovalRequests((current) => current.map((request) => invalidateApprovalReview(request, params)));
      }
      return;
    }
    if (method === "serverRequest/resolved") {
      const requestId = field(params, "requestId");
      if (typeof requestId === "number" && Number.isSafeInteger(requestId)) {
        setApprovalRequests((current) => current.filter((request) => request.requestId !== requestId));
      }
      return;
    }
    if (method === "neobabylon/approvalTimedOut") {
      const requestId = field(params, "requestId");
      if (typeof requestId === "number" && Number.isSafeInteger(requestId)) {
        setApprovalRequests((current) => current.filter((request) => request.requestId !== requestId));
      }
      addActivity({
        id: `approval-timeout-${String(requestId ?? crypto.randomUUID())}`,
        title: "Approval timed out · denied",
        detail: "No choice was made before the deadline; App Server received the schema-valid fail-closed response.",
        status: "failed",
      });
      return;
    }
    if (method === "neobabylon/approvalTimeoutFailed") {
      const requestId = field(params, "requestId");
      if (typeof requestId === "number" && Number.isSafeInteger(requestId)) {
        setApprovalRequests((current) => current.filter((request) => request.requestId !== requestId));
      }
      addActivity({
        id: `approval-timeout-failed-${String(requestId ?? crypto.randomUUID())}`,
        title: "Approval expired · App Server stopped",
        detail: "The host could not deliver the fail-closed response and terminated the App Server process; no approval was sent.",
        status: "failed",
      });
      return;
    }
    if (method === "neobabylon/serverRequestDenied") {
      const deniedMethod = textValue(field(params, "method")) ?? "unknown authority request";
      const deniedReason = textValue(field(params, "reason")) ?? "no approved handler exists";
      const requestId = field(params, "id");
      if (typeof requestId === "number" && Number.isSafeInteger(requestId)) {
        setApprovalRequests((current) => current.filter((request) => request.requestId !== requestId));
      }
      addActivity({
        id: `denied-${String(requestId ?? crypto.randomUUID())}`,
        title: "Unsupported request denied",
        detail: `${deniedMethod} · ${deniedReason}`,
        status: "failed",
      });
      return;
    }
    if (method === "neobabylon/outputTruncated") {
      const itemId = textValue(field(params, "itemId"));
      const displayMetadata = recordValue(field(params, "neoBabylonDisplay")) as DisplayMetadata | undefined;
      if (itemId && displayMetadata) {
        setMessages((current) => current.map((message) => message.id === itemId && message.role === "assistant"
          ? { ...message, ...displayMetadata,
            turnId: textValue(field(params, "turnId")) ?? message.turnId ?? latestTurnIdRef.current ?? undefined,
            threadId: textValue(field(params, "threadId")) ?? message.threadId ?? threadId ?? undefined }
          : message));
      }
      return;
    }
    const item = recordValue(field(params, "item"));
    const itemType = textValue(field(item, "type"));
    const itemId = textValue(field(item, "id")) ?? textValue(field(params, "itemId")) ?? method;
    const notificationThreadId = textValue(field(params, "threadId"));
    const notificationTurnId = textValue(field(params, "turnId"));
    if (method === "item/started" && itemType === "commandExecution"
        && typeof event.requestId === "string" && notificationThreadId && notificationTurnId) {
      lateCommandBindingsRef.current.set(
        commandIdentityKey(notificationThreadId, notificationTurnId, itemId),
        { requestId: event.requestId, threadId: notificationThreadId, turnId: notificationTurnId, itemId },
      );
    }
    if (method === "item/completed" && itemType === "commandExecution"
        && notificationThreadId && notificationTurnId) {
      lateCommandBindingsRef.current.delete(commandIdentityKey(notificationThreadId, notificationTurnId, itemId));
    }
    if (method === "item/agentMessage/delta" || method === "item/assistantMessage/delta") {
      const delta = textValue(field(params, "delta"));
      if (delta) {
        const id = textValue(field(params, "itemId")) ?? "assistant-current";
        const displayMetadata = recordValue(field(params, "neoBabylonDisplay")) as DisplayMetadata | undefined;
        assistantDeltaBatcherRef.current?.push(id, delta, displayMetadata);
      }
      return;
    }
    if (method === "item/started" && itemType && ["commandExecution", "fileChange", "mcpToolCall", "dynamicToolCall"].includes(itemType)) {
      addActivity({ id: itemId, title: textValue(field(item, "command")) ?? textValue(field(item, "tool")) ?? `${itemType} started`, status: "running" });
      return;
    }
    if (method === "item/completed" && item) {
      if (itemType === "agentMessage") {
        const body = textValue(field(item, "text"));
        const displayMetadata = recordValue(field(item, "neoBabylonDisplay")) as DisplayMetadata | undefined;
        const turnId = textValue(field(params, "turnId")) ?? latestTurnIdRef.current ?? undefined;
        if (body) {
          assistantDeltaBatcherRef.current?.discard(itemId);
          upsertAssistant(body, false, false, itemId, displayMetadata, turnId,
            textValue(field(params, "threadId")) ?? threadId ?? undefined);
        } else assistantDeltaBatcherRef.current?.flush();
      } else if (["commandExecution", "fileChange", "mcpToolCall", "dynamicToolCall", "functionCallOutput"].includes(itemType ?? "")) {
        const status = textValue(field(item, "status"));
        const rawFailure = field(item, "error");
        const rawExitCode = field(item, "exitCode");
        const exitCode = typeof rawExitCode === "number" && Number.isSafeInteger(rawExitCode) ? rawExitCode : undefined;
        const failure = textValue(rawFailure);
        const displayMetadata = recordValue(field(item, "neoBabylonDisplay")) as DisplayMetadata | undefined;
        addActivity({
          id: itemId,
          title: textValue(field(item, "command")) ?? itemType ?? "Tool activity",
          detail: failure ?? textValue(field(item, "aggregatedOutput")) ?? textValue(field(item, "output")),
          status: classifyActivityOutcome(status, rawFailure),
          ...(exitCode === undefined ? {} : { exitCode }),
          ...displayMetadata,
          itemType,
          threadId: textValue(field(params, "threadId")) ?? threadId ?? undefined,
          turnId: textValue(field(params, "turnId")) ?? latestTurnIdRef.current ?? undefined,
        });
      }
    }
  }

  async function interruptTurn() {
    try { await requestHost<JsonRecord>("interruptTurn"); }
    catch (error) { setTurnError(error instanceof Error ? error.message : "The runtime did not accept the interrupt request."); }
  }

  async function compactCurrentContext() {
    const state = { threadId, busy: isBusy, executionEligible: threadExecutionEligible };
    if (!canCompactContext(state) || compactRequestRef.current || !threadId) return;
    const token = taskTelemetry.identityToken();
    const captured = { requestId: `compact-${crypto.randomUUID()}`, threadId, identityKey: token.key, epoch: token.epoch };
    compactRequestRef.current = captured;
    taskTelemetry.beginCompaction(captured.requestId);
    setCompactingContext(true);
    setShowModelMenu(false);
    setCompactFeedback({ status: "pending", message: "NeoBabylon.UI: Asking Codex to compact context. Draft and history remain unchanged; waiting for final confirmation." });
    let confirmed = false;
    let confirmedTurnId: string | null = null;
    try {
      const result = await requestContextCompaction((operation, payload) => requestHost<JsonRecord>(operation, payload, captured.requestId), state);
      if (compactRequestRef.current !== captured || !taskTelemetry.matchesIdentity(captured.identityKey, captured.epoch)) return;
      if (result.status !== "blocked") {
        confirmed = result.status === "completed";
        confirmedTurnId = typeof result.turnId === "string" ? result.turnId : null;
        setCompactFeedback({ status: result.status, message: result.message });
      }
    } catch (error) {
      if (compactRequestRef.current === captured && taskTelemetry.matchesIdentity(captured.identityKey, captured.epoch)) setCompactFeedback({ status: "unknown", message: error instanceof Error ? error.message : "NeoBabylon.UI: Compaction failed without a confirmed outcome." });
    } finally {
      if (compactRequestRef.current === captured) {
        // Retain lifetime spend, but never refill context from an identical old journal observation.
        if (taskTelemetry.matchesIdentity(captured.identityKey, captured.epoch)) {
          taskTelemetry.finishCompaction(captured.requestId, confirmed, confirmedTurnId);
          void taskTelemetry.refresh();
        }
        compactRequestRef.current = null;
        setCompactingContext(false);
      }
    }
  }

  async function stopCommand(activity: ActivityEntry) {
    updateCommandActivity(activity.id, {
      commandStopState: "stopping",
      commandStopDetail: "Asking Codex App Server to stop this exact command…",
    });
    const outcome = await requestCommandStop(activity.id, requestHost);
    updateCommandActivity(activity.id, {
      commandStopState: outcome.state,
      commandStopAvailable: outcome.actionAvailable,
      commandStopDetail: outcome.detail,
      commandStopAttribution: outcome.attributedTo,
      status: outcome.state === "running" ? "running" : "info",
    });
  }

  const sessionContextValue = taskTelemetry.snapshot.currentUsage?.modelContextWindow;
  const threadTitle = useMemo(() => {
    const currentThread = threadId ? savedThreads.find((thread) => thread.id === threadId) : undefined;
    const savedName = threadId ? threadNamesById[threadId] ?? currentThread?.name : undefined;
    if (savedName) return savedName;
    const firstUser = messages.find((message) => message.role === "user")?.text;
    return firstUser ? firstUser.slice(0, 54) + (firstUser.length > 54 ? "…" : "") : "New task";
  }, [messages, savedThreads, threadId, threadNamesById]);
  const visibleThreads = useMemo(() => filterSavedThreads(savedThreads, threadSearch), [savedThreads, threadSearch]);
  const savedHistoryView = savedHistoryState({
    unavailable: Boolean(threadListError),
    query: threadSearch,
    resultCount: visibleThreads.length,
  });

  return <CommandStopActionContext.Provider value={stopCommand}><div className="app-shell">
    <aside className="sidebar" aria-label="Workspace navigation" inert={diagnosticsOpen || unapprovedToolsOpen || reviewOpen || Boolean(outputInspection)}>
      <div className="brand-lockup"><img className="brand-symbol" src="/favicon.svg" alt="" aria-hidden="true" /><div className="brand-copy"><strong>neobabylon</strong><span>DESKTOP AGENT WORKSPACE</span></div><IconButton className="icon-button sidebar-search" label="Search conversations" tooltip="Search conversations" variant="ghost" icon={<Icon name="search" />} onClick={() => threadSearchRef.current?.focus()} /></div>
      <Button className="new-task-button" label={creatingNewTask ? "Saving task…" : "New task"} variant="secondary" icon={<Icon name="plus" size={17} />} onClick={() => void startNewTask()} isDisabled={isBusy} tooltip={threadId ? "Close this view and preserve the saved App Server task" : "Start a task"} />
      <nav className="nav-section" aria-label="Workspace sections"><div className="section-label">WORKSPACE</div><div className="nav-item active" aria-current="page"><Icon name="folder" size={17} /><span>Projects</span></div><button type="button" className="nav-item" onClick={() => void openUnapprovedTools()}><Icon name="shield" size={17} /><span>Unapproved tools</span></button><button type="button" className="nav-item" onClick={() => setDiagnosticsOpen(true)}><Icon name="pulse" size={17} /><span>Activity & diagnostics</span></button></nav>
      <div className="project-list" role="region" aria-label="Projects"><div className="section-heading"><span>Projects</span><button type="button" className="subtle-icon" aria-label="Add project" title="Choose a local project folder" disabled={isBusy} onClick={() => void changeProject("addProject")}><Icon name="plus" size={15} /></button></div><div className="project-entries">{projects.map((project, index) => <div className="project-entry" key={project.workspacePath}><button type="button" className={`project-row ${project.workspacePath.toLowerCase() === selectedWorkspace?.toLowerCase() ? "selected" : ""}`} onClick={() => { if (project.available && !isBusy) void changeProject("selectProject", project.workspacePath); }} disabled={isBusy} aria-disabled={!project.available || isBusy} aria-describedby={`project-path-${index}`} aria-current={project.workspacePath.toLowerCase() === selectedWorkspace?.toLowerCase() ? "page" : undefined} title={project.available ? project.workspacePath : `Folder unavailable: ${project.workspacePath}`}><span className="project-dot" /><span className="project-name">{project.name}</span><Icon name="chevronRight" size={15} /></button><div id={`project-path-${index}`} className="project-path" title={project.workspacePath}>{shortPath(project.workspacePath)}{!project.available ? " · missing" : ""}</div></div>)}</div>{projectError && <div className="project-error" role="alert">{projectError}</div>}</div>
      <div className="chat-list" role="region" aria-label={`Saved conversations, ${savedThreads.length} loaded`}>
        <div className="section-heading"><span>Chats</span><span className="live-count" aria-label={`${savedThreads.length} loaded`}>{savedThreads.length}</span></div>
        {!threadListError && <label className="history-search"><Icon name="search" size={15} /><input ref={threadSearchRef} type="search" aria-label="Search loaded conversations" placeholder="Search loaded conversations" value={threadSearch} onChange={(event) => setThreadSearch(event.target.value)} /></label>}
        {!threadListError && threadListWarning && <div className="history-warning" role="status">{threadListWarning}</div>}
        {savedHistoryView === "unavailable" ? <div className="empty-chats chat-list-error" role="alert" title={threadListError ?? undefined}>Saved history is unavailable.</div>
          : savedHistoryView === "search-empty" ? <div className="empty-chats" role="status">{savedThreadSearchEmptyMessage(threadSearch, historyNextCursor !== null)}</div>
            : savedHistoryView === "empty" ? <div className="empty-chats">Your conversations will appear here.</div>
              : <div className="saved-thread-list">{visibleThreads.map((thread) => {
                const displayName = threadNamesById[thread.id] ?? thread.name ?? (thread.preview || "Untitled task");
                const menuOpen = threadContextMenu?.threadId === thread.id;
                return <div className="saved-thread-entry" key={thread.id}>
                  <button
                    type="button"
                    className={`chat-row saved-chat ${thread.id === threadId ? "current-chat" : ""}`}
                    onClick={() => void openSavedThread(thread)}
                    onContextMenu={(event) => {
                      const opener = event.currentTarget;
                      const anchor = opener.parentElement!;
                      handleThreadContextMenuEvent(event.nativeEvent, ({ clientX, clientY }) =>
                        openThreadContextMenu(thread.id, anchor, opener, clientX, clientY));
                    }}
                    onKeyDown={(event) => {
                      const opener = event.currentTarget;
                      const anchor = opener.parentElement!;
                      handleThreadContextMenuEvent(event.nativeEvent, ({ clientX, clientY }) =>
                        openThreadContextMenu(thread.id, anchor, opener, clientX, clientY));
                    }}
                    disabled={isBusy}
                    aria-keyshortcuts="ContextMenu Shift+F10"
                    aria-haspopup="menu"
                    aria-expanded={menuOpen}
                    title={`${displayName}\n${thread.forkedFromId ? `Branch of ${thread.forkedFromId}` : "Conversation"}\n${thread.modelProvider} · ${thread.model}`}
                    aria-current={thread.id === threadId ? "page" : undefined}
                  >
                    <Icon name={thread.forkedFromId ? "fork" : "chat"} size={15} />
                    <span className="chat-row-copy"><span>{displayName}</span><small>{thread.forkedFromId ? "BRANCH · " : ""}{thread.modelProvider} · {thread.model}</small></span>
                    {openingThreadId === thread.id ? <span className="thread-loading-dot" /> : thread.id === threadId ? <span className="chat-live-dot" /> : null}
                  </button>
                  <button
                    type="button"
                    className="chat-overflow"
                    aria-label={`Conversation options: ${displayName}`}
                    aria-haspopup="menu"
                    aria-expanded={menuOpen}
                    disabled={isBusy}
                    onClick={(event) => {
                      const anchor = event.currentTarget.parentElement!;
                      const bounds = anchor.getBoundingClientRect();
                      openThreadContextMenu(thread.id, anchor, event.currentTarget, bounds.right - 8, bounds.top + bounds.height / 2);
                    }}
                  ><Icon name="more" size={16} /></button>
                  {menuOpen && <div
                    className="chat-context-menu"
                    role="menu"
                    aria-label={`Conversation options for ${displayName}`}
                    style={{ left: threadContextMenu.left, top: threadContextMenu.top }}
                    onKeyDown={(event) => {
                      if (event.key !== "Escape") return;
                      event.preventDefault();
                      closeThreadContextMenu();
                    }}
                    onBlur={(event) => {
                      if (!event.currentTarget.contains(event.relatedTarget as Node | null)) closeThreadContextMenu(false);
                    }}
                  ><button ref={threadMenuItemRef} type="button" role="menuitem" onClick={() => beginRenameThread(thread)}>Rename</button></div>}
                </div>;
              })}</div>}
        {!threadListError && historyNextCursor && <button className="history-load-more" onClick={() => void loadOlderThreads()} disabled={isBusy}>{loadingOlderThreads ? "Loading older conversations…" : "Load older conversations"}</button>}
      </div>
      <div className="sidebar-footer"><div className="connection-status"><span className={`connection-dot ${startupError ? "offline" : "online"}`} /><span>{startupError ? "Host unavailable" : "Desktop host ready"}</span><span className="footer-version">{appServerVersion}</span></div><button className="nav-item footer-settings" onClick={() => setDiagnosticsOpen(true)}><Icon name="settings" size={17} /><span>Runtime details</span></button><div className="user-badge"><div className="avatar">M</div><div><strong>Local session</strong><span>App-isolated data</span></div><Icon name="chevron" size={15} /></div></div>
    </aside>

    <main className="main-workspace" data-thread-read-only={threadReadOnly ? "true" : undefined} inert={diagnosticsOpen || unapprovedToolsOpen || reviewOpen || Boolean(outputInspection)}>
      <header className="topbar">
        <div className="breadcrumb"><span>Projects</span><Icon name="chevronRight" size={14} /><strong>{selectedProjectName}</strong><Icon name="chevronRight" size={14} /><span className="breadcrumb-current">{threadTitle}</span></div>
        <div className="topbar-actions">
          <span className="runtime-pill"><span className="runtime-pill-dot" />Pinned App Server {appServerVersion}</span>
          <IconButton className="icon-button appearance-toggle" label={`Switch to ${appearance === "dark" ? "light" : "dark"} mode`} tooltip={`Switch to ${appearance === "dark" ? "light" : "dark"} mode`} variant="ghost" icon={<Icon name={appearance === "dark" ? "sun" : "moon"} size={17} />} onClick={toggleAppearance} />
          {threadId && <IconButton className="icon-button" label="Fork conversation" tooltip="Create a new task branch; keep the original" variant="ghost" icon={<Icon name="fork" size={17} />} onClick={() => void forkCurrentThread()} isDisabled={isBusy || threadReadOnly} />}
          <IconButton className={`icon-button details-toggle ${showDetails ? "pressed" : ""}`} label={showDetails ? "Hide task details" : "Show task details"} tooltip={showDetails ? "Hide task details" : "Show task details"} variant="ghost" icon={<Icon name="info" size={17} />} onClick={() => setShowDetails((value) => !value)} />
          <IconButton className="icon-button" label="Open diagnostics" tooltip="Runtime diagnostics" variant="ghost" icon={<Icon name="settings" size={17} />} onClick={() => setDiagnosticsOpen(true)} />
        </div>
      </header>
      <div className="content-grid">
        <section ref={conversationRef} tabIndex={-1} className="conversation" aria-label="Conversation" data-stream-event-count={streamEventCount} data-turn-state={turnState}>
          {recordRecoveryBlocked && <section className="protected-record-startup-notice" role="alert" aria-labelledby="protected-record-startup-title">
            <div><strong id="protected-record-startup-title">Application record recovery required</strong>
              <p>Host recovery code: {recordRecoveryStatus}{recordRecoveryKey ? ` · record ${recordRecoveryKey}` : ""}. projectRegistryResolved={String(field(runtime, "projectRegistryResolved"))}; executionBlocked={String(field(runtime, "executionBlocked"))}.</p>
              <p>{recordRecoveryReason ?? "The protected application record has not been reported resolved."} Inference and project-record writes are paused. Existing future-version or corrupt records remain preserved; this UI will not reset, replace, or overwrite them.</p>
              {recordRecoveryRefreshMessage && <p className="protected-record-startup-refresh" role="status">{recordRecoveryRefreshMessage}</p>}
            </div>
            <div className="protected-record-startup-actions">
              <button type="button" onClick={() => { setProtectedRecordRecoveryOpen(true); setDiagnosticsOpen(true); }}>Open record recovery</button>
              <button type="button" disabled={refreshingRecordRecovery} onClick={() => void refreshProtectedRecordRecoveryStatus().then(setRecordRecoveryRefreshMessage)}>{refreshingRecordRecovery ? "Refreshing host status…" : "Refresh host status"}</button>
            </div>
          </section>}
          {historyTruncated && <div className="history-note" role="note">Some earlier or longer messages are omitted from this preview. Full history remains in Codex App Server.</div>}
          {savedOutputsTruncated && <div className="history-note" role="note">Showing the 64 newest saved tool outputs. Older output history remains in Codex App Server.</div>}
          {activitiesTruncated && <div className="history-note" role="note">Older activity cards or text are omitted from this local preview to keep long sessions responsive. Saved tool output may still be available in task history.</div>}
          <div className={`conversation-scroll ${messages.length ? "has-messages" : ""}`}>
          {messages.length === 0 ? <div className="welcome-wrap"><div className="welcome-mark"><Icon name="spark" size={28} /></div><div className="eyebrow"><span className="eyebrow-line" />A THOUGHTFUL WAY TO BUILD<span className="eyebrow-line" /></div><h1>What are we<br /><span>working on today?</span></h1><p className="welcome-copy">A calm, capable workspace for the ideas you want to bring to life.</p><div className="welcome-context"><div className="context-icon"><Icon name="folder" size={15} /></div><div><span>Working in</span><strong>{shortPath(selectedWorkspace)}</strong></div><span className="context-separator" /><div className="context-ready"><span />App data isolated</div></div></div> : <div className="transcript"><div className="conversation-date"><span />TODAY<span /></div>{messages.map((message) => message.role === "status" ? <div className={message.transcriptWindowOmission ? "history-note" : "turn-status-line"} role={message.transcriptWindowOmission ? "note" : "status"} key={message.id}>{!message.transcriptWindowOmission && <Icon name="alert" size={14} />}<span>{message.text}</span></div> : <article className={`message message-${message.role} ${message.failed ? "message-failed" : ""}`} key={message.id}><div className={`message-avatar ${message.role === "assistant" ? "assistant-avatar" : "user-avatar"}`}>{message.role === "assistant" ? <Icon name="spark" size={15} /> : "M"}</div><div className="message-body"><div className="message-meta"><strong>{message.role === "assistant" ? "NeoBabylon" : "You"}</strong>{message.streaming && <span className="typing-indicator"><i /><i /><i /> working</span>}</div><div className="message-text">{message.text}{message.streaming && <span className="stream-cursor" />}</div>{(message.displayTruncated || message.upstreamTruncated) && <div className="output-truncation-note" role="note"><span>{message.displayTruncated ? `Showing a bounded preview; ${message.omittedCharacters ?? "some"} characters are omitted.` : "App Server indicates that source output was already truncated."}</span>{message.sourceRetained && message.threadId && message.turnId ? <button type="button" onClick={() => inspectOutput({ threadId: message.threadId!, turnId: message.turnId!, itemId: message.id, itemType: "agentMessage", title: "Assistant response" })}>Inspect saved output</button> : <span>The partial output was not saved by App Server and cannot be restored.</span>}{message.upstreamTruncated && <span>App Server reports an upstream omission; source bytes omitted before persistence are unavailable.</span>}</div>}</div></article>)}{activities.length > 0 && <div className="activity-stack" aria-label="Agent activity">{activities.map((activity) => <ActivityCard activity={activity} key={activity.id} onInspect={inspectOutput} />)}</div>}{turnReview && turnReview.status !== "awaiting" && turnReview.threadId === threadId && <div className="review-activity"><span><Icon name="folder" size={15} /> App Server tracked changes</span><button type="button" onClick={(event) => { reviewOpenerRef.current = event.currentTarget; setReviewOpen(true); }}>Review changes</button></div>}{approvalRequests.length > 0 && <div className="approval-stack" role="region" aria-label="Pending App Server approvals" aria-live="polite">{approvalRequests.map((request) => <ApprovalCard key={request.requestId} request={request} isResolving={resolvingApprovalId !== null} error={approvalError?.requestId === request.requestId ? approvalError.message : undefined} onDecision={(decision) => void respondToApproval(request, decision)} />)}</div>}{turnState === "running" && !messages.some((message) => message.streaming) && approvalRequests.length === 0 && <div className="assistant-thinking"><span className="thinking-mark"><Icon name="spark" size={15} /></span><span className="thinking-dots"><i /><i /><i /></span><span>Thinking through your request</span></div>}{turnError && <div className="turn-error" role="alert"><Icon name="alert" size={16} /><span>{turnError}</span></div>}</div>}
            {turnError && messages.length === 0 && <div className="turn-error empty-state-error" role="alert"><Icon name="alert" size={16} /><span>{turnError}</span></div>}
            {startupError && <div className="startup-error" role="alert"><Icon name="alert" size={16} /><div><strong>Desktop host not connected</strong><span>{startupError}</span></div></div>}
          </div>
          {!fullAccessNoticeDismissed && <div className="execution-warning" role="note" aria-label="Full-access notice"><Icon name="alert" size={14} /><div className="execution-warning-body"><span><strong>{executionPolicyView.label}</strong> — {executionPolicyView.warning}</span>{fullAccessNoticeStorageError && <span className="execution-warning-error">Couldn’t save this preference. You can still dismiss the notice for this session.</span>}</div><div className="execution-warning-actions"><button type="button" onClick={() => { if (hideFullAccessNotice()) setFullAccessNoticeDismissed(true); else setFullAccessNoticeStorageError(true); }}>Don’t show again</button><button type="button" aria-label="Dismiss full-access notice" onClick={() => setFullAccessNoticeDismissed(true)}><Icon name="close" size={13} /></button></div></div>}
          {reconnectWarning && <div className="reconnect-warning" role={threadReadOnly ? "note" : "alert"}><Icon name="alert" size={14} /><span>{reconnectWarning}</span></div>}
          {restoredTurnWarning && <div className="reconnect-warning" role="alert"><Icon name="alert" size={14} /><span>{restoredTurnWarning}</span></div>}
          <fieldset className="composer-fieldset" disabled={threadReadOnly}>
          <div className="composer-area">
            {draftStorageError && <div className="provider-warning" role="alert"><Icon name="alert" size={14} /><span>{draftStorageError}</span></div>}
            {!selectedWorkspaceAvailable && <div className="provider-warning" role="alert"><Icon name="alert" size={14} /><span>The selected project folder is unavailable. Choose another project before starting a task.</span></div>}
            {currentProvider === "OpenRouter" && !credentialCaptured && <div className="provider-warning" role="note"><Icon name="alert" size={14} /><span>OpenRouter credential was not provided for this launch. Requests will fail explicitly; no provider or model fallback is enabled.</span></div>}
            {capabilityDiagnosticsWarning && <div className="provider-warning" role="status"><Icon name="alert" size={14} /><span>{capabilityDiagnosticsWarning}</span></div>}
            {!showDetails && compactFeedback && <div className="provider-warning" role={compactFeedback.status === "failed" || compactFeedback.status === "unknown" ? "alert" : "status"}>{compactFeedback.message}</div>}
            <div className="composer-hint"><span id="composer-keyboard-hint">Enter sends · Shift+Enter for a new line</span></div>
            <div className={`composer ${isBusy ? "composer-busy" : ""}`}>
              <textarea ref={composerRef} value={draft} onChange={(event) => updateDraft(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) { event.preventDefault(); void submitTask(); } }} placeholder={threadId ? "Continue the conversation…" : "Describe what you’d like to work on"} aria-label="Message NeoBabylon" aria-describedby="composer-keyboard-hint" disabled={Boolean(startupError) || isBusy || !selectedWorkspaceAvailable || !draftScope} rows={2} />
              <div className="composer-toolbar">
                <div className="composer-controls">
                  <div className="model-select-wrap" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) setShowModelMenu(false); }}>
                    <button ref={modelSelectRef} type="button" className="model-select" onClick={() => setShowModelMenu((value) => !value)} aria-label={`Model for next turn: ${currentProvider} ${currentModel}`} aria-expanded={showModelMenu} aria-haspopup="listbox" aria-controls={showModelMenu ? "model-capability-list" : undefined} disabled={isBusy} title="Select the exact capability for the next turn">
                      <span className="model-glyph"><Icon name="spark" size={13} /></span><span className="model-provider">{currentProvider}</span><span className="model-divider" /><span className="model-short-name">{currentModel.split("/").at(-1)}</span><Icon name="chevron" size={14} />
                    </button>
                    {showModelMenu && <div className="model-menu">
                      <div className="model-menu-label" id="model-capability-label">VERIFIED CAPABILITY RECORDS</div>
                      <div ref={modelListRef} id="model-capability-list" className="model-option-list" role="listbox" aria-labelledby="model-capability-label" aria-activedescendant={capabilities.length ? `model-option-${activeCapabilityIndex}` : undefined} tabIndex={0} onKeyDown={handleModelMenuKeyDown}>
                        {capabilities.map((entry, index) => {
                          const entryCapability = entry.capabilityRecord;
                          const identifier = modelName(entryCapability);
                          const provider = providerName(entryCapability);
                          const selected = sameCapabilityRecord(entryCapability, capability);
                          const sameTuple = textValue(field(entryCapability, "providerId")) === currentProviderId && identifier === currentModel;
                          const active = index === activeCapabilityIndex;
                          const disabled = !canSelectCapability({ busy: isBusy }) || selected;
                          return <div id={`model-option-${index}`} className={`model-option ${selected ? "selected" : ""}`} key={`${provider}-${identifier}-${entry.sourceFile}`} role="option" aria-selected={selected} aria-disabled={disabled} data-active={active || undefined} onMouseDown={(event) => event.preventDefault()} onMouseMove={() => setActiveCapabilityIndex(index)} onClick={() => { if (!disabled) void selectModel(entry); }}>
                            <span className="model-option-mark"><Icon name={selected ? "check" : "spark"} size={14} /></span><span className="model-option-copy"><strong>{identifier}</strong><small>{provider} · {entry.sourceFile}</small>{sameTuple && !selected && <small>Different capability record · select to adopt after Host confirmation</small>}{provider === "NVIDIA NIM" && <small className="model-adapter-note">NVIDIA NIM hosted route · Responses-to-Chat-Completions adapter · model-specific tool behavior unqualified</small>}</span>{selected && <span className="selected-label">SELECTED</span>}
                          </div>;
                        })}
                      </div>
                      {!capabilities.length && <div className="model-menu-note" role="status">No verified capability records are available.</div>}
                    </div>}
                  </div>
                  <ReasoningSelector capability={capability} selection={reasoningSelection} busy={isBusy} readOnly={threadReadOnly} onChange={setReasoningSelection} />
                  <span className="control-divider" />
                  <button type="button" className="permission-chip" aria-label={`Tool authority: ${executionPolicyView.label}. Open runtime details`} title="Open runtime details for requested and effective tool authority" onClick={() => setDiagnosticsOpen(true)}><Icon name="shield" size={14} /><span>{executionPolicyView.label}</span><Icon name="chevron" size={13} /></button>
                  <div className="output-options-wrap" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) setAdvancedOptionsOpen(false); }}>
                    <button ref={outputOptionsOpenerRef} type="button" className="composer-options-trigger" aria-expanded={advancedOptionsOpen} aria-controls={advancedOptionsOpen ? "turn-options-panel" : undefined} onClick={() => setAdvancedOptionsOpen((open) => !open)}>Options</button>
                    {advancedOptionsOpen && <section id="turn-options-panel" className="output-options-panel" role="group" aria-labelledby="turn-options-title" onKeyDown={(event) => { if (event.key === "Escape") { event.preventDefault(); setAdvancedOptionsOpen(false); requestAnimationFrame(() => outputOptionsOpenerRef.current?.focus()); } }}>
                      <div className="output-options-heading"><strong id="turn-options-title">Turn options</strong><button type="button" onClick={() => { setAdvancedOptionsOpen(false); requestAnimationFrame(() => outputOptionsOpenerRef.current?.focus()); }}>Done</button></div>
                      <div className="output-cap-control">
                        <label htmlFor="max-output-tokens">Completion token cap for next turn</label>
                        <div className="output-cap-entry"><input id="max-output-tokens" type="text" inputMode="numeric" autoComplete="off" spellCheck={false} value={outputOverride} onChange={(event) => setOutputOverride(event.currentTarget.value)} aria-invalid={Boolean(outputSelection.error)} aria-describedby={outputSelection.error ? "output-cap-help output-cap-error" : "output-cap-help"} placeholder="Default" disabled={isBusy} /><button type="button" onClick={() => setOutputOverride("")} disabled={!outputOverride || isBusy}>Use default</button></div>
                        <p id="output-cap-help">{completionCap.state === "Known" ? `Known advertised maximum: ${numberFormat(completionCap.maximumTokens)} tokens. Default uses that maximum.` : completionCap.state === "Unknown" ? "Advertised maximum Unknown. Default: 32,768 tokens." : "Advertised maximum invalid; sending is blocked."} {outputOverride ? "Override applies to this turn only." : "Leave blank for the default."}</p>
                        {outputSelection.error && <p id="output-cap-error" className="output-cap-error" role="alert">{outputSelection.error}</p>}
                      </div>
                    </section>}
                  </div>
                </div>
                <div className="composer-end">{turnState === "running" ? <IconButton className="send-button stop-button" label="Stop turn" tooltip="Request interruption" variant="destructive" icon={<Icon name="stop" size={15} />} onClick={() => void interruptTurn()} /> : <IconButton className="send-button" label="Send message" tooltip="Send message" variant="primary" icon={<Icon name="arrow" size={17} />} onClick={() => void submitTask()} isDisabled={!draft.trim() || isBusy || Boolean(startupError) || !selectedWorkspaceAvailable || !draftScope || Boolean(outputSelection.error) || Boolean(requestedReasoning.error)} />}</div>
              </div>
            </div>
          </div>
          </fieldset>
        </section>

        {showDetails && <aside className="details-panel" aria-label="Task details"><div className="details-header"><div><span className="details-kicker">CONTEXT</span><h2>Task details</h2></div><button className="icon-button close-details" aria-label="Close details" onClick={() => setShowDetails(false)}><Icon name="close" size={16} /></button></div>
          <TaskTelemetryPanel telemetry={taskTelemetry.snapshot} capability={capability}
            compactAvailable={canCompactContext({ threadId, busy: isBusy, executionEligible: threadExecutionEligible })}
            compacting={compactingContext} compactFeedback={compactFeedback} onCompact={() => void compactCurrentContext()} />
          <div className="details-card model-card"><div className="card-overline"><span>SELECTED MODEL</span><span className="verified-tag"><Icon name="check" size={11} />CAPABILITY RECORD</span></div><div className="details-model-name">{currentModel}</div><div className="details-provider"><span className="provider-badge">{currentProvider.slice(0, 2).toUpperCase()}</span><span>{currentProvider}</span><span className="provider-state"><span />{capability ? "Selected" : "Unknown"}</span></div><div className="card-separator" /><DetailRow label="Route" value={routeLabel} /><DetailRow label="Endpoint" value={textValue(field(capability, "endpoint")) ?? "Unknown"} compact /><DetailRow label="Variant" value={textValue(field(capability, "modelVariant")) ?? "Unknown"} compact /></div>
          <div className="details-section">
            <div className="details-section-heading"><span>CAPABILITIES</span><button className="text-link" onClick={() => setDiagnosticsOpen(true)}>Evidence</button></div>
            <CapabilityRow label="Provider context (advertised)" value={contextTokens(providerContextAdvertised.value)} state={providerContextAdvertised.state} />
            <CapabilityRow label="Provider context (effective)" value={contextTokens(providerContextEffective.value)} state={providerContextEffective.state} />
            {typeof sessionContextValue === "number" && <CapabilityRow label="Codex task context" value={contextTokens(sessionContextValue)} state="Observed" />}
            <CapabilityRow label="Reasoning controls (advertised)" value={reasoningPolicy.controlType === "enabled" ? `On/Off · advertised default ${reasoningPolicy.defaultEnabled ? "On" : "Off"}` : displayValue(field(recordValue(reasoning.value), "supported_efforts") ?? field(recordValue(reasoning.value), "supportedEfforts") ?? reasoning.value)} state={reasoning.state} />
            <CapabilityRow label="Reasoning requested (next turn)" value={requestedReasoning.effort ?? "Provider default · no override"} state="Requested" />
            <CapabilityRow label="Reasoning observed (last reported turn)" value={taskTelemetry.snapshot.observedReasoning ?? "Unknown"} state={taskTelemetry.snapshot.observedReasoning ? "Observed" : "Unknown"} />
            {taskTelemetry.snapshot.reasoningSource && <p className="telemetry-source">Reasoning evidence: {taskTelemetry.snapshot.reasoningSource}</p>}
            <CapabilityRow label="Tool calling (advertised)" value={displayValue(toolCallingAdvertised.value)} state={toolCallingAdvertised.state} />
            <CapabilityRow label="Tool qualification (selected tuple)" value={selectedToolQualificationSummary(capability)} />
            <CapabilityRow label="Structured output (provider)" value={displayValue(structuredOutput.value)} state={structuredOutput.state} />
          </div>
          <div className="details-section execution-section"><div className="details-section-heading"><span>EXECUTION</span><span className={`turn-badge turn-${turnState}`}>{turnState === "idle" ? "READY" : turnState.toUpperCase()}</span></div><div className="execution-row"><span className="execution-icon"><Icon name="folder" size={14} /></span><div><small>Workspace</small><strong>{shortPath(selectedWorkspace)}</strong></div></div><div className="execution-row"><span className="execution-icon"><Icon name="shield" size={14} /></span><div><small>Tool authority</small><strong>{executionPolicyView.label}</strong></div></div>{threadId && <div className="execution-row"><span className="execution-icon"><Icon name="chat" size={14} /></span><div><small>Runtime thread</small><strong className="mono-id">{threadId.slice(0, 18)}…</strong></div></div>}</div>
          <div className="details-section isolation-section"><div className="details-section-heading"><span>LOCAL ISOLATION</span><Icon name="shield" size={14} /></div><div className="isolation-item"><Icon name="check" size={14} /><span>App data is separate from the source repository</span></div><div className="isolation-item"><Icon name="check" size={14} /><span>Ordinary Codex data root is not used</span></div><div className="isolation-path" title={String(dataRoot ?? "")}>{shortPath(dataRoot)}</div><div className="isolation-path" title={String(codexHome ?? "")}>CodexHome · {shortPath(codexHome)}</div></div>
          <button className="diagnostics-link" onClick={() => setDiagnosticsOpen(true)}><span className="diagnostics-icon"><Icon name="pulse" size={15} /></span><span><strong>Runtime diagnostics</strong><small>Identity, evidence & isolation</small></span><Icon name="chevronRight" size={15} /></button>
        </aside>}
      </div>
    </main>
    {diagnosticsOpen && <DiagnosticsDrawer runtime={runtime} capability={capability} providerCredentialCaptured={credentialCaptured}
      openProtectedRecordRecovery={protectedRecordRecoveryOpen} onRestoreComplete={refreshProtectedRecordRecoveryStatus}
      onClose={() => { setDiagnosticsOpen(false); setProtectedRecordRecoveryOpen(false); }} />}
    {unapprovedToolsOpen && <UnapprovedToolsDrawer candidates={unapprovedTools} loading={unapprovedToolsLoading} error={unapprovedToolsError} requestCandidateHost={requestCandidateHost} unresolvedStage={unresolvedStage} stageRecoveryNotice={stageRecoveryNotice} onStageFailure={(target, message) => { setUnresolvedStage({ target, message }); setStageRecoveryNotice(null); }} onStageSuccess={() => setStageRecoveryNotice(null)} onStageRecovered={() => { setUnresolvedStage(null); setStageRecoveryNotice("Exact candidate, review, and prepared-disabled binding identity revalidated. Previous Stage outcome remains Unknown; a repeat may be rejected if the host already staged it. No callable access is confirmed."); }} onReload={() => void loadUnapprovedTools()} onClose={closeUnapprovedTools} />}
    {reviewOpen && turnReview && <ReviewDrawer review={turnReview} returnFocusTo={reviewOpenerRef.current} onClose={() => setReviewOpen(false)} />}
    {outputInspection && <OutputInspectionDrawer
      location={outputInspection}
      page={outputInspectionPage}
      loading={outputInspectionLoading}
      error={outputInspectionError}
      onClose={closeOutputInspection}
      onLoadRange={(offset) => void loadOutputRange(outputInspection, offset)}
    />}
    {renameTarget && <div className="rename-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) closeRenameDialog(); }}>
      <section ref={renameDialogRef} tabIndex={-1} className="rename-dialog" role="dialog" aria-modal="true" aria-labelledby="rename-dialog-title" onKeyDown={(event) => handleDialogKeyDown(event, renameDialogRef.current, closeRenameDialog)}>
        <header><div><span className="details-kicker">SAVED CONVERSATION</span><h2 id="rename-dialog-title">Rename conversation</h2></div></header>
        <form onSubmit={(event) => void submitThreadRename(event)} aria-busy={renamePendingThreadId === renameTarget.threadId}>
          <label htmlFor="rename-conversation-name">Name</label>
          <input ref={renameInputRef} id="rename-conversation-name" type="text" autoComplete="off" value={renameDraft} disabled={renamePendingThreadId !== null} aria-invalid={Boolean(renameError)} aria-describedby={renameError ? "rename-name-help rename-error" : "rename-name-help"} onChange={(event) => { setRenameDraft(event.currentTarget.value); setRenameError(null); }} />
          <p id="rename-name-help">Use 1–160 characters after trimming whitespace. Control characters are not allowed.</p>
          {renameError && <p id="rename-error" className="rename-error" role="alert">{renameError}</p>}
          {renamePendingThreadId === renameTarget.threadId && <p className="rename-pending" role="status">Saving this name…</p>}
          <div className="rename-dialog-actions"><button type="button" onClick={closeRenameDialog} disabled={renamePendingThreadId !== null}>Cancel</button><button type="submit" disabled={renamePendingThreadId !== null}>Rename</button></div>
        </form>
      </section>
    </div>}
  </div></CommandStopActionContext.Provider>;
}

function ApprovalCard({ request, isResolving, error, onDecision }: {
  request: ApprovalRequest;
  isResolving: boolean;
  error?: string;
  onDecision: (decision: string) => void;
}) {
  const presentation = approvalPresentation(request);
  if (!presentation.supported) return null;
  const details = Object.entries(presentation.details).filter(([, value]) => value !== null && value !== undefined && value !== "");

  return <section className="approval-card" role="region" aria-labelledby={`approval-title-${request.requestId}`}>
    <header className="approval-header">
      <span className="approval-icon"><Icon name="shield" size={16} /></span>
      <div><h3 id={`approval-title-${request.requestId}`}>{presentation.title}</h3><p>Codex App Server is waiting for your decision.</p></div>
      <span className="approval-pending">ACTION REQUIRED</span>
    </header>
    <dl className="approval-details">
      {details.map(([name, value]) => <div className="approval-detail" key={name}>
        <dt>{name === "workingDirectory" ? "Working directory" : name === "requestedRoot" ? "Requested write root" : name}</dt>
        <dd className={name === "command" ? "approval-command" : ""}>{typeof value === "string" ? value : JSON.stringify(value, null, 2)}</dd>
      </div>)}
    </dl>
    {presentation.reviewPreview && <div className="approval-file-review" role="region" aria-label="Exact proposed file changes">
      <div className="approval-file-review-heading">Exact proposed changes · review before approving</div>
      {presentation.reviewPreview.changes.map((change, index) => <section className="approval-file-change" key={`${index}:${change.path}`}>
        <div className="approval-file-change-meta"><strong>{change.path}</strong><span>{change.kind}</span></div>
        {change.movePath && <div className="approval-file-change-move">Move to {change.movePath}</div>}
        <pre tabIndex={0} aria-label={`Proposed changes to ${change.path}`}>{change.diff}</pre>
      </section>)}
    </div>}
    {presentation.permissionProfile && <div className="approval-profile">
      <span>Requested permission profile · exact scope below</span>
      <pre tabIndex={0} aria-label="Requested permission profile">{JSON.stringify(presentation.permissionProfile, null, 2)}</pre>
    </div>}
    {presentation.warning && <p className="approval-warning" role="note">{presentation.warning} NeoBabylon will not broaden this request.</p>}
    {error && <p className="approval-error" role="alert">{error}</p>}
    <div className="approval-footer">
      <span>{isResolving ? "Sending your decision…" : "No persistent session policy is offered here."}</span>
      <div className="approval-actions">{presentation.choices.map((item) => <button
        className={`approval-button approval-${item.tone}`}
        key={item.decision}
        type="button"
        aria-label={`${item.label} for ${presentation.title}, request ${request.requestId}`}
        disabled={isResolving}
        onClick={() => onDecision(item.decision)}
      >{item.label}</button>)}</div>
    </div>
  </section>;
}

function ActivityCard({ activity, onInspect }: { activity: ActivityEntry; onInspect: (location: OutputInspectionLocation) => void }) {
  const inspectable = (activity.displayTruncated === true || activity.upstreamTruncated === true) && activity.sourceRetained === true
    && Boolean(activity.threadId && activity.turnId && activity.itemType);
  const exitCode = activity.exitCode !== undefined && Number.isSafeInteger(activity.exitCode) ? activity.exitCode : undefined;
  const exitCodeDescription = exitCode === undefined ? "" : `, exit code ${exitCode}`;
  const stopCommandAction = useContext(CommandStopActionContext);
  const commandState = activity.commandStopState;
  const stateLabel = commandState === "identityChanged" ? "identity changed"
    : commandState === "notReportedActive" ? "not reported active"
      : commandState ?? activity.status;
  const attribution = activity.commandStopAttribution ?? "Codex App Server";
  const showSpinner = commandState === "running" || commandState === "stopping"
    || (!commandState && activity.status === "running");
  return <div className={`activity-card activity-${activity.status}`} role="group" aria-label={`${activity.title}, ${stateLabel}${exitCodeDescription}, ${attribution}`}>
    <span className="activity-state-icon">{activity.status === "succeeded" ? <Icon name="check" size={13} /> : activity.status === "failed" ? <Icon name="alert" size={13} /> : showSpinner ? <span className="activity-spinner" /> : <Icon name="info" size={13} />}</span>
    <div className="activity-copy">
      <div className="activity-title">{activity.title}</div>
      {activity.detail && <pre tabIndex={0} aria-label={`${activity.title} details`}>{activity.detail}</pre>}
      {activity.commandStopDetail && <p className="activity-command-note" role="note">{activity.commandStopDetail}</p>}
      {(activity.displayTruncated || activity.upstreamTruncated) && <div className="output-truncation-note" role="note"><span>{activity.displayTruncated ? `${activity.omittedCharacters ?? "Some"} characters omitted from this preview.` : "App Server indicates that source output was already truncated."}</span>{inspectable ? <button type="button" onClick={() => onInspect({ threadId: activity.threadId!, turnId: activity.turnId!, itemId: activity.id, itemType: activity.itemType!, title: activity.title })}>Inspect saved output</button> : <span>App Server did not retain a completed item for inspection.</span>}{activity.upstreamTruncated && <span>Source bytes omitted before persistence are unavailable.</span>}</div>}
      <span className="activity-attribution">{attribution}</span>
    </div>
    <div className="activity-status-group">
      <span className="activity-status-label" role="status">{stateLabel}</span>
      {exitCode !== undefined && <span className="activity-exit-code">Exit code {exitCode}</span>}
      {mayStopCommand(activity) && stopCommandAction && <button
        type="button"
        className="activity-command-stop"
        aria-label={`${commandState === "unknown" ? "Retry stop for" : "Stop"} command ${activity.title}`}
        disabled={commandState === "stopping"}
        onClick={() => void stopCommandAction(activity)}
      >{commandState === "unknown" ? "Retry stop" : commandState === "stopping" ? "Stopping…" : "Stop command"}</button>}
    </div>
  </div>;
}

function DetailRow({ label, value, compact = false }: { label: string; value: string; compact?: boolean }) {
  return <div className={`detail-row ${compact ? "detail-row-compact" : ""}`}><span>{label}</span><strong title={value}>{value}</strong></div>;
}

function CapabilityRow({ label, value, state }: { label: string; value: string; state?: string }) {
  return <div className="capability-row"><div><span>{label}</span><strong>{value}</strong></div>{state && <span className={`observation-state ${["known", "observed"].includes(state.toLowerCase()) ? "known" : "unknown"}`}>{state}</span>}</div>;
}

function OutputInspectionDrawer({
  location,
  page,
  loading,
  error,
  onClose,
  onLoadRange,
}: {
  location: OutputInspectionLocation;
  page: OutputInspectionPage | null;
  loading: boolean;
  error: string | null;
  onClose: () => void;
  onLoadRange: (offset: number) => void;
}) {
  const dialogRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    closeButtonRef.current?.focus();
  }, []);
  const hasPrevious = Boolean(page && page.offset > 0);
  return <div className="drawer-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <section ref={dialogRef} tabIndex={-1} className="diagnostics-drawer output-inspection-drawer" role="dialog" aria-modal="true" aria-labelledby="output-inspection-title" onKeyDown={(event) => handleDialogKeyDown(event, dialogRef.current, onClose)}>
      <header className="drawer-header"><div><span className="details-kicker">CODEX APP SERVER · SAVED ITEM</span><h2 id="output-inspection-title">Inspect output</h2></div><button ref={closeButtonRef} className="icon-button" aria-label="Close output inspection" onClick={onClose}><Icon name="close" size={18} /></button></header>
      <div className="drawer-scroll output-inspection-scroll" role="region" aria-label="Saved output page" tabIndex={0}>
        <p className="review-caveat">{location.title} · exact thread, turn, and item identity · read-only pages contain at most 32,768 characters.</p>
        {loading && <p role="status">Loading saved item range…</p>}
        {error && <p className="review-unavailable" role="alert">{error}</p>}
        {page && <>
          <p className="output-page-position" role="status">Characters {page.offset.toLocaleString()}–{Math.max(page.offset, page.nextOffset - 1).toLocaleString()} of {page.totalCharacters.toLocaleString()}</p>
          {page ? (page.upstreamTruncated
            ? <p className="review-unavailable" role="note">App Server marks this saved tool output as truncated upstream. The retained head/tail is inspectable; bytes omitted before persistence are not available.</p>
            : <p className="review-unavailable" role="note">The saved item contains no recognized upstream omission marker. That is not proof the original output is complete: NeoBabylon can inspect only what App Server returned and cannot recover or count any text omitted before persistence.</p>) : null}
          <pre className="output-inspection-content" tabIndex={0} aria-label={`${location.title} saved output`}>{page.text}</pre>
        </>}
      </div>
      <footer className="drawer-footer"><span>{page ? `${page.itemType} · ${page.totalCharacters.toLocaleString()} retained characters` : "App Server-owned history"}</span><div className="output-inspection-controls"><button type="button" className="drawer-done" disabled={!hasPrevious || loading} onClick={() => onLoadRange(Math.max(0, (page?.offset ?? 0) - 32_768))}>Previous</button><button type="button" className="drawer-done" disabled={!page?.hasMore || loading} onClick={() => onLoadRange(page?.nextOffset ?? 0)}>Next</button><button type="button" className="drawer-done" onClick={onClose}>Done</button></div></footer>
    </section>
  </div>;
}

function ReviewDrawer({ review, returnFocusTo, onClose }: { review: TurnReview; returnFocusTo: HTMLButtonElement | null; onClose: () => void }) {
  const dialogRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    closeButtonRef.current?.focus();
    return () => { requestAnimationFrame(() => { if (returnFocusTo?.isConnected) returnFocusTo.focus(); }); };
  }, [returnFocusTo]);
  const saved = review.source === "savedFileChangeItems";
  const unavailable = review.status === "oversized"
    ? "The tracked diff exceeds the display limit; no partial review is shown."
    : saved ? "The saved patch item is incomplete or malformed; no partial review is shown."
      : "App Server invalidated its tracked diff. No reviewable content remains.";
  return <div className="drawer-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <section ref={dialogRef} tabIndex={-1} className="diagnostics-drawer review-drawer" role="dialog" aria-modal="true" aria-labelledby="review-title" onKeyDown={(event) => handleDialogKeyDown(event, dialogRef.current, onClose)}>
      <header className="drawer-header"><div><span className="details-kicker">CODEX APP SERVER · TURN {review.turnId.slice(0, 18)}</span><h2 id="review-title">Review changes</h2></div><button ref={closeButtonRef} className="icon-button" aria-label="Close review" onClick={onClose}><Icon name="close" size={18} /></button></header>
      <div className="drawer-scroll review-scroll" role="region" aria-label="Change review content" tabIndex={0}>
        <p className="review-caveat">Read-only · not an approval. {saved ? "These are patch item diffs restored from App Server-owned task history." : "This is the latest live App Server turn diff."} Shell-written files and other untracked changes may not be included.</p>
        {review.status === "available" && review.diff !== null
          ? <pre className="review-diff" tabIndex={0} aria-label={saved ? "Saved App Server patch item diffs" : "App Server tracked diff"}>{review.diff}</pre>
          : <p className="review-unavailable" role="status">{unavailable}</p>}
      </div>
      <footer className="drawer-footer"><span>{saved ? "Saved patch item diffs" : "App Server tracked diff"} · no tool authority granted</span><button className="drawer-done" onClick={onClose}>Done</button></footer>
    </section>
  </div>;
}

function CandidateFields({ source, entries }: { source: JsonRecord | undefined; entries: { key: string; label: string; mono?: boolean }[] }) {
  return <>{entries.map(({ key, label, mono }) => <DiagnosticField key={key} label={label} value={displayValue(field(source, key))} mono={mono} />)}</>;
}

function CandidateRecords({ title, records, entries, declaration = false, onInspectPath, inspectDisabled = false }: {
  title: string;
  records: unknown;
  entries: { key: string; label: string; mono?: boolean }[];
  declaration?: boolean;
  onInspectPath?: (path: string) => void;
  inspectDisabled?: boolean;
}) {
  const items = Array.isArray(records) ? records.map(recordValue) : [];
  return <section className="candidate-subsection"><h4>{title}{declaration ? " · unverified declarations" : ""}</h4>
    {items.length ? items.map((item, index) => {
      const path = textValue(field(item, "path"));
      return <div className="candidate-record" key={`${title}-${index}`}><span className="candidate-record-index">{title} {index + 1}</span><CandidateFields source={item} entries={entries} />{onInspectPath && path && <button type="button" className="candidate-inline-action" disabled={inspectDisabled} onClick={() => onInspectPath(path)}>Inspect {title === "Evidence" ? "evidence" : "source"} file: {path}</button>}</div>;
    }) : <p>None recorded.</p>}
  </section>;
}

function CandidateCard({ candidate, requestCandidateHost, decisionLocked, unresolvedStage, onStageFailure, onStageSuccess, onDecisionBusyChange }: {
  candidate: JsonRecord;
  requestCandidateHost: CandidateHostRequest;
  decisionLocked: boolean;
  unresolvedStage: boolean;
  onStageFailure: (target: StageBindingContext & { toolId: string }, message: string) => void;
  onStageSuccess: () => void;
  onDecisionBusyChange: (busy: boolean) => void;
}) {
  const toolId = textValue(field(candidate, "toolId"));
  const contentIdentity = textValue(field(candidate, "contentIdentity")) ?? "";
  const contract = candidateContract(candidate);
  const origin = recordValue(field(candidate, "origin"));
  const permissions = recordValue(field(candidate, "permissions"));
  const [filePage, setFilePage] = useState<CandidateFilePage | null>(null);
  const [fileLoading, setFileLoading] = useState(false);
  const [fileError, setFileError] = useState<string | null>(null);
  const [previousOffsets, setPreviousOffsets] = useState<number[]>([]);
  const [history, setHistory] = useState<CandidateReviewRecord[] | null>(null);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [pendingDecision, setPendingDecision] = useState<"reviewed" | "rejected" | null>(null);
  const [decisionNote, setDecisionNote] = useState("");
  const [decisionBusy, setDecisionBusy] = useState(false);
  const [decisionError, setDecisionError] = useState<string | null>(null);
  const [decisionStatus, setDecisionStatus] = useState<string | null>(null);
  const [needsReload, setNeedsReload] = useState(false);
  const [outcomeUncertain, setOutcomeUncertain] = useState(false);
  const [bindingHistory, setBindingHistory] = useState<PreparedBindingRecord[] | null>(null);
  const [bindingHistoryLoading, setBindingHistoryLoading] = useState(false);
  const [bindingHistoryError, setBindingHistoryError] = useState<string | null>(null);
  const [pendingBindingAction, setPendingBindingAction] = useState<BindingAction | null>(null);
  const [bindingNote, setBindingNote] = useState("");
  const [bindingBusy, setBindingBusy] = useState(false);
  const [bindingError, setBindingError] = useState<string | null>(null);
  const [bindingStatus, setBindingStatus] = useState<string | null>(null);
  const [bindingUncertain, setBindingUncertain] = useState(false);
  const [pendingStage, setPendingStage] = useState(false);
  const [stageBusy, setStageBusy] = useState(false);
  const [stageResult, setStageResult] = useState<DisabledStageResult | null>(null);
  const fileRequestRef = useRef(0);
  const historyRequestRef = useRef(0);
  const bindingHistoryRequestRef = useRef(0);
  const noteRef = useRef<HTMLInputElement>(null);
  const bindingNoteRef = useRef<HTMLInputElement>(null);
  const reviewButtonRef = useRef<HTMLButtonElement>(null);
  const rejectButtonRef = useRef<HTMLButtonElement>(null);
  const prepareButtonRef = useRef<HTMLButtonElement>(null);
  const revokeButtonRef = useRef<HTMLButtonElement>(null);
  const cleanupButtonRef = useRef<HTMLButtonElement>(null);
  const stageButtonRef = useRef<HTMLButtonElement>(null);
  const stageConfirmRef = useRef<HTMLButtonElement>(null);
  const stageOutcomeRef = useRef<HTMLParagraphElement>(null);
  const stageInFlightRef = useRef(false);
  useEffect(() => () => { fileRequestRef.current += 1; historyRequestRef.current += 1; bindingHistoryRequestRef.current += 1; }, []);

  async function inspectFile(path: string, offset = 0, backstack: number[] = []) {
    if (!toolId || !contentIdentity) return;
    const request = { toolId, contentIdentity, path, offset };
    const generation = ++fileRequestRef.current;
    setFilePage(null);
    setFileError(null);
    setFileLoading(true);
    try {
      const response = await requestCandidateHost("readGeneratedToolCandidateFileRange", request);
      const page = parseCandidateFilePage(response, request);
      if (generation !== fileRequestRef.current) return;
      setFilePage(page);
      setPreviousOffsets(backstack);
    } catch (error) {
      if (generation !== fileRequestRef.current) return;
      const message = error instanceof Error ? error.message : "File inspection failed.";
      if (/already in progress/i.test(message)) {
        setFileError(`${message} Retry the named read; no stale identity was established.`);
      } else {
        setNeedsReload(true);
        setFileError(`${message} Reload candidates to revalidate before a decision.`);
      }
    } finally {
      if (generation === fileRequestRef.current) setFileLoading(false);
    }
  }

  async function loadHistory(): Promise<CandidateReviewRecord[] | null> {
    if (!toolId) return null;
    const generation = ++historyRequestRef.current;
    setHistoryLoading(true);
    setHistoryError(null);
    try {
      const response = await requestCandidateHost("listGeneratedToolReviewHistory", { toolId });
      const records = parseReviewHistory(response, toolId);
      if (generation !== historyRequestRef.current) return null;
      setHistory(records);
      return records;
    } catch (error) {
      if (generation !== historyRequestRef.current) return null;
      setHistory(null);
      setHistoryError(error instanceof Error ? error.message : "Review history could not be loaded.");
      return null;
    } finally {
      if (generation === historyRequestRef.current) setHistoryLoading(false);
    }
  }

  async function loadBindingHistory(): Promise<PreparedBindingRecord[] | null> {
    if (!toolId) return null;
    const generation = ++bindingHistoryRequestRef.current;
    setBindingHistoryLoading(true);
    setBindingHistoryError(null);
    try {
      const response = await requestCandidateHost("listGeneratedToolPreparedBindingHistory", { toolId });
      const records = parsePreparedBindingHistory(response, toolId);
      if (generation !== bindingHistoryRequestRef.current) return null;
      setBindingHistory(records);
      return records;
    } catch (error) {
      if (generation !== bindingHistoryRequestRef.current) return null;
      setBindingHistory(null);
      setBindingHistoryError(error instanceof Error ? error.message : "Prepared binding history could not be loaded.");
      return null;
    } finally {
      if (generation === bindingHistoryRequestRef.current) setBindingHistoryLoading(false);
    }
  }

  function beginDecision(decision: "reviewed" | "rejected") {
    setPendingDecision(decision);
    setDecisionNote("");
    setDecisionError(null);
    setDecisionStatus(null);
    requestAnimationFrame(() => noteRef.current?.focus());
  }

  function cancelDecision() {
    const button = pendingDecision === "reviewed" ? reviewButtonRef.current : rejectButtonRef.current;
    setPendingDecision(null);
    setDecisionNote("");
    requestAnimationFrame(() => button?.focus());
  }

  async function submitDecision() {
    if (!toolId || !contentIdentity || !contract || !pendingDecision || !history || currentReviewRecord(history, contentIdentity)
        || needsReload || outcomeUncertain || bindingBusy || bindingUncertain || decisionBusy || reviewNoteError(decisionNote)) return;
    const decision = pendingDecision;
    setDecisionBusy(true);
    onDecisionBusyChange(true);
    setDecisionError(null);
    setDecisionStatus(null);
    try {
      const response = await requestCandidateHost(decision === "reviewed" ? "recordGeneratedToolReview" : "rejectGeneratedToolCandidate",
        { toolId, contentIdentity, note: decisionNote });
      if (field(response, "attributedTo") !== "NeoBabylon.Host") {
        throw new Error("The decision response has no NeoBabylon.Host attribution.");
      }
      const records = await loadHistory();
      if (!records || currentReviewRecord(records, contentIdentity)?.decision !== decision) {
        throw new Error("The decision could not be confirmed in review history.");
      }
      setPendingDecision(null);
      setDecisionNote("");
      setDecisionStatus(`${decision === "reviewed" ? "Review" : "Rejection"} recorded for this content identity. The candidate remains unapproved and non-callable.`);
    } catch (error) {
      setOutcomeUncertain(true);
      setDecisionError(`${error instanceof Error ? error.message : "The decision outcome is unknown."} Reload candidates and history before attempting another decision; do not assume the action failed or succeeded.`);
    } finally {
      setDecisionBusy(false);
      onDecisionBusyChange(false);
    }
  }

  function beginBindingAction(action: BindingAction) {
    setPendingBindingAction(action);
    setBindingNote("");
    setBindingError(null);
    setBindingStatus(null);
    requestAnimationFrame(() => bindingNoteRef.current?.focus());
  }

  function beginStage() {
    if (!canStage) return;
    setPendingStage(true);
    requestAnimationFrame(() => stageConfirmRef.current?.focus());
  }

  function cancelStage() {
    setPendingStage(false);
    requestAnimationFrame(() => stageButtonRef.current?.focus());
  }

  async function submitStage() {
    const context = stageBindingContext(currentDecision, bindingHistory, contentIdentity);
    if (!canStage || !context || !pendingStage || !toolId || stageBusy || stageInFlightRef.current) return;
    stageInFlightRef.current = true;
    setStageBusy(true);
    onDecisionBusyChange(true);
    try {
      const response = await requestCandidateHost("stageGeneratedToolDisabledMcp", { toolId, ...context });
      const result = confirmDisabledStageResponse(response, toolId, context);
      setStageResult(result);
      onStageSuccess();
      setPendingStage(false);
    } catch (error) {
      setStageResult(null);
      onStageFailure({ toolId, ...context }, `${error instanceof Error ? error.message : "Stage outcome is unknown."} The outcome is uncertain. Revalidate the fresh candidate listing and exact binding/review histories before another action; do not assume staging failed or succeeded.`);
      setPendingStage(false);
    } finally {
      stageInFlightRef.current = false;
      setStageBusy(false);
      onDecisionBusyChange(false);
      requestAnimationFrame(() => stageOutcomeRef.current?.focus());
    }
  }

  function cancelBindingAction() {
    const button = pendingBindingAction === "prepare" ? prepareButtonRef.current
      : pendingBindingAction === "revoke" ? revokeButtonRef.current : cleanupButtonRef.current;
    setPendingBindingAction(null);
    setBindingNote("");
    requestAnimationFrame(() => button?.focus());
  }

  async function submitBindingAction() {
    const action = pendingBindingAction;
    if (!action || !bindingActionReady || !bindingHistory || !toolId
        || (action === "prepare" && (!contentIdentity || needsReload || outcomeUncertain || !contract))
        || pendingDecision || reviewNoteError(bindingNote)) return;
    if ((action === "prepare" && !canPrepare) || (action === "revoke" && !canRevoke)
        || (action === "cleanup" && !canCleanup)) return;
    const context = bindingActionContext(action, currentDecision, bindingHistory, contentIdentity);
    if (!context) return;
    const previous = bindingHistory.at(-1) ?? null;
    const operation = action === "prepare" ? "prepareGeneratedToolDisabledBinding"
      : action === "revoke" ? "revokeGeneratedToolPreparedBinding" : "cleanupGeneratedToolPreparedBinding";
    setBindingBusy(true);
    onDecisionBusyChange(true);
    setBindingError(null);
    setBindingStatus(null);
    try {
      const response = await requestCandidateHost(operation, { toolId, ...context, note: bindingNote });
      const records = await loadBindingHistory();
      confirmBindingTransition(response, records, action, toolId, context, previous);
      setStageResult(null);
      setPendingBindingAction(null);
      setBindingNote("");
      setBindingStatus(action === "prepare" ? "Disabled binding prepared and confirmed in history. It remains non-callable."
        : action === "revoke" ? "Binding revocation confirmed in history. It remains non-callable."
          : "Binding cleanup tombstone confirmed in history. Candidate and review records were not deleted.");
    } catch (error) {
      setBindingUncertain(true);
      setBindingError(`${error instanceof Error ? error.message : "Prepared binding outcome is unknown."} Reload candidates and both histories before another lifecycle action; do not assume the transition failed or succeeded.`);
    } finally {
      setBindingBusy(false);
      onDecisionBusyChange(false);
    }
  }

  const currentDecision = history && contentIdentity ? currentReviewRecord(history, contentIdentity) : null;
  const currentBinding = bindingHistory?.at(-1) ?? null;
  const activationReviewIdentity = currentDecision?.decision === "reviewed" ? currentDecision.reviewIdentity : null;
  const activationBindingRecordSha256 = currentBinding?.state === "prepared-disabled"
      && currentBinding.candidateContentIdentity === contentIdentity
      && currentBinding.reviewIdentity === activationReviewIdentity
    ? currentBinding.recordSha256 : null;
  const bindingActionReady = Boolean(toolId && bindingHistory && !bindingUncertain && !unresolvedStage && !decisionLocked && !decisionBusy && !bindingBusy && !stageBusy && !pendingStage && !pendingDecision && !bindingHistoryLoading);
  const canPrepare = bindingActionReady && Boolean(contract && !needsReload && !outcomeUncertain && !historyLoading && !fileLoading && bindingActionContext("prepare", currentDecision, bindingHistory, contentIdentity));
  const canRevoke = bindingActionReady && Boolean(bindingActionContext("revoke", currentDecision, bindingHistory, contentIdentity));
  const canCleanup = bindingActionReady && Boolean(bindingActionContext("cleanup", currentDecision, bindingHistory, contentIdentity));
  const stageContext = stageBindingContext(currentDecision, bindingHistory, contentIdentity);
  const canStage = Boolean(toolId && bindingHistory && contract && stageContext && !bindingUncertain && !unresolvedStage && !needsReload && !outcomeUncertain && !decisionLocked && !decisionBusy && !bindingBusy && !stageBusy && !pendingDecision && !pendingBindingAction && !bindingHistoryLoading && !historyLoading && !fileLoading && !stageResult);
  const canDecide = Boolean(toolId && contentIdentity && contract && history && !currentDecision && !needsReload && !outcomeUncertain && !bindingUncertain && !unresolvedStage && !decisionLocked && !decisionBusy && !bindingBusy && !pendingBindingAction && !historyLoading && !bindingHistoryLoading && !fileLoading);
  const noteError = pendingDecision ? reviewNoteError(decisionNote) : null;
  const bindingNoteError = pendingBindingAction ? reviewNoteError(bindingNote) : null;
  const pendingBindingContext = pendingBindingAction ? bindingActionContext(pendingBindingAction, currentDecision, bindingHistory, contentIdentity) : null;
  return <details className="candidate-card">
    <summary><span><strong>{displayValue(field(candidate, "name"))}</strong><small>{displayValue(field(candidate, "toolId"))} · revision {displayValue(field(candidate, "revision"))}</small></span><span className="candidate-state">{displayValue(field(candidate, "state"))}</span></summary>
    <div className="candidate-body">
      <CandidateFields source={candidate} entries={[{ key: "purpose", label: "Purpose" }, { key: "proposedBehavior", label: "Proposed behavior" }, { key: "missingCapability", label: "Missing capability / claimed gap" }, { key: "entryPoint", label: "Proposed entry point", mono: true }, { key: "actualPath", label: "Actual candidate path", mono: true }, { key: "contentIdentity", label: "Content identity / hash", mono: true }]} />
      <section className="candidate-subsection candidate-contract" aria-label="Declared invocation contract"><h4>Invocation contract · unverified declaration</h4><p>Displayed as inert data; this is not a callable registration or an authority grant.</p>
        {contract ? <><div><strong>Invocation</strong><pre tabIndex={0}>{contract.invocation}</pre></div><div><strong>Input schema</strong><pre tabIndex={0}>{contract.inputSchema}</pre></div><div><strong>Output schema</strong><pre tabIndex={0}>{contract.outputSchema}</pre></div></>
          : <p className="candidate-error" role="alert">The host did not supply a complete contract. Review and Reject are unavailable.</p>}
      </section>
      <section className="candidate-subsection"><h4>Provenance · unverified declaration</h4><CandidateFields source={origin} entries={[{ key: "taskId", label: "Task ID", mono: true }, { key: "sessionId", label: "Session ID", mono: true }, { key: "createdAtUtc", label: "Created at UTC" }, { key: "creator", label: "Creator" }, { key: "provider", label: "Provider" }, { key: "model", label: "Model" }, { key: "capabilityIdentity", label: "Capability identity / hash", mono: true }, { key: "gapEvidenceId", label: "Claimed gap evidence ID", mono: true }]} /></section>
      <section className="candidate-subsection"><h4>Permissions and authority · unverified declaration</h4><CandidateFields source={permissions} entries={[{ key: "requiredPermissions", label: "Required permissions" }, { key: "fileScopes", label: "File scopes", mono: true }, { key: "networkDestinations", label: "Network destinations" }, { key: "dataFlows", label: "Data flows" }, { key: "denialBehavior", label: "Denial behavior" }, { key: "permissionMode", label: "Declared permission mode" }, { key: "approvalPolicy", label: "Declared approval policy" }, { key: "evidenceSha256", label: "Authority evidence SHA-256", mono: true }]} /></section>
      <CandidateRecords title="Dependencies" records={field(candidate, "dependencies")} entries={[{ key: "name", label: "Name" }, { key: "version", label: "Version" }, { key: "source", label: "Source" }, { key: "sha256", label: "SHA-256", mono: true }]} declaration />
      <CandidateRecords title="Evidence" records={field(candidate, "evidence")} entries={[{ key: "kind", label: "Kind" }, { key: "evidenceId", label: "Evidence ID", mono: true }, { key: "outcome", label: "Declared outcome" }, { key: "description", label: "Description" }, { key: "path", label: "Path", mono: true }, { key: "sha256", label: "SHA-256", mono: true }]} declaration onInspectPath={(path) => void inspectFile(path)} inspectDisabled={fileLoading || historyLoading || decisionBusy || decisionLocked || unresolvedStage} />
      <CandidateRecords title="Files" records={field(candidate, "files")} entries={[{ key: "path", label: "Relative path", mono: true }, { key: "sha256", label: "SHA-256", mono: true }]} declaration onInspectPath={(path) => void inspectFile(path)} inspectDisabled={fileLoading || historyLoading || decisionBusy || decisionLocked || unresolvedStage} />
      <section className="candidate-subsection candidate-inspection" aria-label="Candidate file inspection"><h4>Source and evidence inspection</h4><p>Named host read only · exact displayed content identity · UTF-16 character offsets · at most 32,768 characters per page. Text is displayed, never executed.</p>
        {fileLoading && <p role="status">Loading file page…</p>}
        {fileError && <p className="candidate-error" role="alert">{fileError}</p>}
        {filePage && <><p className="candidate-page-position" role="status">{filePage.path} · characters {filePage.offset.toLocaleString()}–{filePage.next.toLocaleString()} of {filePage.total.toLocaleString()} (UTF-16)</p><pre className="candidate-file-text" tabIndex={0} aria-label={`${filePage.path} source text`}>{filePage.text}</pre><div className="candidate-page-actions"><button type="button" disabled={!previousOffsets.length || fileLoading || unresolvedStage} onClick={() => void inspectFile(filePage.path, previousOffsets.at(-1)!, previousOffsets.slice(0, -1))}>Previous page</button><button type="button" disabled={!filePage.hasMore || fileLoading || unresolvedStage} onClick={() => void inspectFile(filePage.path, filePage.next, [...previousOffsets, filePage.offset])}>Next page</button></div></>}
      </section>
      <section className="candidate-subsection candidate-review-history" aria-label="Candidate review history"><h4>Review history</h4><p>Product-mediated local decisions only. Hashes and links are operational integrity checks, not independent reviewer attestation or tamper-proof evidence.</p><button type="button" className="candidate-inline-action" disabled={historyLoading || fileLoading || decisionBusy || decisionLocked || pendingStage || unresolvedStage} onClick={() => void loadHistory()}>{historyLoading ? "Loading history…" : history ? "Reload review history" : "Load review history"}</button>
        {historyError && <p className="candidate-error" role="alert">{historyError}</p>}
        {history && !history.length && <p role="status">No review decisions recorded for this tool.</p>}
        {history?.map((record) => <div className="candidate-record" key={record.reviewIdentity}><strong className="candidate-history-title">{record.decision === "reviewed" ? "Reviewed" : "Rejected"} · sequence {record.sequence}{record.candidateContentIdentity === contentIdentity ? " · displayed content" : " · historical content"}</strong><CandidateFields source={recordValue(record)} entries={[{ key: "candidateContentIdentity", label: "Reviewed content identity", mono: true }, { key: "reviewInterfaceVersion", label: "Product review interface" }, { key: "interaction", label: "Interaction claim" }, { key: "note", label: "Decision note" }, { key: "recordedAtUtc", label: "Recorded at UTC" }, { key: "reviewIdentity", label: "Review identity", mono: true }, { key: "previousRecordSha256", label: "Previous record SHA-256", mono: true }, { key: "recordSha256", label: "Record SHA-256", mono: true }]} /></div>)}
        {toolId && contentIdentity && <GeneratedToolReviewComparison key={JSON.stringify([toolId, contentIdentity])}
          toolId={toolId} contentIdentity={contentIdentity} requestHost={requestCandidateHost} />}
      </section>
      <section className="candidate-subsection candidate-decision" aria-label="Manual candidate decision"><h4>Manual decision for displayed content</h4><p>Review and Reject are separate records. Neither action integrates, activates, revokes, cleans up, or grants tool authority.</p>
        {currentDecision && <p role="status">This exact content identity already has a {currentDecision.decision} decision. A duplicate or conflicting decision is unavailable.</p>}
        {!history && <p>Load review history before recording a decision.</p>}
        {!contract && <p className="candidate-error" role="alert">A complete displayed invocation contract is required before a decision.</p>}
        {needsReload && <p className="candidate-error" role="alert">The displayed candidate may be stale. Reload candidates before a decision.</p>}
        {decisionStatus && <p role="status">{decisionStatus}</p>}
        {decisionError && <p className="candidate-error" role="alert">{decisionError}</p>}
        {!pendingDecision && <div className="candidate-decision-actions"><button ref={reviewButtonRef} type="button" disabled={!canDecide} onClick={() => beginDecision("reviewed")}>Review this content</button><button ref={rejectButtonRef} type="button" disabled={!canDecide} onClick={() => beginDecision("rejected")}>Reject this content</button></div>}
        {pendingDecision && <fieldset className="candidate-confirm" disabled={decisionBusy}><legend>Confirm {pendingDecision === "reviewed" ? "Review" : "Reject"} for this exact content</legend><p className="candidate-confirm-identity">{contentIdentity}</p><label htmlFor={`candidate-note-${toolId}`}>Required single-line decision note</label><input ref={noteRef} id={`candidate-note-${toolId}`} type="text" value={decisionNote} onChange={(event) => setDecisionNote(event.currentTarget.value)} aria-invalid={Boolean(decisionNote && noteError)} aria-describedby={`candidate-note-help-${toolId}`} /><p id={`candidate-note-help-${toolId}`}>This records a local interaction, not independent attestation or activation. The note is sent exactly as entered (maximum 2,048 characters).</p>{decisionNote && noteError && <p className="candidate-error" role="alert">{noteError}</p>}<div className="candidate-decision-actions"><button type="button" disabled={Boolean(noteError) || !canDecide} onClick={() => void submitDecision()}>Confirm {pendingDecision === "reviewed" ? "Review" : "Reject"}</button><button type="button" onClick={cancelDecision}>Cancel</button></div></fieldset>}
      </section>
      <section className="candidate-subsection candidate-binding" aria-label="Prepared disabled binding lifecycle"><h4>Prepared-disabled binding · non-callable</h4><p>These are separate product-mediated local records. Preparing does not activate, register, expose, or permit a tool call. Revoke and Cleanup retain history; Cleanup appends a tombstone and does not delete the candidate or review.</p>
        <button type="button" className="candidate-inline-action" disabled={bindingHistoryLoading || decisionBusy || bindingBusy || decisionLocked || Boolean(pendingBindingAction) || pendingStage || unresolvedStage} onClick={() => void loadBindingHistory()}>{bindingHistoryLoading ? "Loading binding history…" : bindingHistory ? "Reload binding history" : "Load binding history"}</button>
        {bindingHistoryError && <p className="candidate-error" role="alert">{bindingHistoryError}</p>}
        {bindingHistory === null && <p>Load binding history before a lifecycle action.</p>}
        {bindingHistory && !bindingHistory.length && <p role="status">No prepared binding records for this tool.</p>}
        {currentBinding && <p className="candidate-binding-current" role="status">Latest recorded binding state: <strong>{currentBinding.state}</strong> · activation disabled · callable route none. Bound content: <span className="candidate-confirm-identity">{currentBinding.candidateContentIdentity}</span></p>}
        {currentBinding && currentBinding.candidateContentIdentity !== contentIdentity && <p>Latest binding belongs to historical content. Revoke or Cleanup uses that binding’s exact identity and latest record hash, not the displayed candidate’s identity.</p>}
        {bindingHistory && bindingHistory.length > 0 && <details className="candidate-binding-history"><summary>Binding history ({bindingHistory.length} records)</summary>{bindingHistory.map((record) => <div className="candidate-record" key={record.recordSha256}><strong className="candidate-history-title">{record.state} · sequence {record.sequence}</strong><CandidateFields source={recordValue(record)} entries={[{ key: "candidateContentIdentity", label: "Bound content identity", mono: true }, { key: "reviewIdentity", label: "Review identity", mono: true }, { key: "reviewInterfaceVersion", label: "Review interface version" }, { key: "reviewRecordSha256", label: "Review record SHA-256", mono: true }, { key: "activationState", label: "Activation state" }, { key: "callableRoute", label: "Callable route" }, { key: "note", label: "Transition note" }, { key: "recordedAtUtc", label: "Recorded at UTC" }, { key: "previousRecordSha256", label: "Previous record SHA-256", mono: true }, { key: "recordSha256", label: "Record SHA-256", mono: true }]} /></div>)}</details>}
        {bindingUncertain && <p className="candidate-error" role="alert">Binding outcome or identity is uncertain. Reload candidates and both histories before another action.</p>}
        {bindingStatus && <p role="status">{bindingStatus}</p>}
        {bindingError && <p className="candidate-error" role="alert">{bindingError}</p>}
        {!pendingBindingAction && <div className="candidate-decision-actions"><button ref={prepareButtonRef} type="button" disabled={!canPrepare} onClick={() => beginBindingAction("prepare")}>Prepare disabled binding</button><button ref={revokeButtonRef} type="button" disabled={!canRevoke} onClick={() => beginBindingAction("revoke")}>Revoke binding</button><button ref={cleanupButtonRef} type="button" disabled={!canCleanup} onClick={() => beginBindingAction("cleanup")}>Cleanup binding (tombstone)</button></div>}
        {pendingBindingAction && <fieldset className="candidate-confirm" disabled={bindingBusy}><legend>Confirm {pendingBindingAction === "prepare" ? "Prepare disabled" : pendingBindingAction === "revoke" ? "Revoke" : "Cleanup"} binding</legend><p>This records only a disabled, non-callable transition. No activation is available.</p><p className="candidate-confirm-identity">Content: {pendingBindingContext?.contentIdentity ?? "Unavailable"}<br />Review: {pendingBindingContext?.reviewIdentity ?? "Unavailable"}{pendingBindingContext?.currentRecordSha256 && <><br />Current record SHA-256: {pendingBindingContext.currentRecordSha256}</>}</p><label htmlFor={`candidate-binding-note-${toolId}`}>Required single-line transition note</label><input ref={bindingNoteRef} id={`candidate-binding-note-${toolId}`} type="text" value={bindingNote} onChange={(event) => setBindingNote(event.currentTarget.value)} aria-invalid={Boolean(bindingNote && bindingNoteError)} aria-describedby={`candidate-binding-note-help-${toolId}`} /><p id={`candidate-binding-note-help-${toolId}`}>The note is sent exactly as entered (maximum 2,048 characters). Cleanup retains candidate, review, and binding evidence.</p>{bindingNote && bindingNoteError && <p className="candidate-error" role="alert">{bindingNoteError}</p>}<div className="candidate-decision-actions"><button type="button" disabled={!pendingBindingContext || !bindingActionReady || Boolean(bindingNoteError)} onClick={() => void submitBindingAction()}>Confirm {pendingBindingAction === "prepare" ? "Prepare disabled" : pendingBindingAction === "revoke" ? "Revoke" : "Cleanup"}</button><button type="button" onClick={cancelBindingAction}>Cancel</button></div></fieldset>}
      </section>
      <section className="candidate-subsection candidate-binding" aria-label="Stage disabled MCP binding"><h4>Stage disabled MCP · separate local step</h4><p>Only the currently displayed, reviewed content with a current prepared-disabled binding can be staged. Staging is not activation and does not make a tool callable.</p>
        {stageResult && <p ref={stageOutcomeRef} tabIndex={-1} className="candidate-binding-current candidate-stage-outcome" role="status">Host staging result: <strong>{stageResult.state}</strong> for record <span className="candidate-confirm-identity">{stageResult.currentRecordSha256}</span>. Runtime confirmation: <strong>{stageResult.runtimeConfirmation}</strong>. No App Server confirmation or callable access is claimed.</p>}
        {!pendingStage && <button ref={stageButtonRef} type="button" className="candidate-inline-action" disabled={!canStage} onClick={beginStage}>Stage disabled MCP</button>}
        {pendingStage && <fieldset className="candidate-confirm" disabled={stageBusy}><legend>Confirm Stage disabled MCP</legend><p>The app-root Adapters binary must exist at <span className="candidate-confirm-identity">Adapters\NeoBabylon.GeneratedToolMcp.exe</span>. Staging requires no active App Server task. This does not Activate or grant callable access; runtime confirmation remains Unknown.</p><p className="candidate-confirm-identity">Tool: {toolId}<br />Content: {stageContext?.contentIdentity ?? "Unavailable"}<br />Review: {stageContext?.reviewIdentity ?? "Unavailable"}<br />Current record SHA-256: {stageContext?.currentRecordSha256 ?? "Unavailable"}</p><div className="candidate-decision-actions"><button ref={stageConfirmRef} type="button" disabled={!canStage} onClick={() => void submitStage()}>Confirm Stage disabled</button><button type="button" onClick={cancelStage}>Cancel</button></div></fieldset>}
      </section>
      {toolId && <GeneratedToolActivationPanel key={JSON.stringify([toolId, contentIdentity, activationReviewIdentity, activationBindingRecordSha256])}
        toolId={toolId} requestHost={requestCandidateHost} mode="candidate"
        contentIdentity={contentIdentity ?? null} reviewIdentity={activationReviewIdentity}
        bindingRecordSha256={activationBindingRecordSha256} permissions={permissions}
        actionLocked={decisionLocked || decisionBusy || bindingBusy || stageBusy || Boolean(pendingDecision || pendingBindingAction || pendingStage || unresolvedStage)}
        onActionBusyChange={onDecisionBusyChange} />}
    </div>
  </details>;
}

function BindingRecoverySection({ requestCandidateHost, actionLocked, onActionBusyChange }: {
  requestCandidateHost: CandidateHostRequest;
  actionLocked: boolean;
  onActionBusyChange: (busy: boolean) => void;
}) {
  const [toolIds, setToolIds] = useState<string[] | null>(null);
  const [idsLoading, setIdsLoading] = useState(false);
  const [idsError, setIdsError] = useState<string | null>(null);
  const [selectedToolId, setSelectedToolId] = useState<string | null>(null);
  const [history, setHistory] = useState<PreparedBindingRecord[] | null>(null);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<"revoke" | "cleanup" | null>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionStatus, setActionStatus] = useState<string | null>(null);
  const idsRequestRef = useRef(0);
  const historyRequestRef = useRef(0);
  const noteRef = useRef<HTMLInputElement>(null);
  const revokeRef = useRef<HTMLButtonElement>(null);
  const cleanupRef = useRef<HTMLButtonElement>(null);

  async function loadToolIds() {
    const generation = ++idsRequestRef.current;
    historyRequestRef.current += 1;
    setToolIds(null);
    setSelectedToolId(null);
    setHistory(null);
    setHistoryLoading(false);
    setHistoryError(null);
    setIdsError(null);
    setIdsLoading(true);
    try {
      const response = await requestCandidateHost("listGeneratedToolPreparedBindingToolIds");
      const ids = parsePreparedBindingToolIds(response);
      if (generation === idsRequestRef.current) setToolIds(ids);
    } catch (error) {
      if (generation === idsRequestRef.current) setIdsError(error instanceof Error ? error.message : "Binding recovery IDs could not be listed.");
    } finally {
      if (generation === idsRequestRef.current) setIdsLoading(false);
    }
  }

  async function loadBindingHistory(toolId: string): Promise<PreparedBindingRecord[] | null> {
    const generation = ++historyRequestRef.current;
    setHistory(null);
    setHistoryError(null);
    setHistoryLoading(true);
    try {
      const response = await requestCandidateHost("listGeneratedToolPreparedBindingHistory", { toolId });
      const records = parsePreparedBindingHistory(response, toolId);
      if (generation !== historyRequestRef.current) return null;
      setHistory(records);
      return records;
    } catch (error) {
      if (generation === historyRequestRef.current) setHistoryError(error instanceof Error ? error.message : "Binding recovery history could not be loaded.");
      return null;
    } finally {
      if (generation === historyRequestRef.current) setHistoryLoading(false);
    }
  }

  useEffect(() => {
    void loadToolIds();
    return () => { idsRequestRef.current += 1; historyRequestRef.current += 1; };
  }, []);

  function selectTool(toolId: string) {
    historyRequestRef.current += 1;
    setSelectedToolId(toolId || null);
    setHistory(null);
    setHistoryLoading(false);
    setHistoryError(null);
    setActionStatus(null);
    if (toolId) void loadBindingHistory(toolId);
  }

  function beginAction(action: "revoke" | "cleanup") {
    setPendingAction(action);
    setNote("");
    setActionError(null);
    setActionStatus(null);
    requestAnimationFrame(() => noteRef.current?.focus());
  }

  function cancelAction() {
    const button = pendingAction === "revoke" ? revokeRef.current : cleanupRef.current;
    setPendingAction(null);
    setNote("");
    requestAnimationFrame(() => button?.focus());
  }

  const current = history?.at(-1) ?? null;
  const canAct = Boolean(selectedToolId && toolIds?.includes(selectedToolId) && history && !historyLoading && !idsLoading && !uncertain && !busy && !actionLocked);
  const canRevoke = canAct && Boolean(bindingActionContext("revoke", null, history, ""));
  const canCleanup = canAct && Boolean(bindingActionContext("cleanup", null, history, ""));
  const context = pendingAction ? bindingActionContext(pendingAction, null, history, "") : null;
  const noteError = pendingAction ? reviewNoteError(note) : null;

  async function submitAction() {
    const action = pendingAction;
    const toolId = selectedToolId;
    if (!action || !toolId || !history || !context || !canAct || reviewNoteError(note)
        || (action === "revoke" && !canRevoke) || (action === "cleanup" && !canCleanup)) return;
    const previous = history.at(-1) ?? null;
    if (!previous || context.currentRecordSha256 !== previous.recordSha256) return;
    setBusy(true);
    onActionBusyChange(true);
    setActionError(null);
    setActionStatus(null);
    try {
      const response = await requestCandidateHost(action === "revoke" ? "revokeGeneratedToolPreparedBinding" : "cleanupGeneratedToolPreparedBinding",
        { toolId, contentIdentity: context.contentIdentity, reviewIdentity: context.reviewIdentity, currentRecordSha256: context.currentRecordSha256, note });
      const records = await loadBindingHistory(toolId);
      confirmBindingTransition(response, records, action, toolId, context, previous);
      setPendingAction(null);
      setNote("");
      setActionStatus(action === "revoke" ? "Revocation confirmed in binding history; the binding remains non-callable."
        : "Cleanup tombstone confirmed in binding history; candidate and review evidence is retained.");
    } catch (error) {
      setUncertain(true);
      setActionError(`${error instanceof Error ? error.message : "Recovery transition outcome is unknown."} No further recovery action is available in this drawer. Reopen it and inspect the current binding history before deciding what to do next.`);
    } finally {
      setBusy(false);
      onActionBusyChange(false);
    }
  }

  return <section className="candidate-recovery" aria-label="Binding denial recovery"><h3>Binding recovery · Revoke / Cleanup only</h3><p>This path reads durable binding records without validating a candidate. It remains available if the candidate listing is empty or fails. No Prepare or Activate action is available here.</p>
    <button type="button" className="candidate-inline-action" disabled={idsLoading || busy || actionLocked || Boolean(pendingAction)} onClick={() => void loadToolIds()}>{idsLoading ? "Loading binding IDs…" : "Reload binding IDs"}</button>
    {idsError && <p className="candidate-error" role="alert">{idsError}</p>}
    {toolIds && !toolIds.length && <p role="status">No durable binding IDs were returned.</p>}
    {toolIds && toolIds.length > 0 && <><label className="candidate-recovery-label" htmlFor="binding-recovery-tool">Binding tool ID</label><select id="binding-recovery-tool" value={selectedToolId ?? ""} disabled={busy || actionLocked || Boolean(pendingAction)} onChange={(event) => selectTool(event.currentTarget.value)}><option value="">Select a binding</option>{toolIds.map((toolId) => <option key={toolId} value={toolId}>{toolId}</option>)}</select></>}
    {selectedToolId && <button type="button" className="candidate-inline-action" disabled={historyLoading || busy || actionLocked || Boolean(pendingAction)} onClick={() => void loadBindingHistory(selectedToolId)}>{historyLoading ? "Loading selected history…" : "Reload selected history"}</button>}
    {historyError && <p className="candidate-error" role="alert">{historyError}</p>}
    {selectedToolId && history && !history.length && <p role="status">No published binding record for this ID. Denial actions are unavailable.</p>}
    {current && <><p className="candidate-binding-current" role="status">Latest recorded state: <strong>{current.state}</strong> · activation disabled · callable route none.</p><CandidateFields source={recordValue(current)} entries={[{ key: "toolId", label: "Binding tool ID", mono: true }, { key: "candidateContentIdentity", label: "Bound content identity", mono: true }, { key: "reviewIdentity", label: "Review identity", mono: true }, { key: "recordSha256", label: "Current record SHA-256", mono: true }, { key: "sequence", label: "Sequence" }, { key: "note", label: "Latest note" }]} /><details className="candidate-binding-history"><summary>Full binding history ({history?.length ?? 0} records)</summary>{history?.map((record) => <div className="candidate-record" key={record.recordSha256}><strong className="candidate-history-title">{record.state} · sequence {record.sequence}</strong><CandidateFields source={recordValue(record)} entries={[{ key: "candidateContentIdentity", label: "Bound content identity", mono: true }, { key: "reviewIdentity", label: "Review identity", mono: true }, { key: "reviewInterfaceVersion", label: "Review interface version" }, { key: "reviewRecordSha256", label: "Review record SHA-256", mono: true }, { key: "activationState", label: "Activation state" }, { key: "callableRoute", label: "Callable route" }, { key: "note", label: "Transition note" }, { key: "recordedAtUtc", label: "Recorded at UTC" }, { key: "previousRecordSha256", label: "Previous record SHA-256", mono: true }, { key: "recordSha256", label: "Record SHA-256", mono: true }]} /></div>)}</details></>}
    {selectedToolId && <GeneratedToolActivationPanel key={selectedToolId} toolId={selectedToolId}
      requestHost={requestCandidateHost} mode="recovery" actionLocked={actionLocked || busy}
      onActionBusyChange={onActionBusyChange} />}
    {uncertain && <p className="candidate-error" role="alert">The outcome is uncertain; recovery controls are disabled until the drawer is reopened and history is inspected again.</p>}
    {actionError && <p className="candidate-error" role="alert">{actionError}</p>}
    {actionStatus && <p role="status">{actionStatus}</p>}
    {!pendingAction && <div className="candidate-decision-actions"><button ref={revokeRef} type="button" disabled={!canRevoke} onClick={() => beginAction("revoke")}>Revoke selected binding</button><button ref={cleanupRef} type="button" disabled={!canCleanup} onClick={() => beginAction("cleanup")}>Cleanup selected binding (tombstone)</button></div>}
    {pendingAction && <fieldset className="candidate-confirm" disabled={busy}><legend>Confirm recovery {pendingAction === "revoke" ? "Revoke" : "Cleanup"}</legend><p>Only the latest hash-bound disabled binding record is targeted. Cleanup retains candidate and review evidence.</p><p className="candidate-confirm-identity">Tool: {selectedToolId}<br />Content: {context?.contentIdentity ?? "Unavailable"}<br />Review: {context?.reviewIdentity ?? "Unavailable"}<br />Current record SHA-256: {context?.currentRecordSha256 ?? "Unavailable"}</p><label htmlFor="binding-recovery-note">Required single-line note</label><input ref={noteRef} id="binding-recovery-note" type="text" value={note} onChange={(event) => setNote(event.currentTarget.value)} aria-invalid={Boolean(note && noteError)} aria-describedby="binding-recovery-note-help" /><p id="binding-recovery-note-help">The note is sent exactly as entered (maximum 2,048 characters). No activation or preparation is possible here.</p>{note && noteError && <p className="candidate-error" role="alert">{noteError}</p>}<div className="candidate-decision-actions"><button type="button" disabled={!context || !canAct || Boolean(noteError) || (pendingAction === "revoke" ? !canRevoke : !canCleanup)} onClick={() => void submitAction()}>Confirm {pendingAction === "revoke" ? "Revoke" : "Cleanup"}</button><button type="button" onClick={cancelAction}>Cancel</button></div></fieldset>}
  </section>;
}

function UnapprovedToolsDrawer({ candidates, loading, error, requestCandidateHost, unresolvedStage, stageRecoveryNotice, onStageFailure, onStageSuccess, onStageRecovered, onReload, onClose }: {
  candidates: JsonRecord[];
  loading: boolean;
  error: string | null;
  requestCandidateHost: CandidateHostRequest;
  unresolvedStage: { target: StageBindingContext & { toolId: string }; message: string } | null;
  stageRecoveryNotice: string | null;
  onStageFailure: (target: StageBindingContext & { toolId: string }, message: string) => void;
  onStageSuccess: () => void;
  onStageRecovered: () => void;
  onReload: () => void;
  onClose: () => void;
}) {
  const dialogRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const stageRecoveryOutcomeRef = useRef<HTMLParagraphElement>(null);
  const stageRecoveryInFlightRef = useRef(false);
  const [activeDecisions, setActiveDecisions] = useState(0);
  const [stageRecoveryBusy, setStageRecoveryBusy] = useState(false);
  const [stageRecoveryError, setStageRecoveryError] = useState<string | null>(null);
  useEffect(() => { closeButtonRef.current?.focus(); }, []);
  useEffect(() => { if (unresolvedStage) stageRecoveryOutcomeRef.current?.focus(); }, [unresolvedStage]);
  const closeIfIdle = () => { if (activeDecisions === 0 && !stageRecoveryInFlightRef.current) onClose(); };
  async function revalidateAfterStageFailure() {
    if (!unresolvedStage || loading || activeDecisions > 0 || stageRecoveryInFlightRef.current) return;
    const target = unresolvedStage.target;
    stageRecoveryInFlightRef.current = true;
    setStageRecoveryBusy(true);
    setStageRecoveryError(null);
    setActiveDecisions((count) => count + 1);
    try {
      const listing = await requestCandidateHost("listGeneratedToolCandidates");
      const reviews = await requestCandidateHost("listGeneratedToolReviewHistory", { toolId: target.toolId });
      const bindings = await requestCandidateHost("listGeneratedToolPreparedBindingHistory", { toolId: target.toolId });
      verifyStageRecovery(listing, reviews, bindings, target);
      onStageRecovered();
    } catch (failure) {
      setStageRecoveryError(`${failure instanceof Error ? failure.message : "Stage revalidation failed."} The original Stage target remains unresolved and candidate actions stay blocked.`);
    } finally {
      stageRecoveryInFlightRef.current = false;
      setStageRecoveryBusy(false);
      setActiveDecisions((count) => Math.max(0, count - 1));
      requestAnimationFrame(() => stageRecoveryOutcomeRef.current?.focus());
    }
  }
  return <div className="drawer-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) closeIfIdle(); }}>
    <section ref={dialogRef} tabIndex={-1} className="diagnostics-drawer unapproved-tools-drawer" role="dialog" aria-modal="true" aria-labelledby="unapproved-tools-title" onKeyDown={(event) => handleDialogKeyDown(event, dialogRef.current, closeIfIdle)}>
      <header className="drawer-header"><div><span className="details-kicker">NEOBABYLON · LOCAL CANDIDATES</span><h2 id="unapproved-tools-title">Unapproved tools</h2></div><button ref={closeButtonRef} className="icon-button" aria-label="Close unapproved tools" disabled={activeDecisions > 0} onClick={closeIfIdle}><Icon name="close" size={18} /></button></header>
      <div className="drawer-scroll" role="region" aria-label="Unapproved tool candidate details" tabIndex={0}>
        <p className="candidate-caveat">Candidate metadata and evidence are unverified declarations. Review, Reject, and prepared-disabled binding transitions are product-mediated local records, not independent attestation, tamper-proof evidence, activation, or permission to execute.</p>
        <button type="button" className="candidate-inline-action candidate-reload" disabled={loading || activeDecisions > 0 || stageRecoveryBusy} onClick={() => { if (!stageRecoveryInFlightRef.current) onReload(); }}>Reload candidates</button>
        {unresolvedStage && <section className="candidate-subsection candidate-binding" aria-label="Unresolved Stage disabled outcome"><h3>Stage outcome Unknown · actions blocked</h3><p ref={stageRecoveryOutcomeRef} tabIndex={-1} className="candidate-error" role="alert">{unresolvedStage.message}</p><p className="candidate-confirm-identity">Tool: {unresolvedStage.target.toolId}<br />Content: {unresolvedStage.target.contentIdentity}<br />Review: {unresolvedStage.target.reviewIdentity}<br />Current record SHA-256: {unresolvedStage.target.currentRecordSha256}</p><p>Reloading or closing this drawer does not clear the block. Only fresh candidate, review, and binding histories matching this original target can unblock candidate actions. Independent binding-denial recovery remains available below.</p>{stageRecoveryError && <p className="candidate-error" role="alert">{stageRecoveryError}</p>}<button type="button" className="candidate-inline-action" disabled={loading || activeDecisions > 0 || stageRecoveryBusy} onClick={() => void revalidateAfterStageFailure()}>{stageRecoveryBusy ? "Revalidating exact identity…" : "Revalidate candidate and histories"}</button></section>}
        {!unresolvedStage && stageRecoveryNotice && <p ref={stageRecoveryOutcomeRef} tabIndex={-1} className="candidate-binding-current candidate-stage-outcome" role="status">{stageRecoveryNotice}</p>}
        {loading && <p role="status">Loading unapproved tools…</p>}
        {error && <p className="review-unavailable" role="alert">{error}</p>}
        {!loading && !error && !candidates.length && <p className="tool-catalog-empty" role="status">No unapproved tool candidates were returned.</p>}
        {!loading && !error && candidates.map((candidate) => <CandidateCard candidate={candidate} requestCandidateHost={requestCandidateHost} decisionLocked={activeDecisions > 0} unresolvedStage={Boolean(unresolvedStage)} onStageFailure={(target, message) => { setStageRecoveryError(null); onStageFailure(target, message); }} onStageSuccess={onStageSuccess} onDecisionBusyChange={(busy) => setActiveDecisions((count) => Math.max(0, count + (busy ? 1 : -1)))} key={`${textValue(field(candidate, "toolId"))}-${textValue(field(candidate, "contentIdentity"))}`} />)}
        <BindingRecoverySection requestCandidateHost={requestCandidateHost} actionLocked={activeDecisions > 0} onActionBusyChange={(busy) => setActiveDecisions((count) => Math.max(0, count + (busy ? 1 : -1)))} />
      </div>
      <footer className="drawer-footer"><span>Manual records only · no callable route or execution grant</span><button type="button" className="drawer-done" disabled={activeDecisions > 0} onClick={closeIfIdle}>Done</button></footer>
    </section>
  </div>;
}

function DiagnosticsDrawer({ runtime, capability, providerCredentialCaptured, openProtectedRecordRecovery, onRestoreComplete, onClose }: {
  runtime: JsonRecord | null;
  capability: JsonRecord | null;
  providerCredentialCaptured: boolean;
  openProtectedRecordRecovery: boolean;
  onRestoreComplete: () => Promise<string>;
  onClose: () => void;
}) {
  const dialogRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const recoverySummaryRef = useRef<HTMLElement>(null);
  const [recoveryExpanded, setRecoveryExpanded] = useState(openProtectedRecordRecovery);
  useEffect(() => {
    closeButtonRef.current?.focus();
  }, []);
  useEffect(() => {
    if (!openProtectedRecordRecovery) return;
    setRecoveryExpanded(true);
    requestAnimationFrame(() => recoverySummaryRef.current?.focus());
  }, [openProtectedRecordRecovery]);
  const identity = recordValue(field(runtime, "runtime"));
  const authority = recordValue(field(recordValue(field(runtime, "lastThreadStart")), "executionAuthority"));
  const policy = recordValue(field(runtime, "executionPolicy"));
  const policyView = executionPolicyPresentation(policy, authority);
  const context = recordValue(field(runtime, "lastModelContextEvidence"));
  const providerContextAdvertised = observation(capability, "contextWindowAdvertised");
  const providerContextEffective = observation(capability, "contextWindowEffective");
  const toolCallingAdvertised = observation(capability, "toolFunctionCalling");
  return <div className="drawer-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}><section ref={dialogRef} tabIndex={-1} className="diagnostics-drawer" role="dialog" aria-modal="true" aria-labelledby="diagnostics-title" onKeyDown={(event) => handleDialogKeyDown(event, dialogRef.current, onClose)}><header className="drawer-header"><div><span className="details-kicker">NEOBABYLON · LOCAL</span><h2 id="diagnostics-title">Runtime diagnostics</h2></div><button ref={closeButtonRef} className="icon-button" aria-label="Close diagnostics" onClick={onClose}><Icon name="close" size={18} /></button></header><div className="drawer-scroll" role="region" aria-label="Runtime diagnostic details" tabIndex={0}>
    <section className="diagnostic-group"><div className="diagnostic-group-title"><span className="diagnostic-group-icon"><Icon name="pulse" size={15} /></span><div><h3>Runtime identity</h3><p>Verified against the product runtime lock.</p></div></div><DiagnosticField label="Version" value={displayValue(field(identity, "version"))} /><DiagnosticField label="Source revision" value={displayValue(field(identity, "sourceRevision"))} mono /><DiagnosticField label="Binary SHA-256" value={displayValue(field(identity, "sha256"))} mono /><DiagnosticField label="Runtime binary" value={displayValue(field(identity, "binaryPath"))} mono /></section>
    <section className="diagnostic-group">
      <div className="diagnostic-group-title"><span className="diagnostic-group-icon"><Icon name="spark" size={15} /></span><div><h3>Selected capability</h3><p>Provider metadata is not runtime telemetry. Unknown remains Unknown.</p></div></div>
      <DiagnosticField label="Provider" value={providerName(capability)} />
      <DiagnosticField label="Model" value={modelName(capability)} mono />
      <DiagnosticField label="Endpoint" value={displayValue(field(capability, "endpoint"))} mono />
      <DiagnosticField label="Credential captured at startup" value={providerCredentialCaptured ? "Yes · held by host" : "No · not configured"} />
      <DiagnosticField label="Provider context (advertised)" value={contextTokens(providerContextAdvertised.value)} />
      <DiagnosticField label="Provider context (effective)" value={contextTokens(providerContextEffective.value)} />
      <DiagnosticField label="Codex task context (observed)" value={contextTokens(field(context, "sessionContextWindow"))} />
      <DiagnosticField label="Provider context evidence (advertised)" value={providerContextAdvertised.evidence} />
      <DiagnosticField label="Provider context evidence (effective)" value={providerContextEffective.evidence} />
      <DiagnosticField label="Reasoning evidence" value={observation(capability, "reasoningControls").evidence} />
      <DiagnosticField label="Tool calling (advertised)" value={displayValue(toolCallingAdvertised.value)} />
      <DiagnosticField label="Tool qualification (selected tuple)" value={selectedToolQualificationSummary(capability)} />
      <DiagnosticField label="Tool calling evidence (advertised)" value={toolCallingAdvertised.evidence} />
    </section>
    <ToolCapabilityCatalog capability={capability} />
    <section className="diagnostic-group"><div className="diagnostic-group-title"><span className="diagnostic-group-icon"><Icon name="shield" size={15} /></span><div><h3>Isolation boundaries</h3><p>State belongs to the selected application root.</p></div></div><DiagnosticField label="Source repository" value={displayValue(field(runtime, "sourceRepositoryRoot"))} mono /><DiagnosticField label="Application root" value={displayValue(field(runtime, "applicationRoot"))} mono /><DiagnosticField label="Data root" value={displayValue(field(runtime, "dataRoot"))} mono /><DiagnosticField label="WebView2 profile" value={displayValue(field(runtime, "webView2UserDataFolder"))} mono /><DiagnosticField label="Isolated Codex home" value={displayValue(field(runtime, "codexHome"))} mono /><DiagnosticField label="Ordinary Codex root used" value={displayValue(field(runtime, "ordinaryCodexRootUsed"))} /></section>
    <section className="diagnostic-group"><div className="diagnostic-group-title"><span className="diagnostic-group-icon"><Icon name="shield" size={15} /></span><div><h3>Execution authority</h3><p>Requested and effective authority are shown separately.</p></div></div><DiagnosticField label="Selected tool policy" value={policyView.label} /><DiagnosticField label="Config sandbox mode" value={displayValue(field(recordValue(field(runtime, "executionPolicy")), "configSandboxMode"))} /><DiagnosticField label="App Server effective authority" value={displayValue(field(authority, "effectiveSandbox"))} /><DiagnosticField label="Containment qualified" value="No" /><DiagnosticField label="Capability record path" value={displayValue(field(runtime, "capabilityRecordPath"))} mono /></section>
    <details className="diagnostic-group protected-record-recovery" open={recoveryExpanded} onToggle={(event) => setRecoveryExpanded(event.currentTarget.open)}><summary ref={recoverySummaryRef} className="protected-record-recovery-summary"><span className="diagnostic-group-icon"><Icon name="shield" size={15} /></span><span><h3>Application records recovery</h3><p>Projects and Fork bookmarks only</p></span><span className="protected-record-recovery-hint">Show</span></summary><div className="protected-record-recovery-content"><ProtectedRecordRecoveryPanel requestHost={requestHost} onRestoreComplete={onRestoreComplete} /></div></details>
  </div><footer className="drawer-footer"><span><span className="connection-dot online" />Host-attributed diagnostics · limited record recovery</span><button className="drawer-done" onClick={onClose}>Done</button></footer></section></div>;
}

type ToolCatalogItemView = {
  id: string;
  name: string;
  description: string;
  exposure: { state: string; label: string; permissionSource: string; evidenceSource: string };
  qualification: { state: string; observedOn?: string; evidenceSource?: string; scope?: string; note?: string };
};

type ToolCatalogCategoryView = { id: string; title: string; items: ToolCatalogItemView[] };

const catalogQualificationLabel = (state: string) => ({
  qualified: "One recorded success",
  advertised: "Advertised · not end-to-end verified",
  known: "Known metadata",
  unsupported: "Unsupported for this tuple",
  blocked: "Blocked",
  "not-applicable": "Not model-specific",
}[state] ?? "Unknown");

const selectedToolQualificationSummary = (capability: JsonRecord | null): string => {
  const qualifications = selectedToolQualifications(capability);
  return qualifications.length
    ? qualifications.map((entry) => `${entry.operationId}: ${catalogQualificationLabel(entry.state)}`).join(" · ")
    : "Unknown";
};

const catalogStateClass = (state: string) => state.toLowerCase().replace(/[^a-z-]/g, "-");

function ToolCapabilityCatalog({ capability }: { capability: JsonRecord | null }) {
  const [query, setQuery] = useState("");
  const catalog = buildCapabilityCatalogView(toolCapabilityCatalog, capability) as ToolCatalogCategoryView[];
  const visibleCategories = filterCapabilityCatalog(catalog, query) as ToolCatalogCategoryView[];
  const selectedTuple = `${providerName(capability)} / ${modelName(capability)}`;

  return <section className="diagnostic-group tool-capability-catalog" data-tool-capability-catalog>
    <div className="diagnostic-group-title"><span className="diagnostic-group-icon"><Icon name="spark" size={15} /></span><div><h3>Tools &amp; capabilities</h3><p>Exposure and selected-tuple evidence are separate. Catalog v{String(field(toolCapabilityCatalog, "catalogVersion") ?? "Unknown")} · pinned App Server {String(field(recordValue(field(toolCapabilityCatalog, "upstreamRuntime")), "version") ?? "Unknown")}.</p></div></div>
    <div className="tool-catalog-intro"><span>Selected tuple</span><strong title={selectedTuple}>{selectedTuple}</strong><p>Read-only inventory; this panel does not launch or enable tools. Unknown and unsupported stay non-callable.</p></div>
    <label className="tool-catalog-search">Search categories and evidence<input type="search" value={query} onChange={(event) => setQuery(event.currentTarget.value)} placeholder="Search tools, status, or source" aria-label="Search tools and capabilities" /></label>
    <div className="tool-catalog-results" role="status" aria-live="polite">{visibleCategories.length} {visibleCategories.length === 1 ? "category" : "categories"}</div>
    <div className="tool-catalog-list">
      {visibleCategories.length ? visibleCategories.map((category) => <details className="tool-catalog-category" key={category.id}>
        <summary><span>{category.title}</span><span className="tool-catalog-count">{category.items.length}</span></summary>
        <div className="tool-catalog-items">
          {category.items.map((item) => <article className="tool-catalog-item" key={item.id} data-exposure-state={item.exposure.state} data-qualification-state={item.qualification.state}>
            <h4>{item.name}</h4>
            <p className="tool-catalog-description">{item.description}</p>
            <div className="tool-catalog-status-grid">
              <div><span>NeoBabylon exposure</span><strong className={`tool-catalog-badge exposure-${catalogStateClass(item.exposure.state)}`}>{item.exposure.label}</strong></div>
              <div><span>Selected tuple</span><strong className={`tool-catalog-badge qualification-${catalogStateClass(item.qualification.state)}`}>{catalogQualificationLabel(item.qualification.state)}{item.qualification.observedOn ? ` · ${item.qualification.observedOn}` : ""}</strong></div>
            </div>
            <div className="tool-catalog-provenance">
              <p><strong>Permission / boundary:</strong> {item.exposure.permissionSource}</p>
              <p><strong>Exposure evidence:</strong> {item.exposure.evidenceSource}</p>
              <p><strong>Tuple evidence:</strong> {item.qualification.evidenceSource || item.qualification.scope || "No tuple-specific evidence recorded."}</p>
              {item.qualification.scope ? <p><strong>Qualification scope:</strong> {item.qualification.scope}</p> : null}
              {item.qualification.note ? <p>{item.qualification.note}</p> : null}
            </div>
          </article>)}
        </div>
      </details>) : <p className="tool-catalog-empty" role="status">No categories match “{query}”.</p>}
    </div>
  </section>;
}

function DiagnosticField({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return <div className="diagnostic-field"><span>{label}</span><strong className={mono ? "mono-value" : ""} title={value}>{value}</strong></div>;
}
