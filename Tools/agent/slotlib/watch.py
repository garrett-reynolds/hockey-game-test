"""Seam for the hang watch (ticket #179).

`test` calls watch_run() once the run_tests CLI process has started and the slot lock is held.
It must return only when the process has exited (and return its exit code). Today it just waits.
#179 replaces the body with a CPU sampler: sample the slot's Editor CPU, and report a hang.
Never kill the CLI process from here: a run cut short jams the Editor.
"""


def watch_run(slot, proc):
    return proc.wait()
