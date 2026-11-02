#!/usr/bin/env python3
"""Generate and validate IWanner Unity prefabs from an accepted YAML template."""

from __future__ import annotations

import argparse
import json
import re
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path


ACTOR_GUID = "788a6cfafda262d4299d1eb405285ff2"
DOOR_GUID = "1a2ad5397db19e040bbd2b8226c6571f"
ACTOR_TRANSFORM_SOURCE_ID = "4236403131785930328"
DOOR_TRANSFORM_SOURCE_ID = "9135075361025361483"
DOC_PATTERN = re.compile(r"(?m)^--- !u!(\d+) &([^\s]+)( stripped)?\r?\n")
FILE_ID_PATTERN = re.compile(r"\{fileID: (-?\d+)\}")
DEFINED_ID_PATTERN = re.compile(r"(?m)^--- !u!\d+ &([^\s]+)")


@dataclass
class Document:
    unity_type: str
    file_id: str
    stripped: str
    body: str

    def render(self) -> str:
        return f"--- !u!{self.unity_type} &{self.file_id}{self.stripped}\n{self.body}"


def read_utf8(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig").replace("\r\n", "\n")


def parse_documents(text: str) -> tuple[str, list[Document]]:
    matches = list(DOC_PATTERN.finditer(text))
    if not matches:
        raise ValueError("Unity YAML document headers were not found")
    prefix = text[: matches[0].start()]
    documents = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        documents.append(
            Document(match.group(1), match.group(2), match.group(3) or "", text[match.end() : end])
        )
    return prefix, documents


def game_object_name(document: Document) -> str | None:
    if document.unity_type != "1":
        return None
    match = re.search(r"(?m)^  m_Name: (.*)$", document.body)
    return match.group(1) if match else None


def component_ids(document: Document) -> list[str]:
    match = re.search(r"(?ms)^  m_Component:\n(.*?)(?=^  m_Layer:)", document.body)
    return re.findall(r"fileID: (-?\d+)", match.group(1)) if match else []


def replace_field(body: str, field: str, value: str, indent: str = "  ") -> str:
    pattern = re.compile(rf"(?m)^{re.escape(indent + field + ': ')}.*$")
    updated, count = pattern.subn(f"{indent}{field}: {value}", body, count=1)
    if count != 1:
        raise ValueError(f"field not found exactly once: {field}")
    return updated


def set_modification(body: str, source_id: str, guid: str, property_path: str, value: float) -> str:
    pattern = re.compile(
        rf"(- target: \{{fileID: {source_id}, guid: {guid}, type: 3\}}\n"
        rf"      propertyPath: {re.escape(property_path)}\n"
        rf"      value: )[^\n]*"
    )
    updated, count = pattern.subn(rf"\g<1>{format_number(value)}", body, count=1)
    if count != 1:
        raise ValueError(f"prefab modification not found: {guid} {property_path}")
    return updated


def modification_value(body: str, source_id: str, guid: str, property_path: str) -> float | None:
    match = re.search(
        rf"- target: \{{fileID: {source_id}, guid: {guid}, type: 3\}}\n"
        rf"      propertyPath: {re.escape(property_path)}\n"
        rf"      value: ([^\n]*)",
        body,
    )
    return float(match.group(1)) if match else None


def format_number(value: float) -> str:
    text = f"{float(value):.6f}".rstrip("0").rstrip(".")
    return "0" if text == "-0" else text


def make_ids(level_number: int, index: int) -> tuple[str, str, str, str]:
    prefix = 8_000_000_000_000_000_000 + level_number * 1_000_000_000 + index * 10_000
    return tuple(str(prefix + offset) for offset in (1, 2, 3, 4))


def ground_name(index: int) -> str:
    return "Ground" if index == 0 else f"Ground ({index})"


def clone_ground(template_docs: list[Document], ids: tuple[str, str, str, str], index: int, spec: dict) -> list[Document]:
    old_ids = [document.file_id for document in template_docs]
    replacements = dict(zip(old_ids, ids))
    clones = []
    for document in template_docs:
        body = document.body
        for old_id, new_id in replacements.items():
            body = re.sub(rf"(?<!\d){old_id}(?!\d)", new_id, body)
        clones.append(Document(document.unity_type, replacements[document.file_id], document.stripped, body))

    game_object, transform, sprite_renderer, collider = clones
    game_object.body = replace_field(game_object.body, "m_Name", ground_name(index))
    game_object.body = replace_field(game_object.body, "m_Layer", "6")
    transform.body = replace_field(
        transform.body,
        "m_LocalPosition",
        f"{{x: {format_number(spec['x'])}, y: {format_number(spec['y'])}, z: 0}}",
    )
    size = f"{{x: {format_number(spec['width'])}, y: {format_number(spec['height'])}}}"
    sprite_renderer.body = replace_field(sprite_renderer.body, "m_Size", size)
    collider.body = replace_field(collider.body, "newSize", size, indent="    ")
    collider.body = replace_field(collider.body, "m_Size", size)
    return clones


def find_ground_template(documents: list[Document]) -> tuple[list[Document], set[str]]:
    game_objects = {document.file_id: document for document in documents if document.unity_type == "1"}
    ground_game_objects = {
        file_id for file_id, document in game_objects.items() if re.fullmatch(r"Ground(?: \(\d+\))?", game_object_name(document) or "")
    }
    if not ground_game_objects:
        raise ValueError("template does not contain a Ground object")
    first_game_object_id = next(document.file_id for document in documents if document.file_id in ground_game_objects)
    first_game_object = game_objects[first_game_object_id]
    ids = component_ids(first_game_object)
    grouped = [first_game_object]
    for expected_type, component_id in zip(("4", "212", "61"), ids):
        document = next((item for item in documents if item.file_id == component_id), None)
        if document is None or document.unity_type != expected_type:
            raise ValueError("Ground template component contract does not match Transform/SpriteRenderer/BoxCollider2D")
        grouped.append(document)
    remove_ids = set(ground_game_objects)
    game_object_pattern = "|".join(re.escape(item) for item in ground_game_objects)
    for document in documents:
        if re.search(r"m_GameObject: \{fileID: (?:" + game_object_pattern + r")\}", document.body):
            remove_ids.add(document.file_id)
    return grouped, remove_ids


def update_scene_children(documents: list[Document], child_ids: list[str]) -> None:
    scene_go = next((document for document in documents if game_object_name(document) == "Scene"), None)
    if scene_go is None:
        raise ValueError("Scene GameObject was not found")
    scene_transform_id = component_ids(scene_go)[0]
    scene_transform = next(document for document in documents if document.file_id == scene_transform_id)
    replacement = "  m_Children:\n" + "".join(f"  - {{fileID: {child_id}}}\n" for child_id in child_ids)
    scene_transform.body, count = re.subn(
        r"(?ms)^  m_Children:\n.*?(?=^  m_Father:)", replacement, scene_transform.body, count=1
    )
    if count != 1:
        raise ValueError("Scene.m_Children was not found")


def scene_transform(documents: list[Document]) -> Document:
    scene_go = next((document for document in documents if game_object_name(document) == "Scene"), None)
    if scene_go is None:
        raise ValueError("Scene GameObject was not found")
    scene_transform_id = component_ids(scene_go)[0]
    return next(document for document in documents if document.file_id == scene_transform_id)


def transform_child_ids(document: Document) -> list[str]:
    match = re.search(r"(?ms)^  m_Children:\n(.*?)(?=^  m_Father:)", document.body)
    return re.findall(r"fileID: (-?\d+)", match.group(1)) if match else []


def collect_scene_content_ids(documents: list[Document]) -> set[str]:
    """Collect the template Scene children so level-specific objects cannot leak into generated prefabs."""
    by_id = {document.file_id: document for document in documents}
    remove_ids: set[str] = set()

    def collect_transform(transform_id: str) -> None:
        if transform_id in remove_ids:
            return
        transform = by_id.get(transform_id)
        if transform is None:
            raise ValueError(f"Scene child transform was not found: {transform_id}")

        prefab_match = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)\}", transform.body)
        prefab_instance_id = prefab_match.group(1) if prefab_match else "0"
        if transform.stripped and prefab_instance_id != "0":
            remove_ids.add(prefab_instance_id)
            prefab_document_ids = {
                document.file_id
                for document in documents
                if re.search(rf"m_PrefabInstance: \{{fileID: {re.escape(prefab_instance_id)}\}}", document.body)
            }
            remove_ids.update(prefab_document_ids)
            prefab_game_object_ids = {
                document.file_id
                for document in documents
                if document.file_id in prefab_document_ids and document.unity_type == "1"
            }
            if prefab_game_object_ids:
                game_object_pattern = "|".join(re.escape(item) for item in prefab_game_object_ids)
                remove_ids.update(
                    document.file_id
                    for document in documents
                    if re.search(rf"m_GameObject: \{{fileID: (?:{game_object_pattern})\}}", document.body)
                )
            return

        for child_id in transform_child_ids(transform):
            collect_transform(child_id)

        game_object_match = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", transform.body)
        if game_object_match is None:
            raise ValueError(f"Scene child GameObject was not found for transform: {transform_id}")
        game_object_id = game_object_match.group(1)
        game_object = by_id.get(game_object_id)
        if game_object is None:
            raise ValueError(f"Scene child GameObject document was not found: {game_object_id}")
        remove_ids.add(game_object_id)
        remove_ids.update(component_ids(game_object))

    for child_id in transform_child_ids(scene_transform(documents)):
        collect_transform(child_id)
    return remove_ids


