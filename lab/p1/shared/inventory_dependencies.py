"""Inspect restored public package metadata; never reads user package configuration."""
import hashlib
import json
from pathlib import Path
import tomllib
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ns = {"n":"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}
runtime = {}
build = {}
for candidate in "ab":
    lock = json.loads((ROOT / f"candidate-{candidate}/packages.lock.json").read_text())
    runtime[candidate] = {name:entry["resolved"] for group in lock["dependencies"].values()
                         for name,entry in group.items() if entry["type"] != "Project"}
for project in (ROOT / ".cache/macro-deck/sdk/src").glob("*/obj/project.assets.json"):
    data = json.loads(project.read_text())
    for name, lib in data["libraries"].items():
        if lib["type"] == "package":
            package, version = name.rsplit("/",1)
            build[package] = version
packages = dict(build)
for group in runtime.values():
    packages.update(group)
entries = []
for name,version in sorted(packages.items(),key=lambda item:item[0].lower()):
    directory = ROOT / ".cache/nuget" / name.lower() / version.lower()
    nuspec = directory / (name.lower()+".nuspec")
    metadata = ET.fromstring(nuspec.read_bytes()).find("{*}metadata")
    license_node = metadata.find("{*}license")
    entry = {"id":name,"version":version,"licenseType":license_node.get("type") if license_node is not None else "UNKNOWN",
             "license":license_node.text if license_node is not None else "UNKNOWN",
             "runtimeCandidates":[c for c,group in runtime.items() if name.lower() in {k.lower() for k in group}],
             "nuspecSha256":hashlib.sha256(nuspec.read_bytes()).hexdigest()}
    if license_node is not None and license_node.get("type") == "file":
        license_file = directory / license_node.text
        entry["licenseFileSha256"] = hashlib.sha256(license_file.read_bytes()).hexdigest() if license_file.exists() else "MISSING"
    entries.append(entry)
macro = ROOT / ".cache/macro-deck"
lock_stats = []
for relative in ["ui/package-lock.json", "ui/bootstrapper/package-lock.json", "ui/bootstrapper/Cargo.lock"]:
    raw = (macro / relative).read_bytes()
    if relative.endswith(".json"):
        packages_data=json.loads(raw).get("packages",{})
        count=len(packages_data)
    else:
        count=len(tomllib.loads(raw.decode()).get("package",[]))
    lock_stats.append({"path":relative,"sha256":hashlib.sha256(raw).hexdigest(),"lockEntries":count,"installedInLab":False})
report = {"sampleKind":"TEST","macroDeckCommit":"614dc0bbb04be21434176208fadff45179a6d5ec",
          "runtimePackageCounts":{c:len(group) for c,group in runtime.items()},"packages":entries,
          "unbuiltUiLocks":lock_stats,"redistributionApproved":False}
(ROOT / "shared/dependencies.json").write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
print(json.dumps({"runtimePackageCounts":report["runtimePackageCounts"],"inspectedPackages":len(entries),"uiLocks":lock_stats}))
