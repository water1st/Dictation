import { readFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";

const TRELLO_API_BASE = "https://api.trello.com/1/";
const TASK_ID_PATTERN = /^task-([A-Za-z0-9-]+)_([0-9a-f]{10})$/;
const TITLE_TASK_PATTERN = /\[(task-[A-Za-z0-9-]+_[0-9a-f]{10})\]/g;
const ANY_TASK_PATTERN = /task-[A-Za-z0-9-]+_[0-9a-f]{10}/g;
const TRELLO_ID_PATTERN = /^[0-9a-f]{24}$/;

/** 验证真实 PR 合并、唯一任务标识及项目/仓库/分支对应关系。 */
export function validateMergeEvent(event, info) {
  const project = info?.projectName;
  const repository = info?.githubRepository;
  const baseBranch = info?.prBaseBranch;
  if (!project || !repository || !baseBranch || !repository.includes("/")) {
    throw new Error("ProjectInfo 项目身份或 PR 基准与仓库不一致");
  }
  if (event?.action !== "closed" || event?.pull_request?.merged !== true) {
    throw new Error("仅允许已合并 PR 的 closed 事件同步 Trello");
  }
  const pr = event.pull_request;
  if (
    event.repository?.full_name !== repository ||
    pr.base?.repo?.full_name !== repository ||
    pr.head?.repo?.full_name !== repository ||
    pr.base?.ref !== baseBranch
  ) {
    throw new Error("合并事件仓库、来源仓库或目标分支不符合项目配置");
  }
  const title = pr.title;
  const headRef = pr.head?.ref;
  if (typeof title !== "string" || typeof headRef !== "string") {
    throw new Error("合并事件缺少 PR 标题或来源分支");
  }
  const titleMatches = [...title.matchAll(TITLE_TASK_PATTERN)].map((match) => match[1]);
  const rawMatches = [...title.matchAll(ANY_TASK_PATTERN)].map((match) => match[0]);
  if (titleMatches.length !== 1 || rawMatches.length !== 1 || rawMatches[0] !== titleMatches[0]) {
    throw new Error("PR 标题必须恰有一个用方括号包围的完整 Task ID");
  }
  const taskId = titleMatches[0];
  const parsed = taskId.match(TASK_ID_PATTERN);
  if (!parsed || parsed[1] !== project) {
    throw new Error("PR Task ID 与配置的项目名称不匹配");
  }
  const branchPattern = new RegExp(
    "^codex/(feature|fix|opt|verify)/" + taskId + "/[a-z0-9][a-z0-9-]*$",
  );
  if (!branchPattern.test(headRef)) {
    throw new Error("PR 开发分支格式或其中的 Task ID 不匹配");
  }
  return taskId;
}

/** 仅报告操作名称及状态码；不得输出 URL、认证参数或响应正文。 */
async function requestTrello(fetchImpl, env, method, resource, operation, params = {}) {
  const url = new URL(resource, TRELLO_API_BASE);
  for (const [name, value] of Object.entries({
    ...params,
    key: env.TRELLO_API_KEY,
    token: env.TRELLO_TOKEN,
  })) {
    url.searchParams.set(name, String(value));
  }
  let response;
  try {
    response = await fetchImpl(url, {
      method,
      redirect: "error",
      signal: AbortSignal.timeout(30000),
    });
  } catch {
    throw new Error("Trello " + operation + " 网络请求失败");
  }
  if (!response.ok) {
    throw new Error("Trello " + operation + " 返回 HTTP " + response.status);
  }
  try {
    return await response.json();
  } catch {
    throw new Error("Trello " + operation + " 返回无效 JSON");
  }
}

function uniqueBy(records, field, value, description) {
  if (!Array.isArray(records)) {
    throw new Error("Trello " + description + " 的响应不是数组");
  }
  const matches = records.filter((record) => record?.[field] === value);
  if (matches.length !== 1) {
    throw new Error("Trello " + description + " 必须恰好匹配一项，实际 " + matches.length + " 项");
  }
  return matches[0];
}

/** 使用合并 PR 的唯一完整 Task ID 匹配卡片；仅在 PR → 已完成时发出 PUT。 */
export async function completeMergedTask({ event, projectInfo, env, fetchImpl = fetch }) {
  const taskId = validateMergeEvent(event, projectInfo);
  if (!projectInfo?.trello?.boardName) {
    throw new Error("ProjectInfo 的看板配置不完整");
  }
  const boardId = env?.BOARD_ID;
  if (!TRELLO_ID_PATTERN.test(boardId ?? "")) {
    throw new Error("BOARD_ID Secret 缺失或无效");
  }
  if (!env.TRELLO_API_KEY || !env.TRELLO_TOKEN) {
    throw new Error("TRELLO_API_KEY 或 TRELLO_TOKEN Secret 缺失");
  }
  const get = (path, name, params) => requestTrello(fetchImpl, env, "GET", path, name, params);

  const board = await get("boards/" + boardId, "核对看板", { fields: "id,name,closed" });
  if (board?.id !== boardId || board?.name !== projectInfo.trello.boardName || board.closed) {
    throw new Error("Trello 看板身份或状态不符合配置");
  }

  const lists = await get("boards/" + boardId + "/lists", "获取生命周期列表", {
    filter: "open",
    fields: "id,name",
  });
  const prList = uniqueBy(lists, "name", "PR", "PR 列表");
  const doneList = uniqueBy(lists, "name", "已完成", "已完成列表");
  if (!TRELLO_ID_PATTERN.test(prList.id) || !TRELLO_ID_PATTERN.test(doneList.id)) {
    throw new Error("Trello 生命周期列表 ID 不合法");
  }

  const cards = await get("boards/" + boardId + "/cards", "获取 Task 卡片", {
    filter: "all",
    fields: "id,name,idList,closed,dueComplete",
  });
  if (!Array.isArray(cards)) {
    throw new Error("Trello 卡片数据不是数组");
  }
  const matches = cards.filter((card) => card?.name?.endsWith("[" + taskId + "]"));
  if (matches.length !== 1) {
    throw new Error("完整 Task ID 对应的卡片数量不为 1，实际 " + matches.length + " 项");
  }
  const card = matches[0];
  if (!TRELLO_ID_PATTERN.test(card.id) || card.closed) {
    throw new Error("Task 卡片已归档或 ID 不合法");
  }
  if (card.idList === doneList.id && card.dueComplete === true) {
    return { taskId, status: "already-complete" };
  }
  if (card.idList !== prList.id) {
    throw new Error("Task 当前不在 PR 列表，拒绝修改");
  }
  const updated = await requestTrello(
    fetchImpl,
    env,
    "PUT",
    "cards/" + card.id,
    "完成 Task",
    { idList: doneList.id, dueComplete: "true" },
  );
  if (updated?.id !== card.id ||
      updated.idList !== doneList.id ||
      updated.dueComplete !== true) {
    throw new Error("Trello 未确认 Task 完成状态");
  }
  return { taskId, status: "completed" };
}

async function main() {
  if (!process.env.GITHUB_EVENT_PATH) {
    throw new Error("缺少 GitHub 合并事件文件");
  }
  const event = JSON.parse(await readFile(process.env.GITHUB_EVENT_PATH, "utf8"));
  const projectInfo = JSON.parse(
    await readFile(new URL("../../ProjectInfo.json", import.meta.url), "utf8"),
  );
  const result = await completeMergedTask({ event, projectInfo, env: process.env });
  console.log("Trello Task " + result.taskId + ": " + result.status);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    await main();
  } catch (error) {
    console.error("Trello 同步失败：" + (error instanceof Error ? error.message : "未知错误"));
    process.exitCode = 1;
  }
}
