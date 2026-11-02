#!/usr/bin/env python3
"""Measure exact-color horizontal rectangles in IWanner reference screenshots."""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image


def parse_color(value: str) -> tuple[int, int, int]:
    parts = tuple(int(part) for part in value.split(","))
    if len(parts) != 3 or any(part < 0 or part > 255 for part in parts):
        raise argparse.ArgumentTypeError("color must be R,G,B with values from 0 to 255")
    return parts


def find_runs(image: Image.Image, color: tuple[int, int, int], min_width: int, min_height: int):
    pixels = image.load()
    width, height = image.size
    rows: dict[tuple[int, int], list[int]] = defaultdict(list)

    for y in range(height):
        x = 0
        while x < width:
            if pixels[x, y] != color:
                x += 1
                continue
            left = x
            while x + 1 < width and pixels[x + 1, y] == color:
                x += 1
            right = x
            if right - left + 1 >= min_width:
                rows[(left, right)].append(y)
            x += 1

    rectangles = []
    for (left, right), values in rows.items():
        start = previous = values[0]
        for y in values[1:]:
            if y != previous + 1:
                if previous - start + 1 >= min_height:
                    rectangles.append((left, start, right, previous))
                start = y
            previous = y
        if previous - start + 1 >= min_height:
            rectangles.append((left, start, right, previous))
    return sorted(rectangles, key=lambda item: (item[1], item[0]))


def world_values(rectangle, image_size: tuple[int, int], ppu: float, ground_height: float):
    left, top, right, bottom = rectangle
    image_width, image_height = image_size
    width = (right - left + 1) / ppu
    center_x = ((left + right) / 2 - image_width / 2) / ppu
    top_y = (image_height / 2 - top) / ppu
    center_y = top_y - ground_height / 2
    return center_x, center_y, width, ground_height, top_y, bottom - top + 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("images", nargs="+", type=Path)
    parser.add_argument("--color", type=parse_color, default=(204, 96, 50))
    parser.add_argument("--min-width", type=int, default=40)
    parser.add_argument("--min-height", type=int, default=3)
    parser.add_argument("--ppu", type=float, default=100.0)
    parser.add_argument("--ground-height", type=float, default=10.0)
    args = parser.parse_args()

    for image_path in args.images:
        image = Image.open(image_path).convert("RGB")
        flattened = image.get_flattened_data() if hasattr(image, "get_flattened_data") else image.getdata()
        counts = Counter(flattened)
        print(f"{image_path}: size={image.width}x{image.height}, dominant={counts.most_common(4)}")
        for rectangle in find_runs(image, args.color, args.min_width, args.min_height):
            x, y, width, height, top_y, pixel_height = world_values(
                rectangle, image.size, args.ppu, args.ground_height
            )
            left, top, right, bottom = rectangle
            print(
                f"  px=({left},{top})..({right},{bottom}) pixelHeight={pixel_height} "
                f"ground=(x={x:.3f}, y={y:.3f}, width={width:.3f}, height={height:.3f}) "
                f"topY={top_y:.3f}"
            )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
