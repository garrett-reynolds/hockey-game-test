import os

from conftest import COMPILING, PLAY, READY, reply


def lines(out):
    return [x for x in out.splitlines() if x.strip()]


def test_status_one_line_per_slot(h):
    h.ready("111")
    h.set("w2", "editor_status", reply(PLAY))
    h.set("w2", "test_status", reply({"status": "no_tests"}))
    # w3: no replies at all = Editor does not answer
    rc, out = h.run("status")
    assert rc == 0
    ls = lines(out)
    assert len(ls) == 3
    assert ls[0].startswith("w1 (111): free")
    assert ls[1].startswith("w2: in Play")
    assert ls[2].startswith("w3: not answering")


def test_status_busy_when_a_test_run_is_live(h):
    h.set("111", "editor_status", reply(READY))
    h.set("111", "test_status", reply({"status": "running"}))
    rc, out = h.run("status")
    assert "w1 (111): busy (test run" in out


def test_status_busy_when_compiling(h):
    h.set("111", "editor_status", reply(COMPILING))
    h.set("111", "test_status", reply({"status": "no_tests"}))
    rc, out = h.run("status")
    assert "w1 (111): busy (compiling" in out


def test_status_busy_when_a_live_lock_exists(h):
    h.ready("111")
    h.write_lock("w1", os.getpid())
    rc, out = h.run("status")
    assert "w1 (111): busy (slot tool run" in out


def test_status_reports_stale_lock_without_clearing_it(h):
    from conftest import dead_pid
    h.ready("111")
    h.write_lock("w1", dead_pid())
    rc, out = h.run("status")
    assert "stale lock" in out
    assert (h.locks / "w1.lock").exists()


# ---- review fixes: not open vs not answering, the port, BOM-safe replies ----------------------

def status_rows(h, *slots_ports):
    root = h.env["SLOT_ROOT"].replace("/", "\\")
    folder = {"w1": "111", "w2": "w2", "w3": "w3"}
    out = "".join(f"{port}\tready\t{root}\{folder[s]}\t6000.6.5f1\t4242\n" for s, port in slots_ports)
    h.set("_status", "status", {"out": out, "rc": 0 if out else 6})


def test_status_tells_not_open_from_not_answering(h):
    status_rows(h, ("w2", 7801))  # w1 and w3 are not in `unity status`
    h.ready("w2")
    rc, out = h.run("status")
    ls = lines(out)
    assert len(ls) == 3
    assert ls[0].startswith("w1 (111): not open")
    assert ls[2].startswith("w3: not open")
    assert h.run_tests_calls() == []
    # the Editors that are not open were not asked anything
    assert not [c for c in h.calls() if "--project-path" in c and c[c.index("--project-path") + 1].endswith(("111", "w3"))]


def test_status_prints_the_port_of_an_open_editor(h):
    status_rows(h, ("w1", 7811), ("w2", 7822), ("w3", 7833))
    h.ready("111")
    rc, out = h.run("status")
    ls = lines(out)
    assert "free" in ls[0] and "port 7811" in ls[0]
    assert "port 7822" in ls[1]
    assert "not answering" in ls[2] and "port 7833" in ls[2]


def test_status_not_answering_gives_a_short_reason(h):
    status_rows(h, ("w3", 7833))
    rc, out = h.run("status")
    w3 = [x for x in lines(out) if x.startswith("w3")][0]
    assert "not answering (Error: could not connect to the Editor)" in w3


def test_status_not_answering_timed_out(h):
    status_rows(h, ("w3", 7833))
    h.set("w3", "editor_status", reply(READY, sleep=4))
    h.env["SLOT_STATUS_TIMEOUT_S"] = "1"
    h.env["SLOT_STATUS_GRACE_S"] = "0.5"
    rc, out = h.run("status")
    w3 = [x for x in lines(out) if x.startswith("w3")][0]
    assert "not answering (timed out)" in w3


def test_status_reads_a_reply_with_a_byte_order_mark(h):
    status_rows(h, ("w2", 7822))
    bom = "\ufeff"
    h.set("w2", "editor_status", {"out": bom + __import__("json").dumps(READY), "rc": 0})
    h.set("w2", "test_status", {"out": bom + '{"status": "no_tests"}', "rc": 0})
    rc, out = h.run("status")
    assert [x for x in lines(out) if x.startswith("w2")][0].startswith("w2: free")
