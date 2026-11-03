"""Same black-box HTTP scenarios for A and the B SDK laboratory (not full Macro Deck)."""
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request
import unittest
from corpus import at, link_sim, periscope_sim, media_sim, network, smalltv_supplement

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--candidate", choices=["a", "b"], required=True)
parser.add_argument("--relay-schema", required=True)
parser.add_argument("--android-schema", required=True)
parser.add_argument("--report")
ARGS, EXTRA = parser.parse_known_args()

class CandidateTests(unittest.TestCase):
    def setUp(self):
        self.run_dir = ROOT / ".runtime" / f"test-{ARGS.candidate}-{self._testMethodName}"
        self.run_dir.mkdir(parents=True, exist_ok=True)
        # Never recycle a run's watermarks. Use a new directory for every execution.
        self.state = self.run_dir / str(time.time_ns())
        self.state.mkdir()
        self.addCleanup(self.cleanup)
        self.start()

    def start(self):
        (self.state / "ready.json").unlink(missing_ok=True)
        dll = ROOT / f"candidate-{ARGS.candidate}" / "bin/Release" / ("net10.0-windows" if ARGS.candidate == "a" else "net10.0") / f"Candidate{ARGS.candidate.upper()}.dll"
        self.assertTrue(dll.is_file(), f"Candidate not built: {dll.name}")
        self.log = open(self.state / "process.log", "a", encoding="utf-8")
        env = os.environ.copy()
        env.update(DOTNET_CLI_HOME=str(ROOT / ".cache/cli"), DOTNET_CLI_TELEMETRY_OPTOUT="1",
                   DOTNET_GENERATE_ASPNET_CERTIFICATE="false", TEMP=str(self.state), TMP=str(self.state))
        self.proc = subprocess.Popen([str(ROOT / ".cache/dotnet/dotnet.exe"), str(dll),
            str(self.state), str(Path(ARGS.relay_schema).resolve()), str(Path(ARGS.android_schema).resolve())],
            cwd=ROOT, env=env, stdout=self.log, stderr=self.log,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        for _ in range(200):
            if (self.state / "ready.json").exists():
                self.url = json.loads((self.state / "ready.json").read_text())["url"]
                self.assertTrue(self.url.startswith("http://127.0.0.1:"))
                return
            if self.proc.poll() is not None:
                self.fail("Candidate exited before readiness; see ignored process.log")
            time.sleep(.05)
        self.fail("Candidate readiness timeout")

    def cleanup(self):
        if not hasattr(self, "proc"):
            return
        if self.proc.poll() is None:
            try:
                self.call({"op": "shutdown"})
                self.proc.wait(timeout=8)
            except Exception:
                self.proc.kill()
                self.proc.wait(timeout=5)
                raise
        self.log.close()

    def call(self, body, role="ADMIN", status=200):
        if isinstance(body, dict) and body.get("op") == "smalltv" and "supplement" not in body:
            # Independent LINK_SIM numeric fields, absent from the HUB telemetry schema.
            supplement = smalltv_supplement()
            supplement["capturedAt"] = body["now"]
            body = dict(body, supplement=supplement)
        raw = body if isinstance(body, bytes) else json.dumps(body, separators=(",", ":")).encode()
        req = urllib.request.Request(self.url + "/lab", raw,
            {"Content-Type": "application/json", "Authorization": "Bearer TEST_" + role})
        try:
            response = urllib.request.urlopen(req, timeout=10)
        except urllib.error.HTTPError as e:
            response = e
        self.assertEqual(response.status, status, response.read().decode() if response.status != status else "")
        return json.load(response)

    def publish(self, doc, role="LINK", status=200):
        return self.call({"op": "publish", "now": doc["generatedAt"], "schema": "relay", "document": doc}, role, status)

    def snapshot(self, ms=0):
        return self.call({"op": "snapshot", "now": at(ms)}, "READER")["snapshot"]

    def test_two_sources_two_clients_and_absence(self):
        self.publish(link_sim())
        self.publish(periscope_sim(), "PERISCOPE")
        snap = self.snapshot()
        self.assertEqual(snap["sampleKind"], "TEST")
        self.assertEqual(snap["telemetry"]["cpuPercent"]["value"], 25)
        self.assertIsNone(snap["codex"]["weekly"])
        self.assertEqual(snap["codex"]["activity"]["state"], "UNVERIFIED")
        projection = self.call({"op": "smalltv", "now": at(), "domain": "telemetry"}, "READER")
        numeric = json.loads(projection["body"])
        self.assertEqual(numeric["cpu_usage"], 25)
        self.assertTrue(16 <= len(projection["body"]) <= 384)
        self.assertNotIn("codex", numeric)
        self.call({"op": "smalltv", "now": at(), "domain": "codex"}, "READER", 400)
        self.call({"op": "snapshot", "now": at()}, "INVALID", 403)

    def test_exact_ttl_boundaries_and_capture_not_rebased(self):
        for doc, role, domain, ttl in [(link_sim(), "LINK", "telemetry", 6000),
                (media_sim(), "LINK", "media", 10000), (periscope_sim(), "PERISCOPE", "codex", 900000)]:
            self.publish(doc, role)
            for ms, expected in [(ttl-1,"CURRENT"), (ttl,"STALE"), (ttl+1,"STALE")]:
                data = self.snapshot(ms)[domain]
                self.assertEqual(data["freshness"], expected)
                self.assertEqual(data["capturedAt"], at())

    def test_null_sensor_is_not_zero(self):
        doc = link_sim()
        doc["telemetry"]["gpuPercent"].update(available=False, value=None)
        doc["telemetry"]["availability"] = "PARTIAL"
        self.publish(doc)
        self.assertIsNone(self.snapshot()["telemetry"]["gpuPercent"]["value"])
        self.call({"op":"smalltv", "now":at(), "domain":"telemetry"}, "READER", 400)

    def test_unknown_and_quota_reset(self):
        doc = periscope_sim()
        doc["codex"]["fiveHour"]["resetsAt"] = at(5000)
        self.publish(doc, "PERISCOPE")
        self.assertEqual(self.snapshot(4999)["codex"]["freshness"], "CURRENT")
        self.assertEqual(self.snapshot(5000)["codex"]["freshness"], "STALE")
        doc = link_sim()
        t = doc["telemetry"]
        t.update(capturedAt=None, availability="UNAVAILABLE", freshness="UNKNOWN", error={"code":"NO_DATA"})
        for k in ["cpuPercent","gpuPercent","ramUsedGiB","gpuTemperatureC"]:
            t[k].update(available=False, value=None)
        self.publish(doc)
        self.assertEqual(self.snapshot(6000)["telemetry"]["freshness"], "UNKNOWN")

    def test_replay_conflict_and_temporal_rejection(self):
        self.assertEqual(self.publish(link_sim())["outcome"], "CHANGED")
        self.assertEqual(self.publish(link_sim())["outcome"], "IDEMPOTENT")
        doc = link_sim()
        doc["telemetry"]["cpuPercent"]["value"] = 26
        self.publish(doc, status=409)
        self.publish(link_sim(-1), status=409)
        doc = link_sim(1)
        doc["generatedAt"] = at()
        self.publish(doc, status=400)
        doc = link_sim(1)
        doc["telemetry"]["freshness"] = "STALE"
        self.publish(doc, status=400)
        self.assertEqual(self.snapshot()["telemetry"]["cpuPercent"]["value"], 25)

    def test_schema_network_matrix(self):
        for schema in ["relay", "android"]:
            self.call({"op":"validate", "schema":schema, "document":link_sim()}, "ADMIN")
        doc = link_sim()
        doc["telemetry"]["network"] = network()
        self.call({"op":"validate", "schema":"relay", "document":doc}, status=400)
        self.call({"op":"validate", "schema":"android", "document":doc})
        doc["telemetry"]["network"]["version"] = 999
        self.call({"op":"validate", "schema":"android", "document":doc}, status=400)
        doc["telemetry"]["network"] = "invalid"
        self.call({"op":"validate", "schema":"android", "document":doc}, status=400)

    def test_roles_duplicate_keys_nonfinite_and_capacity(self):
        self.publish(link_sim(), "READER", 403)
        self.publish(link_sim(), "PERISCOPE", 403)
        self.call(b'{"op":"snapshot","op":"shutdown"}', status=400)
        self.call(b'{"op":"snapshot","now":NaN}', status=400)
        doc = link_sim()
        doc["telemetry"]["cpuPercent"]["value"] = 101
        self.publish(doc, status=400)
        self.call(b'{"op":"snapshot","padding":"' + b'x'*263000 + b'"}', status=413)

    def test_event_command_ack_result_and_stop_isolation(self):
        self.publish(link_sim())
        self.publish(periscope_sim(), "PERISCOPE")
        command = {"op":"command", "id":"TEST-C1", "target":"LINK_SIM", "action":"stop"}
        self.call(command, "READER", 403)
        ack = self.call(command)
        self.assertEqual(ack["ack"], "ACCEPTED")
        self.assertEqual(ack["result"], "PENDING")
        self.assertEqual(self.call(command), ack)
        changed = dict(command, target="PERISCOPE_SIM")
        self.call(changed, status=409)
        self.call({"op":"complete", "id":"TEST-C1"})
        self.assertEqual(self.call({"op":"result", "id":"TEST-C1"})["result"], "COMPLETED")
        self.assertEqual(self.snapshot(6000)["telemetry"]["freshness"], "STALE")
        self.assertEqual(self.snapshot(6000)["codex"]["freshness"], "CURRENT")
        self.publish(link_sim(7000), status=409)
        events = self.call({"op":"events"}, "READER")["events"]
        self.assertTrue(any(e["version"] == 1 and e["name"] == "module.stopped" for e in events))

    def test_smalltv_strict_receive_threshold_and_bounds(self):
        self.publish(link_sim())
        good = self.call({"op":"smalltv", "now":at(), "domain":"telemetry"}, "READER")["body"]
        for ms, stale in [(5999,False),(6000,False),(6001,True)]:
            self.assertEqual(self.call({"op":"receive", "body":good, "elapsedMs":ms}, "READER")["stale"], stale)
        for body in ["{}", "x"*385, json.dumps(link_sim()), good.replace('"cpu_usage":25', '"cpu_usage":101')]:
            self.call({"op":"receive", "body":body, "elapsedMs":0}, "READER", 400)
        doc = link_sim(1)
        doc["telemetry"]["gpuTemperatureC"]["value"] = 131
        self.publish(doc)
        self.call({"op":"smalltv", "now":at(1), "domain":"telemetry"}, "READER", 400)

    def test_third_module_extension(self):
        self.publish(link_sim())
        self.publish(periscope_sim(), "PERISCOPE")
        self.call({"op":"extension"})
        self.assertEqual(self.call({"op":"modules"}, "READER")["modules"]["CLOCK_SIM"], "RUNNING")
        self.assertEqual(self.snapshot()["codex"]["fiveHour"]["remainingPercent"], 75)
        if ARGS.candidate == "b":
            sdk = self.call({"op":"sdk"}, "READER")
            transported = json.loads(sdk["sdkReply"]["value"]["text"])
            self.assertEqual(transported, self.snapshot())
            self.assertEqual(transported["telemetry"]["capturedAt"], at())

    def test_network_semantics_and_cached_expiration(self):
        doc = link_sim()
        doc["telemetry"]["network"] = network()
        self.call({"op":"publish", "now":at(), "schema":"android", "document":doc}, "LINK")
        self.assertEqual(self.snapshot(6000)["telemetry"]["network"]["freshness"], "STALE")
        doc["telemetry"]["network"]["capturedAt"] = at(1)
        self.call({"op":"validate", "schema":"android", "document":doc}, status=400)

    def test_smalltv_supplement_must_match_source_capture(self):
        self.publish(link_sim())
        self.call({"op":"smalltv", "now":at(), "domain":"telemetry", "supplement":{
            "sampleKind":"TEST", "capturedAt":at(-1), "gpu_vram_mb":2048, "gpu_power":80}}, "READER", 400)

    def test_restart_preserves_watermarks_and_pending_uncertainty(self):
        self.publish(link_sim())
        command = {"op":"command", "id":"TEST-PENDING", "target":"LINK_SIM", "action":"stop"}
        self.call(command)
        self.cleanup()
        self.start()
        self.assertEqual(self.publish(link_sim())["outcome"], "IDEMPOTENT")
        self.publish(link_sim(-1), status=409)
        self.assertEqual(self.call({"op":"result", "id":"TEST-PENDING"})["result"], "UNKNOWN_NO_RETRY")
        self.call({"op":"complete", "id":"TEST-PENDING"}, status=409)
        self.assertEqual(self.call({"op":"modules"}, "READER")["modules"]["LINK_SIM"], "RUNNING")

    def test_artwork_limits_timeline_and_version(self):
        doc = media_sim()
        doc["media"]["positionMs"] = 100
        doc["media"]["durationMs"] = 50
        self.publish(doc, status=400)
        doc = media_sim()
        doc["media"]["artwork"] = {"available":True,"reference":{
            "kind":"CONTENT_ID","id":"0"*64,"mediaType":"image/jpeg", "bytes":40961,"width":192,"height":192}}
        self.publish(doc, status=400)
        doc["media"]["artwork"]["reference"]["bytes"] = 40960
        self.publish(doc)
        doc = link_sim()
        doc["schemaVersion"] = 1
        self.publish(doc, status=400)

if __name__ == "__main__":
    digest = hashlib.sha256(json.dumps([link_sim(),periscope_sim(),media_sim(),network(),smalltv_supplement()],sort_keys=True).encode()).hexdigest()
    print("CORPUS_SHA256=" + digest, flush=True)
    started = time.monotonic()
    program = unittest.main(argv=[__file__]+EXTRA, verbosity=2, exit=False)
    result = program.result
    if ARGS.report:
        def sha(path):
            return hashlib.sha256(Path(path).read_bytes()).hexdigest()
        report = {"sampleKind":"TEST", "candidate":ARGS.candidate,
            "boundary":"ASP_NET_HTTP" if ARGS.candidate=="a" else "COMMON_ADAPTER_AND_MACRO_DECK_SDK_TEST_HOST",
            "testsRun":result.testsRun, "failures":len(result.failures), "errors":len(result.errors),
            "skipped":len(result.skipped), "functionalDurationSeconds":round(time.monotonic()-started,3),
            "benchmark":False,"corpusSha256":digest,"corpusSourceSha256":sha(Path(__file__).with_name("corpus.py")),
            "testSourceSha256":sha(__file__),"schemas":{"relay":sha(ARGS.relay_schema),"android":sha(ARGS.android_schema)},
            "failedTests":[test.id().split(".")[-1] for test,_ in result.failures+result.errors]}
        Path(ARGS.report).write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
    raise SystemExit(0 if result.wasSuccessful() else 1)
