"""
simulate_agent_error.py
=======================
模拟 Agent 任务执行出错的常见场景，并演示对应的错误处理方式。

场景列表：
  1. ToolCallError      — 工具调用失败（如 API 超时、工具不存在）
  2. InvalidOutputError — Agent 输出格式不合法，无法解析
  3. MaxRetriesError    — 超过最大重试次数
  4. TaskTimeoutError   — 任务执行超时
  5. DependencyError    — 子任务依赖关系出错（前置任务失败）

运行方式：
  python simulate_agent_error.py
"""

import time
import random
import traceback
from dataclasses import dataclass, field
from typing import Any, Callable, Optional


# ---------------------------------------------------------------------------
# 自定义异常
# ---------------------------------------------------------------------------

class AgentTaskError(Exception):
    """Agent 任务基础异常"""


class ToolCallError(AgentTaskError):
    """工具调用失败"""


class InvalidOutputError(AgentTaskError):
    """Agent 输出格式无效"""


class MaxRetriesError(AgentTaskError):
    """超过最大重试次数"""


class TaskTimeoutError(AgentTaskError):
    """任务执行超时"""


class DependencyError(AgentTaskError):
    """依赖的前置任务失败"""


# ---------------------------------------------------------------------------
# 辅助数据结构
# ---------------------------------------------------------------------------

@dataclass
class TaskResult:
    task_id: str
    success: bool
    output: Any = None
    error: Optional[Exception] = None
    attempts: int = 0
    logs: list = field(default_factory=list)

    def __str__(self):
        status = "✅ 成功" if self.success else "❌ 失败"
        lines = [f"[{self.task_id}] {status} (尝试次数: {self.attempts})"]
        if self.error:
            lines.append(f"  错误类型 : {type(self.error).__name__}")
            lines.append(f"  错误信息 : {self.error}")
        if self.output:
            lines.append(f"  输出结果 : {self.output}")
        for log in self.logs:
            lines.append(f"  日志     : {log}")
        return "\n".join(lines)


# ---------------------------------------------------------------------------
# 模拟工具函数（有一定概率失败）
# ---------------------------------------------------------------------------

def flaky_search_tool(query: str, fail_rate: float = 0.7) -> str:
    """模拟一个不稳定的搜索工具"""
    if random.random() < fail_rate:
        raise ToolCallError(f"搜索工具调用失败：连接超时 (query='{query}')")
    return f"搜索结果：关于 '{query}' 的 3 条摘要信息"


def bad_llm_output() -> str:
    """模拟 LLM 返回格式错误的输出"""
    return "这不是合法的 JSON 格式：{{action: missing_quotes}}"


def slow_task(duration: float = 3.0) -> str:
    """模拟耗时任务"""
    time.sleep(duration)
    return "耗时任务完成"


# ---------------------------------------------------------------------------
# 场景 1：工具调用失败 + 重试机制
# ---------------------------------------------------------------------------

def run_with_retry(fn: Callable, max_retries: int = 3, **kwargs) -> Any:
    """带重试的工具调用执行器"""
    last_error = None
    for attempt in range(1, max_retries + 1):
        try:
            result = fn(**kwargs)
            return attempt, result
        except ToolCallError as e:
            last_error = e
            print(f"    第 {attempt} 次尝试失败：{e}")
            time.sleep(0.1 * attempt)
    raise MaxRetriesError(
        f"已重试 {max_retries} 次，仍然失败。最后一次错误：{last_error}"
    )


def scenario_tool_call_error() -> TaskResult:
    result = TaskResult(task_id="TASK-001", success=False, attempts=0)
    result.logs.append("开始执行：工具调用失败场景")
    try:
        attempts, output = run_with_retry(
            flaky_search_tool, max_retries=3, query="autoagents error handling"
        )
        result.success = True
        result.output = output
        result.attempts = attempts
    except MaxRetriesError as e:
        result.success = False
        result.error = e
        result.attempts = 3
    return result


# ---------------------------------------------------------------------------
# 场景 2：无效输出解析错误
# ---------------------------------------------------------------------------

