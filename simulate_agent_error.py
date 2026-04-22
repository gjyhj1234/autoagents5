"""
simulate_agent_error.py
=======================
Simulates a common agent-task pitfall:
  - The task's *status* field is reported as "completed"
  - But the final log / conversation text contains a continuation prompt
    such as "需要继续处理" ("further processing required")

An unattended monitor (`TaskContinuationMonitor`) detects this mismatch and
keeps replying to the task until the work is genuinely finished.

Quick-start
-----------
    python simulate_agent_error.py

The script runs a self-contained demo that:
  1. Creates a fake task that finishes in TOTAL_ROUNDS rounds of work.
  2. Reports status = "completed" after every round (simulating the bug).
  3. Embeds a continuation hint in the log until the last round.
  4. Runs the monitor, which detects the hint and resumes automatically.
"""

from __future__ import annotations

import logging
import random
import time
from dataclasses import dataclass, field
from typing import Callable

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

TOTAL_ROUNDS: int = 5          # how many rounds until truly done
CONTINUATION_HINTS: list[str] = [
    "需要继续处理",
    "处理未完成，请继续",
    "further processing required",
    "task not fully complete, continue",
    "please continue to process",
]
COMPLETION_MARKERS: list[str] = [
    "所有任务已完成",
    "task fully completed",
    "processing finished",
]
MONITOR_POLL_INTERVAL: float = 1.0   # seconds between polls (demo speed)
MAX_MONITOR_ITERATIONS: int = 20     # safety cap

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Data model
# ---------------------------------------------------------------------------

@dataclass
class TaskResult:
    """Represents the response returned after one execution round."""
    status: str           # "completed" | "in_progress" | "failed"
    log_text: str         # full log / conversation text from the agent
    round_number: int     # which round produced this result


@dataclass
class AgentTask:
    """Simulated agent task that requires multiple rounds to fully finish."""
    task_id: str
    total_rounds: int
    _current_round: int = field(default=0, init=False, repr=False)

    def execute_one_round(self) -> TaskResult:
        """
        Advance the task by one round.

        Bug being simulated
        -------------------
        Status is always "completed" even when more rounds remain.
        The log text carries a continuation hint so a smart monitor can
        detect the real state.
        """
        self._current_round += 1
        truly_done = self._current_round >= self.total_rounds

        status = "completed"          # ← the misleading status field

        if truly_done:
            hint = random.choice(COMPLETION_MARKERS)
            log_text = (
                f"[Round {self._current_round}/{self.total_rounds}] "
                f"All items processed. {hint}"
            )
        else:
            hint = random.choice(CONTINUATION_HINTS)
            remaining = self.total_rounds - self._current_round
            log_text = (
                f"[Round {self._current_round}/{self.total_rounds}] "
                f"Processed batch {self._current_round}. "
                f"{remaining} batch(es) remaining. {hint}"
            )

        return TaskResult(
            status=status,
            log_text=log_text,
            round_number=self._current_round,
        )

    @property
    def is_truly_done(self) -> bool:
        return self._current_round >= self.total_rounds


# ---------------------------------------------------------------------------
# Continuation detection
# ---------------------------------------------------------------------------

def result_needs_continuation(result: TaskResult) -> bool:
    """
    Return True when the result looks "completed" by status but the log
    text reveals that more work is needed.

    Strategy
    --------
    1. Status must be "completed" (otherwise the caller handles it differently).
    2. Scan log_text for known continuation hints (case-insensitive).
    3. If no hint is found, check that at least one completion marker IS present;
       absence of a positive completion signal is also treated as incomplete.
    """
    if result.status != "completed":
        return False   # not our responsibility here

    lower_log = result.log_text.lower()

    for hint in CONTINUATION_HINTS:
        if hint.lower() in lower_log:
            log.debug("Continuation hint found in log: %r", hint)
            return True

    # Secondary check: completed status but no positive "done" marker either
    has_completion_marker = any(
        marker.lower() in lower_log for marker in COMPLETION_MARKERS
    )
    if not has_completion_marker:
        log.debug("No completion marker in log — treating as incomplete.")
        return True

    return False