def update_reference_image(documents: list[Document], reference_name: str, reference_guid: str) -> None:
    game_objects = {document.file_id: document for document in documents if document.unity_type == "1"}
    reference = None
    sprite = None
    for game_object in game_objects.values():
        for component_id in component_ids(game_object):
            component = next((document for document in documents if document.file_id == component_id), None)
            if component and component.unity_type == "212" and re.search(
                r"m_Sprite: \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}", component.body
            ):
                reference, sprite = game_object, component
                break
        if reference:
            break
    if reference is None or sprite is None:
        raise ValueError("inactive reference image SpriteRenderer was not found")
    reference.body = replace_field(reference.body, "m_Name", reference_name)
    reference.body = replace_field(reference.body, "m_IsActive", "0")
    sprite.body, count = re.subn(
        r"m_Sprite: \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}",
        f"m_Sprite: {{fileID: 21300000, guid: {reference_guid}, type: 3}}",
        sprite.body,
        count=1,
    )
    if count != 1:
        raise ValueError("reference image GUID was not replaced")


def update_nested_positions(documents: list[Document], actor: dict, door: dict) -> None:
    actor_instance = next(
        document for document in documents if document.unity_type == "1001" and f"guid: {ACTOR_GUID}" in document.body
    )
    door_instance = next(
        document for document in documents if document.unity_type == "1001" and f"guid: {DOOR_GUID}" in document.body
    )
    actor_instance.body = set_modification(actor_instance.body, ACTOR_TRANSFORM_SOURCE_ID, ACTOR_GUID, "m_LocalPosition.x", actor["x"])
    actor_instance.body = set_modification(actor_instance.body, ACTOR_TRANSFORM_SOURCE_ID, ACTOR_GUID, "m_LocalPosition.y", actor["y"])
    door_instance.body = set_modification(door_instance.body, DOOR_TRANSFORM_SOURCE_ID, DOOR_GUID, "m_LocalPosition.x", door["x"])
    door_instance.body = set_modification(door_instance.body, DOOR_TRANSFORM_SOURCE_ID, DOOR_GUID, "m_LocalPosition.y", door["y"])


