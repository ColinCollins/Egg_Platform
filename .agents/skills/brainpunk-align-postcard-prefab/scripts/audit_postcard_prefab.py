#!/usr/bin/env python3
"""Read-only structural audit for BrainPunk Unity postcard prefabs."""

from __future__ import annotations

import argparse
import collections
import hashlib
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


HEADER_RE = re.compile(r"^--- !u!(?P<class_id>\d+) &(?P<file_id>-?\d+)(?P<stripped> stripped)?\r?$", re.MULTILINE)
NAME_RE = re.compile(r"^  m_Name: (?P<value>.*)\r?$", re.MULTILINE)
SCRIPT_RE = re.compile(
    r"^  m_Script: \{fileID: 11500000, guid: (?P<guid>[0-9a-f]{32}), type: 3\}\r?$",
    re.MULTILINE,
)
SOURCE_PREFAB_RE = re.compile(
    r"^  m_SourcePrefab: \{fileID: 100100000, guid: (?P<guid>[0-9a-f]{32}), type: 3\}\r?$",
    re.MULTILINE,
)
GAME_OBJECT_RE = re.compile(r"^  m_GameObject: \{fileID: (?P<file_id>-?\d+)\}\r?$", re.MULTILINE)
FATHER_RE = re.compile(r"^  m_Father: \{fileID: (?P<file_id>-?\d+)\}\r?$", re.MULTILINE)
COMPONENT_RE = re.compile(r"^  - component: \{fileID: (?P<file_id>-?\d+)\}\r?$", re.MULTILINE)
GUID_LINE_RE = re.compile(r"guid: (?P<guid>[0-9a-f]{32})")


CLASS_NAMES = {
    1: "GameObject",
    4: "Transform",
    95: "Animator",
    114: "MonoBehaviour",
    222: "CanvasRenderer",
    223: "Canvas",
    224: "RectTransform",
    225: "CanvasGroup",
    1001: "PrefabInstance",
}


@dataclass(frozen=True)
class UnityDocument:
    class_id: int
    file_id: int
    stripped: bool
    body: str


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_documents(path: Path) -> list[UnityDocument]:
    text = path.read_text(encoding="utf-8-sig")
    matches = list(HEADER_RE.finditer(text))
    if not matches:
        raise ValueError(f"No Unity YAML documents found: {path}")

    documents: list[UnityDocument] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        documents.append(
            UnityDocument(
                class_id=int(match.group("class_id")),
                file_id=int(match.group("file_id")),
                stripped=bool(match.group("stripped")),
                body=text[match.end() : end],
            )
        )
    return documents


def build_meta_index(project_root: Path) -> dict[str, str]:
    index: dict[str, str] = {}
    assets_root = project_root / "Assets"
    if not assets_root.is_dir():
        return index

    for meta_path in assets_root.rglob("*.meta"):
        try:
            with meta_path.open("r", encoding="utf-8-sig", errors="ignore") as stream:
                for _ in range(12):
                    line = stream.readline()
                    if not line:
                        break
                    if line.startswith("guid: "):
                        guid = line[6:].strip()
                        if re.fullmatch(r"[0-9a-f]{32}", guid):
                            asset_path = meta_path.with_suffix("")
                            index[guid] = asset_path.relative_to(project_root).as_posix()
                        break
        except OSError:
            continue
    return index


def component_signature(document: UnityDocument) -> str:
    class_name = CLASS_NAMES.get(document.class_id, str(document.class_id))
    if document.class_id != 114:
        return class_name
    match = SCRIPT_RE.search(document.body)
    return f"MonoBehaviour:{match.group('guid') if match else 'unknown'}"


def root_components(documents: list[UnityDocument]) -> list[str]:
    by_id = {document.file_id: document for document in documents}
    game_objects = {document.file_id: document for document in documents if document.class_id == 1 and not document.stripped}
    root_game_object_ids: list[int] = []

    for document in documents:
        if document.class_id not in (4, 224) or document.stripped:
            continue
        father = FATHER_RE.search(document.body)
        game_object = GAME_OBJECT_RE.search(document.body)
        if father and game_object and int(father.group("file_id")) == 0:
            root_game_object_ids.append(int(game_object.group("file_id")))

    signatures: list[str] = []
    for game_object_id in root_game_object_ids:
        game_object = game_objects.get(game_object_id)
        if not game_object:
            continue
        for match in COMPONENT_RE.finditer(game_object.body):
            component = by_id.get(int(match.group("file_id")))
            if component:
                signatures.append(component_signature(component))
    return signatures


