"""Optional Phase 6 TCP smoke test against a bridge on a disposable port.

Start the built bridge with DESWIK_BRIDGE_PORT=9596, then run this script with
--port 9596. A fake read-only add-in holds its reply beyond the direct 15 s
ceiling; no Deswik process or drawing is touched.
"""
import argparse
import json
import socket
import threading
import time
import uuid


def request(port, action, params):
    with socket.create_connection(("127.0.0.1", port), timeout=3) as connection:
        connection.settimeout(5)
        wire = connection.makefile("rw", encoding="utf-8", newline="\n")
        wire.write(json.dumps({"id": str(uuid.uuid4()), "action": action, "params": params}) + "\n")
        wire.flush()
        return json.loads(wire.readline())


def fake_addin(port, ready, delay):
    with socket.create_connection(("127.0.0.1", port), timeout=3) as connection:
        connection.settimeout(delay + 10)
        wire = connection.makefile("rw", encoding="utf-8", newline="\n")
        wire.write(json.dumps({
            "id": str(uuid.uuid4()), "action": "register_addin",
            "params": {"name": "phase6-fake-read", "capabilities": ["get_cad_elements"]},
        }) + "\n")
        wire.flush()
        assert json.loads(wire.readline())["success"]
        ready.set()
        forwarded = json.loads(wire.readline())
        assert forwarded["action"] == "get_cad_elements"
        time.sleep(delay)
        wire.write(json.dumps({
            "id": forwarded["id"], "action": "get_cad_elements_result",
            "data": {"count": 40, "figures": []},
        }) + "\n")
        wire.flush()
        time.sleep(0.2)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=9596)
    parser.add_argument("--delay", type=float, default=16)
    args = parser.parse_args()
    ready = threading.Event()
    addin = threading.Thread(target=fake_addin, args=(args.port, ready, args.delay), daemon=True)
    addin.start()
    assert ready.wait(5), "fake addin did not register"

    refused = request(args.port, "job.submit", {"action": "draw_cad_ugdrillholes"})
    assert not refused["success"] and refused["errorCode"] == "job_action_refused"
    start = time.monotonic()
    submitted = request(args.port, "job.submit", {
        "action": "get_cad_elements", "args": {"limit": 40},
    })
    assert submitted["success"], submitted
    assert time.monotonic() - start < 5, "job submission waited for addin result"
    job_id = submitted["data"]["JobId"]
    polled = request(args.port, "job.get", {"jobId": job_id})
    assert polled["success"] and polled["data"]["State"] in ("queued", "running")

    addin.join(args.delay + 10)
    finished = request(args.port, "job.get", {"jobId": job_id})
    assert finished["success"] and finished["data"]["State"] == "completed", finished
    assert finished["data"]["Result"]["count"] == 40
    unknown = request(args.port, "job.get", {"jobId": str(uuid.uuid4())})
    assert not unknown["success"] and unknown["errorCode"] == "job_unknown"
    if args.delay > 15:
        print("PASS Phase 6 TCP submit returned immediately and completed after the 15 s direct ceiling")
    else:
        print("PASS Phase 6 TCP submit returned immediately and completed")
    print("PASS unfenced job action refused and unknown job ID failed closed")


if __name__ == "__main__":
    main()
