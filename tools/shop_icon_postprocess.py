"""Prepare style boards and crop generated shop-card art to Unity icon size.

Examples:
    python tools/shop_icon_postprocess.py style-boards
    python tools/shop_icon_postprocess.py crop \
        output/imagegen/shop_icons/Lanternfox_1536x1024.png \
        Assets/Images/Units/Icon/T1/Lanternfox.png
"""

from __future__ import annotations

import argparse
import json
import plistlib
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_STYLE_SOURCE = REPO_ROOT / "Assets" / "Images" / "Units" / "Icon"
DEFAULT_STYLE_OUTPUT = REPO_ROOT / "tmp" / "imagegen" / "shop_style_refs"
TARGET_RATIO = (29, 13)
TARGET_SIZE = (290, 130)


def center_crop_exact_ratio(image: Image.Image, ratio: tuple[int, int]) -> Image.Image:
    """Return the largest centered crop whose dimensions exactly match ratio."""
    ratio_w, ratio_h = ratio
    width, height = image.size
    scale = min(width // ratio_w, height // ratio_h)
    if scale < 1:
        raise ValueError(
            f"Image {width}x{height} is too small for ratio {ratio_w}:{ratio_h}"
        )

    crop_width = ratio_w * scale
    crop_height = ratio_h * scale
    left = (width - crop_width) // 2
    top = (height - crop_height) // 2
    return image.crop((left, top, left + crop_width, top + crop_height))


def crop_icon(source: Path, destination: Path) -> None:
    with Image.open(source) as opened:
        image = ImageOps.exif_transpose(opened).convert("RGB")
        cropped = center_crop_exact_ratio(image, TARGET_RATIO)
        icon = cropped.resize(TARGET_SIZE, Image.Resampling.LANCZOS)

    destination.parent.mkdir(parents=True, exist_ok=True)
    icon.save(destination, format="PNG", optimize=True)

    with Image.open(destination) as verified:
        if verified.size != TARGET_SIZE or verified.mode != "RGB":
            raise RuntimeError(
                f"Invalid output {destination}: size={verified.size}, mode={verified.mode}"
            )

    print(
        f"Wrote {destination} "
        f"(source={image.size[0]}x{image.size[1]}, "
        f"crop={cropped.size[0]}x{cropped.size[1]}, output=290x130 RGB)"
    )


def find_high_resolution_cards(source_dir: Path) -> list[Path]:
    cards: list[Path] = []
    for path in sorted(source_dir.rglob("*.png")):
        with Image.open(path) as image:
            width, height = image.size
        if width >= 1500 and height >= 700:
            cards.append(path)
    return cards


def make_style_boards(source_dir: Path, output_dir: Path) -> None:
    cards = find_high_resolution_cards(source_dir)
    if len(cards) != 40:
        raise RuntimeError(
            f"Expected exactly 40 high-resolution style cards, found {len(cards)}"
        )

    output_dir.mkdir(parents=True, exist_ok=True)
    board_groups = [cards[index : index + 10] for index in range(0, 40, 10)]
    manifest: dict[str, list[str]] = {}

    thumb_size = (960, 430)
    margin = 24
    gap = 16
    board_size = (
        margin * 2 + thumb_size[0] * 2 + gap,
        margin * 2 + thumb_size[1] * 5 + gap * 4,
    )

    for board_index, group in enumerate(board_groups, start=1):
        board = Image.new("RGB", board_size, (10, 12, 18))
        names: list[str] = []

        for image_index, card_path in enumerate(group):
            row, column = divmod(image_index, 2)
            with Image.open(card_path) as opened:
                card = ImageOps.exif_transpose(opened).convert("RGB")
                card = ImageOps.fit(
                    card,
                    thumb_size,
                    method=Image.Resampling.LANCZOS,
                    centering=(0.5, 0.5),
                )
            x = margin + column * (thumb_size[0] + gap)
            y = margin + row * (thumb_size[1] + gap)
            board.paste(card, (x, y))
            names.append(card_path.relative_to(REPO_ROOT).as_posix())

        output_path = output_dir / f"style_board_{board_index:02d}.jpg"
        board.save(output_path, format="JPEG", quality=92, optimize=True)
        manifest[output_path.name] = names
        print(f"Wrote {output_path} ({len(group)} cards)")

    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print(f"Wrote {manifest_path}")


def _parse_atlas_rect(value: str) -> tuple[int, int, int, int]:
    numbers = [int(number) for number in re.findall(r"\d+", value)]
    if len(numbers) != 4:
        raise ValueError(f"Unsupported atlas rectangle: {value}")
    return numbers[0], numbers[1], numbers[2], numbers[3]


def make_sprite_montage(texture: Path, plist_path: Path, destination: Path) -> None:
    """Create a readable six-frame appearance reference from a sprite atlas."""
    with plist_path.open("rb") as handle:
        data = plistlib.load(handle)
    frames: dict[str, dict[str, object]] = data["frames"]

    preferred = ("idle", "breathing", "attack", "run", "hit", "death")
    selected: list[tuple[str, dict[str, object]]] = []
    for animation in preferred:
        match = next(
            (
                (name, details)
                for name, details in frames.items()
                if f"_{animation}_" in name and name.endswith("_000.png")
            ),
            None,
        )
        if match is not None:
            selected.append(match)
    if not selected:
        selected = list(frames.items())[:6]
    selected = selected[:6]

    tile_size = 280
    label_height = 36
    columns = 3
    rows = 2
    margin = 24
    gap = 18
    canvas = Image.new(
        "RGB",
        (
            margin * 2 + columns * tile_size + (columns - 1) * gap,
            margin * 2 + rows * (tile_size + label_height) + (rows - 1) * gap,
        ),
        (78, 97, 106),
    )
    draw = ImageDraw.Draw(canvas)

    with Image.open(texture) as atlas_opened:
        atlas = ImageOps.exif_transpose(atlas_opened).convert("RGBA")
        for index, (name, details) in enumerate(selected):
            x, y, width, height = _parse_atlas_rect(str(details["frame"]))
            sprite = atlas.crop((x, y, x + width, y + height))
            sprite = sprite.resize(
                (tile_size, tile_size), Image.Resampling.NEAREST
            )
            row, column = divmod(index, columns)
            paste_x = margin + column * (tile_size + gap)
            paste_y = margin + row * (tile_size + label_height + gap)
            canvas.paste(sprite, (paste_x, paste_y), sprite)
            label = re.sub(r"^.*?_", "", name).removesuffix(".png")
            draw.text(
                (paste_x + 4, paste_y + tile_size + 8),
                label,
                fill=(238, 242, 244),
            )

    destination.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(destination, format="PNG", optimize=True)
    print(f"Wrote {destination} ({len(selected)} representative frames)")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    crop_parser = subparsers.add_parser(
        "crop", help="Center-crop generated art to 29:13 and save as 290x130 RGB PNG"
    )
    crop_parser.add_argument("source", type=Path)
    crop_parser.add_argument("destination", type=Path)

    boards_parser = subparsers.add_parser(
        "style-boards", help="Combine the 40 high-resolution card references into 4 boards"
    )
    boards_parser.add_argument(
        "--source-dir", type=Path, default=DEFAULT_STYLE_SOURCE
    )
    boards_parser.add_argument(
        "--output-dir", type=Path, default=DEFAULT_STYLE_OUTPUT
    )

    montage_parser = subparsers.add_parser(
        "sprite-montage",
        help="Create an enlarged six-frame character reference from a PNG/plist atlas",
    )
    montage_parser.add_argument("texture", type=Path)
    montage_parser.add_argument("plist", type=Path)
    montage_parser.add_argument("destination", type=Path)

    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if args.command == "crop":
        crop_icon(args.source, args.destination)
    elif args.command == "style-boards":
        make_style_boards(args.source_dir, args.output_dir)
    else:
        make_sprite_montage(args.texture, args.plist, args.destination)


if __name__ == "__main__":
    main()
