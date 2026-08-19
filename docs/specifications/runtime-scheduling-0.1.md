# Runtime Scheduling 0.1

This document records the Phase 0-13 baseline. Phase 0-19 extends it with the bounded concurrency rules in [Runtime Parallel Scheduling 0.1](runtime-parallel-scheduling-0.1.md).

Phase 0-13 scheduling is deterministic, dependency-driven, sequential, and single process.

A step becomes ready only when its control activation is satisfied, required data dependencies are available, binding and expression source steps have completed, the step is not terminal, the step is not skipped, and execution has not completed cancellation.

The planned `core.start` entry step is the only initially activated step. No other control-dependent step starts automatically. Data-only dependencies do not activate control-flow nodes.

When multiple steps are ready, the scheduler chooses by plan document order. The scheduler does not recurse, busy wait, create unbounded tasks, or execute parallel branches. It checks cancellation before each step and enforces execution limits.

If no step is ready, no step is running, execution is not terminal, and pending steps remain, the scheduler reports `SKR1015`.
