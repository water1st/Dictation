import { readFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";

const API_BASE = "https://api.trello.com/1/";
const TASK_ID = /^task-Dictation_[0-9a-f]{10}$/;
const TASK_IN_TEXT = /task-Dictation_[0-9a-f]{10}/g;
const TASK_AT_END = /\[(task-Dictation_[0-9a-f]{10})\]$/;
const TRELLO_ID = /^[0-9a-f]{24}$/;
const BUSY_LISTS = ["開發中", "測試中", "PR"];

function dependencies(description) {
  if (typeof description !== "string") return [];
  const lines = description.split(/\r?\n/);
  const by = lines.filter(line => /^\s*-?\s*Blocked By\s*:/i.test(line));
  if (by.length > 1) return [];
  if (by.length === 1) {
    const values = by[0].replace(/^\s*-?\s*Blocked By\s*:\s*/i, "")
      .split(",").map(value => value.trim());
    return values.length && values.every(value => TASK_ID.test(value)) &&
      new Set(values).size === values.length ? values : [];
  }
  // Older suspension records may express the dependency in their Reason.
  const reasons = lines.filter(line => /^\s*-?\s*Reason\s*:/i.test(line));
  if (reasons.length !== 1 || !/因为|由于|依赖|等待|阻塞|blocked by|waiting for|depends on/i.test(reasons[0])) {
    return [];
  }
  const found = reasons[0].match(TASK_IN_TEXT) ?? [];
  return found.length && new Set(found).size === found.length ? found : [];
}

function cardTaskId(card) {
  return typeof card?.name === "string" ? card.name.match(TASK_AT_END)?.[1] : undefined;
}

function isGroup(card) {
  const description = card.desc ?? "";
  return /^\s*-?\s*Parent Task:\s*task-Dictation_[0-9a-f]{10}\b/m.test(description) ||
    /^\s*-?\s*Active Child:\s*task-Dictation_[0-9a-f]{10}\b/m.test(description) ||
    /^\s*-?\s*Child Tasks:\s*task-Dictation_[0-9a-f]{10}\b/m.test(description);
}

function rank(card) {
  const priority = String(card.desc ?? "").match(/^\s*Priority:\s*P([0-3])\b/m)?.[1];
  return priority === undefined ? 4 : Number(priority);
}

async function requestTrello(env, method, resource, params = {}) {
  const url = new URL(resource, API_BASE);
  for (const [key, value] of Object.entries({
    ...params, key: env.TRELLO_API_KEY, token: env.TRELLO_TOKEN,
  })) url.searchParams.set(key, String(value));
  const response = await fetch(url, {
    method, redirect: "error", signal: AbortSignal.timeout(30000),
  });
  if (!response.ok) throw new Error("Trello HTTP " + response.status);
  return response.json();
}

export async function resumeBlockedTasks(event, env) {
  const result = { resumed: 0, skipped: 0, reason: "no-match" };
  const pr = event?.pull_request;
  if (event?.action !== "closed" || pr?.merged !== true ||
      event.repository?.full_name !== "water1st/Dictation" ||
      pr.base?.repo?.full_name !== "water1st/Dictation" ||
      pr.head?.repo?.full_name !== "water1st/Dictation" ||
      pr.base?.ref !== "master") return result;

  const ids = String(pr.title ?? "").match(/\[task-Dictation_[0-9a-f]{10}\]/g) ?? [];
  if (ids.length !== 1) return result;
  const completedId = ids[0].slice(1, -1);
  if (!TRELLO_ID.test(env.BOARD_ID ?? "") || !env.TRELLO_API_KEY || !env.TRELLO_TOKEN) {
    console.warn("Trello resume: missing configuration");
    return result;
  }

  const boardId = env.BOARD_ID;
  const lists = await requestTrello(env, "GET", "boards/" + boardId + "/lists", {
    filter: "open", fields: "id,name",
  });
  const listId = name => {
    const matches = lists.filter(list => list?.name === name);
    if (matches.length !== 1 || !TRELLO_ID.test(matches[0].id)) {
      throw new Error("Trello list missing or duplicated: " + name);
    }
    return matches[0].id;
  };
  const suspended = listId("掛起");
  const developing = listId("開發中");
  const done = listId("已完成");
  const busy = new Set(BUSY_LISTS.map(listId));

  const cards = await requestTrello(env, "GET", "boards/" + boardId + "/cards", {
    filter: "all", fields: "id,name,desc,idList,closed,pos",
  });
  if (!Array.isArray(cards)) throw new Error("Trello returned invalid cards");
  const byTask = id => cards.filter(card => cardTaskId(card) === id);
  const completed = byTask(completedId);
  if (completed.length !== 1 || completed[0].idList !== done) {
    result.reason = "merged-task-not-complete";
    return result;
  }

  // An active task already occupies this project's single execution slot.
  if (cards.some(card => !card.closed && cardTaskId(card) && busy.has(card.idList))) {
    result.reason = "execution-slot-busy";
    return result;
  }

  const candidates = cards.filter(card =>
    !card.closed && card.idList === suspended && cardTaskId(card) &&
    dependencies(card.desc).length > 0,
  ).sort((a, b) => rank(a) - rank(b) || Number(a.pos ?? 0) - Number(b.pos ?? 0));

  for (const card of candidates) {
    if (isGroup(card)) { result.skipped++; continue; }
    const blockers = dependencies(card.desc);
    const allComplete = blockers.every(id => {
      const matches = byTask(id);
      return matches.length === 1 && matches[0].idList === done;
    });
    if (!allComplete) { result.skipped++; continue; }

    const updated = await requestTrello(env, "PUT", "cards/" + card.id, { idList: developing });
    if (updated?.id !== card.id || updated.idList !== developing) {
      throw new Error("Trello did not confirm resumed task");
    }
    result.resumed = 1;
    result.reason = "resumed";
    return result; // Only one unrelated task may occupy the project execution slot.
  }
  return result;
}

async function main() {
  try {
    const event = JSON.parse(await readFile(process.env.GITHUB_EVENT_PATH, "utf8"));
    const result = await resumeBlockedTasks(event, process.env);
    console.log("Trello dependent resume:", JSON.stringify(result));
  } catch (error) {
    // Trello synchronization is best-effort and must not fail the PR workflow.
    console.warn("Trello dependent resume skipped:", error instanceof Error ? error.message : "unknown error");
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  await main();
}