def prefab_summary(path: Path, project_root: Path, meta_index: dict[str, str]) -> dict[str, Any]:
    documents = parse_documents(path)
    class_counts = collections.Counter(document.class_id for document in documents)
    script_counts = collections.Counter(
        match.group("guid")
        for document in documents
        if document.class_id == 114
        for match in [SCRIPT_RE.search(document.body)]
        if match
    )
    source_prefab_counts = collections.Counter(
        match.group("guid")
        for document in documents
        if document.class_id == 1001
        for match in [SOURCE_PREFAB_RE.search(document.body)]
        if match
    )
    names = [
        match.group("value")
        for document in documents
        if document.class_id == 1 and not document.stripped
        for match in [NAME_RE.search(document.body)]
        if match
    ]
    all_external_guids = collections.Counter(
        match.group("guid")
        for document in documents
        for match in GUID_LINE_RE.finditer(document.body)
    )

    def resolved(counter: collections.Counter[str]) -> list[dict[str, Any]]:
        return [
            {"guid": guid, "count": count, "asset": meta_index.get(guid, "unresolved")}
            for guid, count in sorted(counter.items(), key=lambda item: (-item[1], item[0]))
        ]

    relative_path = path.relative_to(project_root).as_posix()
    return {
        "path": relative_path,
        "sha256": sha256(path),
        "document_count": len(documents),
        "class_counts": {
            CLASS_NAMES.get(class_id, str(class_id)): count
            for class_id, count in sorted(class_counts.items())
        },
        "local_game_object_count": sum(
            1 for document in documents if document.class_id == 1 and not document.stripped
        ),
        "stripped_document_count": sum(1 for document in documents if document.stripped),
        "root_components": root_components(documents),
        "script_dependencies": resolved(script_counts),
        "nested_prefab_dependencies": resolved(source_prefab_counts),
        "external_guid_count": len(all_external_guids),
        "names": names,
    }


def dependency_map(summary: dict[str, Any], key: str) -> dict[str, dict[str, Any]]:
    return {entry["guid"]: entry for entry in summary[key]}


def compare(reference: dict[str, Any], target: dict[str, Any]) -> dict[str, Any]:
    reference_scripts = dependency_map(reference, "script_dependencies")
    target_scripts = dependency_map(target, "script_dependencies")
    reference_prefabs = dependency_map(reference, "nested_prefab_dependencies")
    target_prefabs = dependency_map(target, "nested_prefab_dependencies")

    missing_root_components = list(
        (collections.Counter(reference["root_components"]) - collections.Counter(target["root_components"])).elements()
    )
    extra_root_components = list(
        (collections.Counter(target["root_components"]) - collections.Counter(reference["root_components"])).elements()
    )

    return {
        "missing_reference_script_dependencies": [
            reference_scripts[guid] for guid in sorted(reference_scripts.keys() - target_scripts.keys())
        ],
        "missing_reference_nested_prefabs": [
            reference_prefabs[guid] for guid in sorted(reference_prefabs.keys() - target_prefabs.keys())
        ],
        "root_components_missing_from_target": missing_root_components,
        "root_components_extra_in_target": extra_root_components,
        "reference_prefab_instance_count": reference["class_counts"].get("PrefabInstance", 0),
        "target_prefab_instance_count": target["class_counts"].get("PrefabInstance", 0),
        "reference_local_game_objects": reference["local_game_object_count"],
        "target_local_game_objects": target["local_game_object_count"],
        "interpretation": [
            "Missing dependencies and root components are direct serialized-structure differences.",
            "Different object or prefab-instance counts may be intentional when themes use different puzzle-piece counts.",
            "Unity Prefab Mode and PlayMode validation are still required; this audit does not resolve nested prefab contents.",
        ],
    }


def resolve_prefab_path(project_root: Path, value: str) -> Path:
    candidate = Path(value)
    if not candidate.is_absolute():
        candidate = project_root / candidate
    candidate = candidate.resolve()
    assets_root = (project_root / "Assets").resolve()
    try:
        candidate.relative_to(assets_root)
    except ValueError as error:
        raise ValueError(f"Prefab must be inside project Assets/: {candidate}") from error
    if candidate.suffix.lower() != ".prefab" or not candidate.is_file():
        raise ValueError(f"Prefab not found: {candidate}")
    return candidate


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", required=True, help="Unity project root containing Assets/")
    parser.add_argument("--reference", required=True, help="Reference prefab path, absolute or project-relative")
    parser.add_argument("--target", required=True, help="Target prefab path, absolute or project-relative")
    parser.add_argument("--json-out", help="Optional JSON report path; prefabs are never modified")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = Path(args.project_root).resolve()
    if not (project_root / "Assets").is_dir():
        raise ValueError(f"Unity project root does not contain Assets/: {project_root}")

    reference_path = resolve_prefab_path(project_root, args.reference)
    target_path = resolve_prefab_path(project_root, args.target)
    if reference_path == target_path:
        raise ValueError("Reference and target prefab must be different files")

    meta_index = build_meta_index(project_root)
    reference = prefab_summary(reference_path, project_root, meta_index)
    target = prefab_summary(target_path, project_root, meta_index)
    report = {
        "reference": reference,
        "target": target,
        "comparison": compare(reference, target),
    }
    rendered = json.dumps(report, ensure_ascii=False, indent=2)
    print(rendered)

    if args.json_out:
        output = Path(args.json_out).resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(2)
