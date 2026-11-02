#!/usr/bin/env python3
"""Map reference-image points into an IWanner world-space opening."""

from __future__ import annotations

import argparse
import json


def parse_point(value: str) -> tuple[float, float]:
    x, y = value.split(",", 1)
    return float(x), float(y)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pixel-rect", required=True, help="left,top,width,height")
    parser.add_argument("--world-rect", required=True, help="left,top,width,height")
    parser.add_argument("--point", action="append", default=[], help="pixel x,y; repeatable")
    parser.add_argument("--radius", action="append", default=[], type=float, help="pixel radius; repeatable")
    args = parser.parse_args()

    px_left, px_top, px_width, px_height = map(float, args.pixel_rect.split(","))
    world_left, world_top, world_width, world_height = map(float, args.world_rect.split(","))
    if px_width <= 0 or px_height <= 0 or world_width <= 0 or world_height <= 0:
        parser.error("rectangle widths and heights must be positive")

    def map_point(point: tuple[float, float]) -> dict[str, float]:
        px, py = point
        return {
            "x": world_left + (px - px_left) / px_width * world_width,
            "y": world_top - (py - px_top) / px_height * world_height,
        }

    result = {
        "points": [map_point(parse_point(point)) for point in args.point],
        "radii": [radius / px_height * world_height for radius in args.radius],
        "scale": {"x": world_width / px_width, "y": world_height / px_height},
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
