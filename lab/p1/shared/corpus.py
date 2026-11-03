"""Fictional TEST producers; no sensor, Codex RPC or private file access."""
from datetime import datetime, timedelta, timezone

EPOCH = datetime(2030, 1, 1, tzinfo=timezone.utc)

def at(ms=0):
    return (EPOCH + timedelta(milliseconds=ms)).isoformat(timespec="milliseconds").replace("+00:00", "Z")

def metric(value, unit):
    return {"available": value is not None, "value": value, "unit": unit}

def link_sim(ms=0):
    return {"schemaVersion": 2, "sampleKind": "TEST", "generatedAt": at(ms), "telemetry": {
        "domain": "telemetry", "source": {"component": "LINK", "kind": "DIRECT"},
        "capturedAt": at(ms), "availability": "AVAILABLE", "freshness": "CURRENT",
        "maxAgeSeconds": 6, "error": None, "cpuPercent": metric(25, "percent"),
        "gpuPercent": metric(40, "percent"), "ramUsedGiB": metric(8, "GiB"),
        "gpuTemperatureC": metric(55, "celsius")}}

def periscope_sim(ms=0):
    return {"schemaVersion": 2, "sampleKind": "TEST", "generatedAt": at(ms), "codex": {
        "domain": "codex", "source": {"component": "PERISCOPE", "kind": "DIRECT"},
        "capturedAt": at(ms), "availability": "PARTIAL", "freshness": "CURRENT",
        "maxAgeSeconds": 900, "error": None, "fiveHour": {
            "remainingPercent": 75, "resetsAt": at(ms+1800000), "unit": "percent_remaining"},
        "weekly": None, "reliability": "DIRECT",
        "activity": {"state": "UNVERIFIED", "observedAt": None, "evidence": "NONE"}}}

def media_sim(ms=0):
    return {"schemaVersion": 2, "sampleKind": "TEST", "generatedAt": at(ms), "media": {
        "domain": "media", "source": {"component": "LINK", "kind": "DIRECT"},
        "capturedAt": at(ms), "availability": "AVAILABLE", "freshness": "CURRENT",
        "maxAgeSeconds": 10, "error": None, "state": "NO_SESSION", "player": None,
        "title": None, "artist": None, "album": None, "positionMs": None,
        "durationMs": None, "timelineUnit": "milliseconds",
        "artwork": {"available": False, "reference": None}}}

def network():
    return {"version": 1, "capturedAt": at(), "maxAgeSeconds": 6,
        "availability": "AVAILABLE", "freshness": "CURRENT", "error": None,
        "interfaceId": "TEST_ETHERNET", "interfaceType": "ETHERNET", "localIpv4": "192.0.2.10",
        "linkSpeedMbps": 1000, "rxBytes": 1000000, "txBytes": 500000,
        "windowSeconds": 1, "rxMbps": 8, "txMbps": 4}

def smalltv_supplement(ms=0):
    return {"sampleKind":"TEST", "capturedAt":at(ms), "gpu_vram_mb":2048, "gpu_power":80}