def reference_guid(project_root: Path, reference: str) -> str:
    meta_path = project_root / f"{reference}.meta"
    match = re.search(r"(?m)^guid: ([0-9a-f]{32})$", read_utf8(meta_path)) if meta_path.exists() else None
    if match is None:
        raise ValueError(f"reference image meta GUID is missing: {meta_path}")
    return match.group(1)


def build_prefab(project_root: Path, template_path: Path, level: dict, fixed_grounds: list[dict]) -> tuple[str, dict]:
    prefix, documents = parse_documents(read_utf8(template_path))
    ground_template, remove_ids = find_ground_template(documents)
    remove_ids.update(collect_scene_content_ids(documents))
    root = next(document for document in documents if game_object_name(document) == template_path.stem)
    root.body = replace_field(root.body, "m_Name", level["name"])
    update_reference_image(documents, Path(level["reference"]).stem, reference_guid(project_root, level["reference"]))
    update_nested_positions(documents, level["actor"], level["door"])

    all_grounds = [*fixed_grounds, *level["grounds"]]
    level_number = int(level["name"].split("_")[1])
    generated = []
    child_ids = []
    for index, ground in enumerate(all_grounds):
        ids = make_ids(level_number, index)
        generated.extend(clone_ground(ground_template, ids, index, ground))
        child_ids.append(ids[1])

    kept = [document for document in documents if document.file_id not in remove_ids]
    update_scene_children(kept, child_ids)
    scene_index = next(index for index, document in enumerate(kept) if game_object_name(document) == "Scene")
    kept[scene_index:scene_index] = generated
    text = prefix + "".join(document.render() for document in kept)
    report = {
        "name": level["name"],
        "fixedGrounds": len(fixed_grounds),
        "visibleGrounds": len(level["grounds"]),
        "totalGrounds": len(all_grounds),
        "actor": level["actor"],
        "door": level["door"],
        "reference": level["reference"],
    }
    return text, report