def parse_agent_output(raw: str) -> dict:
    import json
    try:
        return json.loads(raw)
    except json.JSONDecodeError as e:
        raise InvalidOutputError(f"无法解析 Agent 输出为 JSON：{e}\n原始输出：{raw!r}")


def scenario_invalid_output() -> TaskResult:
    result = TaskResult(task_id="TASK-002", success=False, attempts=1)
    result.logs.append("开始执行：无效输出格式场景")
    try:
        raw = bad_llm_output()
        parsed = parse_agent_output(raw)
        result.success = True
        result.output = parsed
    except InvalidOutputError as e:
        result.success = False
        result.error = e
    return result


# ---------------------------------------------------------------------------
# 场景 3：任务超时
# ---------------------------------------------------------------------------

def run_with_timeout(fn: Callable, timeout: float, **kwargs) -> Any:
    """简单的超时检测（通过测量实际耗时模拟；生产中应使用线程/信号）"""
    start = time.monotonic()
    # 在模拟中我们直接检测函数声明的 duration 参数
    declared_duration = kwargs.get("duration", 0)
    if declared_duration > timeout:
        raise TaskTimeoutError(
            f"任务预计耗时 {declared_duration}s，超过超时阈值 {timeout}s，已中止"
        )
    result = fn(**kwargs)
    elapsed = time.monotonic() - start
    return result, elapsed


def scenario_task_timeout() -> TaskResult:
    result = TaskResult(task_id="TASK-003", success=False, attempts=1)
    result.logs.append("开始执行：任务超时场景（超时阈值 2s，任务需要 5s）")
    try:
        output, elapsed = run_with_timeout(slow_task, timeout=2.0, duration=5.0)
        result.success = True
        result.output = output
        result.logs.append(f"实际耗时 {elapsed:.2f}s")
    except TaskTimeoutError as e:
        result.success = False
        result.error = e
    return result


# ---------------------------------------------------------------------------
# 场景 4：依赖任务失败导致后续任务无法执行
# ---------------------------------------------------------------------------

def scenario_dependency_error() -> list[TaskResult]:
    results = []

    # 前置任务（故意失败）
    pre_task = TaskResult(task_id="TASK-004a", success=False, attempts=1)
    pre_task.logs.append("前置任务：数据采集")
    pre_task.error = ToolCallError("数据源连接被拒绝（HTTP 403）")
    results.append(pre_task)

    # 依赖前置任务的后续任务
    follow_up = TaskResult(task_id="TASK-004b", success=False, attempts=0)
    follow_up.logs.append("后续任务：数据分析（依赖 TASK-004a）")
    if not pre_task.success:
        follow_up.error = DependencyError(
            f"前置任务 {pre_task.task_id} 执行失败，后续任务已跳过"
        )
        follow_up.logs.append("任务已被标记为跳过（SKIPPED）")
    results.append(follow_up)

    return results


# ---------------------------------------------------------------------------
# 主流程：依次运行所有场景
# ---------------------------------------------------------------------------

SEPARATOR = "=" * 60


def main():
    random.seed(42)  # 固定随机种子，确保场景可复现

    print(SEPARATOR)
    print("  Agent 任务执行出错模拟演示")
    print(SEPARATOR)

    # 场景 1
    print("\n【场景 1】工具调用失败 + 重试机制")
    print("-" * 40)
    r1 = scenario_tool_call_error()
    print(r1)

    # 场景 2
    print("\n【场景 2】无效输出格式错误")
    print("-" * 40)
    r2 = scenario_invalid_output()
    print(r2)

    # 场景 3
    print("\n【场景 3】任务执行超时")
    print("-" * 40)
    r3 = scenario_task_timeout()
    print(r3)

    # 场景 4
    print("\n【场景 4】依赖任务失败")
    print("-" * 40)
    for r in scenario_dependency_error():
        print(r)

    # 汇总
    print(f"\n{SEPARATOR}")
    all_results = [r1, r2, r3] + scenario_dependency_error()
    failed = [r for r in all_results if not r.success]
    print(f"任务总数: {len(all_results)}  成功: {len(all_results) - len(failed)}  失败: {len(failed)}")
    print(SEPARATOR)


if __name__ == "__main__":
    main()
