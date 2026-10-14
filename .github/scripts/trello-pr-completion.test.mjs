import test from "node:test";
import assert from "node:assert/strict";
import { validateMergeEvent, completeMergedTask } from "./trello-pr-completion.mjs";

const taskId = "task-Dictation_bbccef99f9";
const boardId = "111111111111111111111111";
const prListId = "222222222222222222222222";
const doneListId = "333333333333333333333333";
const labelId = "444444444444444444444444";
const cardId = "555555555555555555555555";
const repo = "water1st/Dictation";
const projectInfo = {
  projectName: "Dictation",
  githubRepository: repo,
  prBaseBranch: "master",
  trello: { boardName: "Development Hub", projectLabel: { name: "Dictation", color: "yellow" } },
};
const env = { BOARD_ID: boardId, TRELLO_API_KEY: "fake-key", TRELLO_TOKEN: "fake-token" };

function event(overrides = {}) {
  return {
    action: "closed",
    repository: { full_name: repo },
    pull_request: {
      merged: true,
      title: "实现测试功能 [" + taskId + "]",
      base: { ref: "master", repo: { full_name: repo } },
      head: {
        ref: "codex/feature/" + taskId + "/testing",
        repo: { full_name: repo },
      },
      ...overrides,
    },
  };
}

function simulate({ cardPatch = {}, labelsPatch = null } = {}) {
  const calls = [];
  const card = {
    id: cardId,
    name: "任务 [" + taskId + "]",
    idList: prListId,
    idLabels: [labelId],
    closed: false,
    dueComplete: false,
    desc: "PROJECT: Dictation\nGITHUB_REPOSITORY: water1st/Dictation\nTRELLO_BOARD: Development Hub",
    ...cardPatch,
  };
  const responses = [
    { id: boardId, name: "Development Hub", closed: false },
    [{ id: prListId, name: "PR" }, { id: doneListId, name: "已完成" }],
    labelsPatch ?? [{ id: labelId, name: "Dictation", color: "yellow" }],
    [card],
  ];
  const fetchImpl = async (url, options) => {
    calls.push({ url: String(url), method: options.method });
    if (options.method === "PUT") {
      return { ok: true, json: async () => ({
        id: cardId, idList: doneListId, dueComplete: true,
      }) };
    }
    return { ok: true, json: async () => responses.shift() };
  };
  return { calls, fetchImpl };
}

test("接受正确合并事件与 Dictation Task", () => {
  assert.equal(validateMergeEvent(event(), projectInfo), taskId);
});

test("拒绝未合并 PR", () => {
  assert.throws(() => validateMergeEvent(event({ merged: false }), projectInfo));
});

test("拒绝错误项目和目标分支", () => {
  assert.throws(() => validateMergeEvent(event({ title: "任务 [task-MyDNS_aabbccddee]" }), projectInfo));
  assert.throws(() => validateMergeEvent(event({ base: { ref: "develop", repo: { full_name: repo } } }), projectInfo));
});

test("拒绝不匹配分支、重复 ID 及来自 Fork 的 PR", () => {
  assert.throws(() => validateMergeEvent(event({
    head: { ref: "codex/feature/task-Dictation_aaaaaaaaaa/test", repo: { full_name: repo } },
  }), projectInfo));
  assert.throws(() => validateMergeEvent(event({ title: "任务 [" + taskId + "] [" + taskId + "]" }), projectInfo));
  assert.throws(() => validateMergeEvent(event({
    head: { ref: "codex/feature/" + taskId + "/testing", repo: { full_name: "fork/Dictation" } },
  }), projectInfo));
});

test("合并后严格匹配并执行一次完成操作", async () => {
  const mock = simulate();
  const result = await completeMergedTask({
    event: event(), projectInfo, env, fetchImpl: mock.fetchImpl,
  });
  assert.equal(result.status, "completed");
  assert.equal(mock.calls.filter((call) => call.method === "PUT").length, 1);
});

test("已经完成的卡片重复触发时保持幂等", async () => {
  const mock = simulate({ cardPatch: { idList: doneListId, dueComplete: true } });
  const result = await completeMergedTask({
    event: event(), projectInfo, env, fetchImpl: mock.fetchImpl,
  });
  assert.equal(result.status, "already-complete");
  assert.equal(mock.calls.filter((call) => call.method === "PUT").length, 0);
});

test("缺少或错误项目标签时，禁止执行任何修改", async () => {
  const mock = simulate({ cardPatch: { idLabels: [] } });
  await assert.rejects(completeMergedTask({
    event: event(), projectInfo, env, fetchImpl: mock.fetchImpl,
  }));
  assert.equal(mock.calls.filter((call) => call.method === "PUT").length, 0);
});

test("卡片不处于 PR 时禁止移动", async () => {
  const mock = simulate({ cardPatch: { idList: "666666666666666666666666" } });
  await assert.rejects(completeMergedTask({
    event: event(), projectInfo, env, fetchImpl: mock.fetchImpl,
  }));
  assert.equal(mock.calls.filter((call) => call.method === "PUT").length, 0);
});

test("无效或缺失 Secrets 时不调用 API", async () => {
  const mock = simulate();
  await assert.rejects(completeMergedTask({
    event: event(), projectInfo, env: { ...env, BOARD_ID: "" }, fetchImpl: mock.fetchImpl,
  }));
  assert.equal(mock.calls.length, 0);
});

test("API 错误不将响应正文或密钥输出到报错消息", async () => {
  const fetchImpl = async () => ({
    ok: false,
    status: 403,
    text: async () => "fake-token fake-key",
  });
  await assert.rejects(
    completeMergedTask({ event: event(), projectInfo, env, fetchImpl }),
    (error) => !error.message.includes("fake-token") && !error.message.includes("fake-key"),
  );
});