def make_meta(template_meta: str) -> str:
    updated, count = re.subn(r"(?m)^guid: [0-9a-f]{32}$", f"guid: {uuid.uuid4().hex}", template_meta, count=1)
    if count != 1:
        raise ValueError("template prefab meta GUID was not found")
    return updated


def ground_geometry_matches(documents: list[Document], expected: list[dict]) -> bool:
    by_id = {document.file_id: document for document in documents}
    for index, spec in enumerate(expected):
        game_object = next((document for document in documents if game_object_name(document) == ground_name(index)), None)
        if game_object is None:
            return False
        if "m_Layer: 6" not in game_object.body:
            return False
        ids = component_ids(game_object)
        if len(ids) < 3:
            return False
        transform, sprite_renderer, collider = (by_id.get(file_id) for file_id in ids[:3])
        if transform is None or sprite_renderer is None or collider is None:
            return False
        position = (
            f"m_LocalPosition: {{x: {format_number(spec['x'])}, "
            f"y: {format_number(spec['y'])}, z: 0}}"
        )
        size = f"{{x: {format_number(spec['width'])}, y: {format_number(spec['height'])}}}"
        if position not in transform.body:
            return False
        if f"m_Size: {size}" not in sprite_renderer.body:
            return False
        if f"newSize: {size}" not in collider.body or f"m_Size: {size}" not in collider.body:
            return False
    return True


def scene_contains_only_grounds(documents: list[Document], expected_count: int) -> bool:
    by_id = {document.file_id: document for document in documents}
    children = transform_child_ids(scene_transform(documents))
    if len(children) != expected_count:
        return False
    expected_names = [ground_name(index) for index in range(expected_count)]
    actual_names = []
    for child_id in children:
        transform = by_id.get(child_id)
        if transform is None or transform.stripped:
            return False
        game_object_match = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", transform.body)
        if game_object_match is None:
            return False
        game_object = by_id.get(game_object_match.group(1))
        if game_object is None:
            return False
        actual_names.append(game_object_name(game_object))
    return actual_names == expected_names


