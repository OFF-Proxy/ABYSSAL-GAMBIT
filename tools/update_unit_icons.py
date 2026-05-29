"""One-shot: update Entity Database.asset icon GUIDs after icon pack import.

Run from repo root:
    py tools/update_unit_icons.py
"""
import re
import sys
import pathlib

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
DB_PATH = REPO_ROOT / "Assets" / "Resources" / "Entity Database.asset"

MAPPING = [
    ("Altgeneraltier2", "b586f0bde65fc52429baeba1a1ff960a"),
    ("Archdeacon", "07b09b399de5a0b4a8ebe5480c9e9c91"),
    ("Auroralioness", "c4539a9fcf26a314e8a8edc18afe1d93"),
    ("Azuritelion", "7804d859e2d24b74a9fe17bae6ddac78"),
    ("Backlinearcher", "3d6c873bf4a926447bbd8ec764d5ec1f"),
    ("Protector", "5563f28792380484bbe37413c7e5e642"),
    ("Sandpanther", "9045e1c187a63fc44b9f5ce05a2b27fe"),
    ("Taskmaster", "75cf7e4c0cbbb9a449f32fd7a66e944f"),
    ("Ilenamk2", "13fde62b338650e498625ce4faab9472"),
    ("Kane", "9e8b912508b5af54bb5ece21828f5851"),
    ("Malyk", "d8cbfb0c488b6cc4da09c9cf391f66ab"),
    ("Paragon", "949df111660d45842ab0410316171bb7"),
    ("Wraith", "52bffa41c7b876a4d91380a3697e0ed6"),
    ("Wujin", "08a29c7d925ba004a9dfe4dc971f0b47"),
    ("Embergeneral", "8302a90a36eab7242a479ab298c889a6"),
    ("Gol", "d77979bb9085b8c40a7d83a21ae3dbd6"),
    ("Invader", "fbd0139b33aa8ae4694ccecdc22d1141"),
    ("Kron", "4c5314a381ebd5e44adfeb6b06215caf"),
    ("Legion", "07dcc9d97ac468d40878b497be075662"),
    ("Plaguegeneral", "f0d666f2845b2b14baf9f70a742aaa68"),
    ("Skyfalltyrant", "24a8f9c6c3029ab449bd145a63cad39f"),
]


def main():
    text = DB_PATH.read_text(encoding="utf-8")
    changed = 0
    for unit, new_guid in MAPPING:
        pattern = re.compile(
            r"(\n    name: " + re.escape(unit) + r"\n    icon: \{fileID: )-?\d+(, guid: )[a-f0-9]+(, type: 3\})"
        )
        replacement = r"\g<1>21300000\g<2>" + new_guid + r"\g<3>"
        new_text, count = pattern.subn(replacement, text)
        if count == 1:
            text = new_text
            changed += 1
            print(f"  updated: {unit} -> {new_guid}")
        elif count > 1:
            print(f"  WARNING: {unit} matched {count} times")
        else:
            print(f"  FAIL: no match for {unit}")

    if changed > 0:
        DB_PATH.write_text(text, encoding="utf-8")
    print(f"\nTotal: {changed}/{len(MAPPING)} icons updated")


if __name__ == "__main__":
    main()
