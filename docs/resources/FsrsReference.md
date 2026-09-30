# FSRS 数值参考

本产品的复习内核参考 [Open Spaced Repetition 官方 py-fsrs](https://github.com/open-spaced-repetition/py-fsrs/tree/9446cb06605c597a063aeee49f7d188d42e34dc2)，固定 commit `9446cb06605c597a063aeee49f7d188d42e34dc2`，MIT 许可证。参考 FSRS 6 默认 21 个参数，不自行拟合或声称适合每个人的最佳参数。

目标保持率 0.9；只接受程序根据作答得出的 Good（3）／Again（1）。会话内重复练习由产品学习／复习流程处理，因此数值对照使用 `Scheduler(desired_retention=0.9, learning_steps=(), relearning_steps=(), enable_fuzzing=False)`。间隔为 1–36500 整天、Python 银行家舍入；遗忘概率使用经过的完整天数，同日更新使用短期稳定度公式。仅出题顺序随机，不对到期日期加 fuzz。

2026-09-30 在内存加载固定版本未修改的 `rating/state/card/review_log/scheduler.py`，调用官方 Scheduler，生成以下黄金向量。产品和普通单元测试不依赖 Python、网络或官方源码文件。

| 作答序列（从首次评价起的天数） | 最终稳定度 | 最终难度 | 最终间隔（天） |
| --- | --- | --- | --- |
| 0 天 Good | 2.3065 | 2.118103970459016 | 2 |
| 0 天 Again | 0.212 | 6.4133 | 1 |
| 0 天 Good；2 天 Good | 10.964332335820698 | 2.111214235785395 | 11 |
| 0 天 Good；7 天 Again | 0.7178527669042931 | 7.394502741279718 | 1 |
| 0 天 Good；0.5 天 Again | 0.7750839828558984 | 7.394502741279718 | 1 |
| 0 天 Again；0.5 天 Good | 0.24668918777567272 | 6.402115069296838 | 1 |
| 0 天 Good；2 天 Good；8 天 Good；30 天 Again | 2.452929475623042 | 7.389975788014609 | 2 |

首次 Good 后第 2 天、第 7 天的预计保持率分别为 `0.9094932559773545`、`0.8083099402375552`，同日为 `1.0`；没有状态的旧词为待测，不提供虚构保持率。公式依据可核对 [官方算法说明](https://github.com/open-spaced-repetition/fsrs4anki/wiki/The-Algorithm)，实现对照以固定 commit 为准。

移植代码随包附 `LICENSES/py-fsrs.txt`。这是最小数值移植，不包含参数优化器、Hard/Easy 自评或新的通用调度框架。计划后续仍需验证每个成员词形的持久化、首次错误评价、风险查询和事务边界，算法测试通过不代表完整复习功能交付。
