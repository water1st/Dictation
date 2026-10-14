---
name: grill-with-docs
description: 对照真实项目文档与代码的需求访谈，使用 grilling 和 domain-modeling 的工作方法。
---

# Grill with Documents

1. 阅读当前项目的 `AGENTS.md`、`rules/design-documents.md`、相关正式文档与真实代码。
2. 加载 `../grilling/SKILL.md` 的分轮访谈流程与 `../domain-modeling/SKILL.md` 的领域概念核对流程。
3. 对外部能查到的事实自行核实，对需用户决策的分歧逐轮提问；提供建议且等待确认。
4. 讨论、审计、需求澄清不会自动创建 Trello Task，也不授权写入 `CONTEXT.md` 或 ADR。用户批准实施后按 Task 工作流修改。
5. 形成最终生效设计，不把背景、用户原话、新旧比较或修改历程写入正式文档。