def validate_prefab(path: Path, level: dict, fixed_grounds: list[dict], project_root: Path) -> dict:
    text = read_utf8(path)
    _, documents = parse_documents(text)
    defined = DEFINED_ID_PATTERN.findall(text)
    counts = {}
    for file_id in defined:
        counts[file_id] = counts.get(file_id, 0) + 1
    duplicates = sorted(file_id for file_id, count in counts.items() if count > 1)
    referenced = set(FILE_ID_PATTERN.findall(text)) - {"0"}
    missing = sorted(referenced - set(defined))
    grounds = len(re.findall(r"(?m)^  m_Name: Ground(?: \(\d+\))?$", text))
    expected_grounds = len(fixed_grounds) + len(level["grounds"])
    expected_geometry = [*fixed_grounds, *level["grounds"]]
    expected_guid = reference_guid(project_root, level["reference"])
    actor_instance = next(
        (document for document in documents if document.unity_type == "1001" and f"guid: {ACTOR_GUID}" in document.body),
        None,
    )
    door_instance = next(
        (document for document in documents if document.unity_type == "1001" and f"guid: {DOOR_GUID}" in document.body),
        None,
    )
    actor_position = (
        modification_value(actor_instance.body, ACTOR_TRANSFORM_SOURCE_ID, ACTOR_GUID, "m_LocalPosition.x"),
        modification_value(actor_instance.body, ACTOR_TRANSFORM_SOURCE_ID, ACTOR_GUID, "m_LocalPosition.y"),
    ) if actor_instance else (None, None)
    door_position = (
        modification_value(door_instance.body, DOOR_TRANSFORM_SOURCE_ID, DOOR_GUID, "m_LocalPosition.x"),
        modification_value(door_instance.body, DOOR_TRANSFORM_SOURCE_ID, DOOR_GUID, "m_LocalPosition.y"),
    ) if door_instance else (None, None)
    reference_object = next(
        (document for document in documents if game_object_name(document) == Path(level["reference"]).stem),
        None,
    )
    checks = {
        "rootName": bool(re.search(rf"(?m)^  m_Name: {re.escape(level['name'])}$", text)),
        "referenceName": bool(re.search(rf"(?m)^  m_Name: {re.escape(Path(level['reference']).stem)}$", text)),
        "referenceInactive": reference_object is not None and "m_IsActive: 0" in reference_object.body,
        "referenceGuid": expected_guid in text,
        "groundCount": grounds == expected_grounds,
        "groundGeometry": ground_geometry_matches(documents, expected_geometry),
        "sceneChildren": scene_contains_only_grounds(documents, expected_grounds),
        "duplicates": duplicates,
        "missingLocalReferences": missing,
        "actorGuid": ACTOR_GUID in text,
        "doorGuid": DOOR_GUID in text,
        "actorPosition": actor_position == (float(level["actor"]["x"]), float(level["actor"]["y"])),
        "doorPosition": door_position == (float(level["door"]["x"]), float(level["door"]["y"])),
        "levelBindings": all(
            re.search(rf"(?m)^  {field}: \{{fileID: (?!0\}})-?\d+\}}$", text)
            for field in ("actor", "successDoorTrigger", "enterDoorAnimCtrl", "deathFeedback")
        ),
    }
    checks["ok"] = all(
        value is True for key, value in checks.items() if key not in {"duplicates", "missingLocalReferences", "ok"}
    ) and not duplicates and not missing
    checks["grounds"] = grounds
    checks["expectedGrounds"] = expected_grounds
    return checks


def load_spec(path: Path) -> dict:
    data = json.loads(read_utf8(path))
    if not isinstance(data.get("levels"), list) or not data["levels"]:
        raise ValueError("spec.levels must be a non-empty list")
    for level in data["levels"]:
        if not re.fullmatch(r"ILevel_\d{3}", level.get("name", "")):
            raise ValueError(f"invalid level name: {level.get('name')}")
    return data


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True, type=Path)
    parser.add_argument("--spec", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--validate-only", action="store_true")
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    spec = load_spec(args.spec.resolve())
    template_path = project_root / spec["template"]
    template_meta = read_utf8(Path(f"{template_path}.meta"))
    output_dir = template_path.parent
    fixed_grounds = spec.get("fixed_grounds", [])
    failed = False

    for level in spec["levels"]:
        target = output_dir / f"{level['name']}.prefab"
        target_meta = Path(f"{target}.meta")
        if args.validate_only:
            if not target.exists():
                print(json.dumps({"name": level["name"], "ok": False, "error": "target missing"}, ensure_ascii=False))
                failed = True
                continue
            report = validate_prefab(target, level, fixed_grounds, project_root)
            print(json.dumps({"name": level["name"], **report}, ensure_ascii=False))
            failed |= not report["ok"]
            continue

        text, report = build_prefab(project_root, template_path, level, fixed_grounds)
        print(json.dumps(report, ensure_ascii=False))
        if args.dry_run:
            continue
        if (target.exists() or target_meta.exists()) and not args.force:
            raise FileExistsError(f"refusing to overwrite existing target without --force: {target}")
        target.write_text(text, encoding="utf-8", newline="\n")
        if not target_meta.exists():
            target_meta.write_text(make_meta(template_meta), encoding="utf-8", newline="\n")

    return 1 if failed else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