# ---------------------------------------------------------------------------
# Continuation monitor
# ---------------------------------------------------------------------------

class TaskContinuationMonitor:
    """
    Watches a task and automatically re-submits a continuation request
    whenever a false-complete result is detected.

    Parameters
    ----------
    task_runner:
        Callable that executes one round and returns a TaskResult.
    on_continue:
        Optional hook called each time a continuation is triggered.
        Receives the previous TaskResult. Useful for logging / UI updates.
    poll_interval:
        Seconds to wait between rounds (default: MONITOR_POLL_INTERVAL).
    max_iterations:
        Hard cap on the number of rounds (default: MAX_MONITOR_ITERATIONS).
    """

    def __init__(
        self,
        task_runner: Callable[[], TaskResult],
        on_continue: Callable[[TaskResult], None] | None = None,
        poll_interval: float = MONITOR_POLL_INTERVAL,
        max_iterations: int = MAX_MONITOR_ITERATIONS,
    ) -> None:
        self._run = task_runner
        self._on_continue = on_continue
        self._poll_interval = poll_interval
        self._max_iterations = max_iterations

    def run_until_done(self) -> TaskResult:
        """
        Execute the task in a loop, resuming automatically whenever a
        false-complete state is detected.

        Returns the final TaskResult when truly done (no continuation needed).
        Raises RuntimeError if the iteration cap is reached first.
        """
        last_result: TaskResult | None = None

        for iteration in range(1, self._max_iterations + 1):
            log.info("--- Monitor iteration %d ---", iteration)
            result = self._run()
            last_result = result

            log.info(
                "Task status=%r | Round %d | Log: %s",
                result.status,
                result.round_number,
                result.log_text,
            )

            if result_needs_continuation(result):
                log.warning(
                    "False-complete detected (status=%r but log suggests more "
                    "work). Sending continuation request …",
                    result.status,
                )
                if self._on_continue:
                    self._on_continue(result)
                time.sleep(self._poll_interval)
                continue

            # Genuine completion
            log.info("Task is genuinely complete. Stopping monitor.")
            return result

        raise RuntimeError(
            f"Reached iteration cap ({self._max_iterations}) without genuine "
            "completion. Manual intervention required."
        )


# ---------------------------------------------------------------------------
# Demo / main
# ---------------------------------------------------------------------------

def _on_continue_hook(result: TaskResult) -> None:
    """Simulate sending a continuation message back to the task."""
    continuation_msg = (
        "检测到任务尚未完成，自动继续处理中…"
        " (Detected incomplete task — auto-resuming …)"
    )
    log.info(">>> Continuation message sent: %s", continuation_msg)


def main() -> None:
    print("=" * 60)
    print(" Simulation: false-complete task with auto-resume monitor")
    print("=" * 60)
    print(
        f"\nTask will need {TOTAL_ROUNDS} rounds to complete.\n"
        "Status will report 'completed' after EVERY round (the bug).\n"
        "The monitor detects continuation hints and resumes automatically.\n"
    )

    task = AgentTask(task_id="demo-task-001", total_rounds=TOTAL_ROUNDS)
    monitor = TaskContinuationMonitor(
        task_runner=task.execute_one_round,
        on_continue=_on_continue_hook,
        poll_interval=MONITOR_POLL_INTERVAL,
        max_iterations=MAX_MONITOR_ITERATIONS,
    )

    try:
        final = monitor.run_until_done()
        print("\n" + "=" * 60)
        print(" DONE — task genuinely completed.")
        print(f" Final log: {final.log_text}")
        print("=" * 60)
    except RuntimeError as exc:
        print(f"\n[ERROR] {exc}")


if __name__ == "__main__":
    main()
