#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  validate_exports.py
#  DECRYPTED - A Walk Through the History of Secret Writing  (Blender pipeline)
#
#  Standalone sanity check for the exported artifact files. It does NOT need
#  Blender or Unity - it just confirms each expected .fbx / .glb exists, is over a
#  minimum size (so it is a real mesh, not an empty stub or a half-written file),
#  and starts with the right magic bytes. Prints a clear PASS / FAIL report.
#
#  Run:  python validate_exports.py          (or: py -3.13 validate_exports.py)
# -----------------------------------------------------------------------------

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "_Project", "Art", "Generated")

ARTIFACTS = ["Museum_Architecture", "CipherDisk", "Enigma", "Vault", "RevealSculpture"]
MIN_BYTES = 10 * 1024          # a real mesh export is comfortably over 10 KB
FORMATS = [".fbx", ".glb"]


def magic_ok(path, ext):
    """Cheap header check: FBX binary starts 'Kaydara FBX Binary'; GLB starts 'glTF'."""
    try:
        with open(path, "rb") as f:
            head = f.read(20)
    except OSError:
        return False
    if ext == ".fbx":
        return head.startswith(b"Kaydara FBX Binary")
    if ext == ".glb":
        return head[:4] == b"glTF"
    return True


def check(path, ext):
    if not os.path.exists(path):
        return "MISSING", 0
    size = os.path.getsize(path)
    if size < 1024:
        return "EMPTY STUB (run git lfs pull?)", size
    if size < MIN_BYTES:
        return "TOO SMALL (likely corrupt)", size
    if not magic_ok(path, ext):
        return "BAD HEADER (corrupt)", size
    return "PASS", size


def main():
    print(f"Validating exports in:\n  {OUT_DIR}\n")
    any_missing = False
    all_pass = True
    for base in ARTIFACTS:
        present = []
        for ext in FORMATS:
            path = os.path.join(OUT_DIR, base + ext)
            status, size = check(path, ext)
            kb = f"{size/1024:8.1f} KB" if size else "      -  "
            mark = "ok  " if status == "PASS" else "FAIL"
            print(f"  [{mark}] {base}{ext:5}  {kb}  {status}")
            if status == "PASS":
                present.append(ext)
        if not present:
            any_missing = True
            all_pass = False
        elif len(present) < len(FORMATS):
            # one format present is fine (GLB-only or FBX-only is valid)
            pass

    print()
    if all_pass and not any_missing:
        print("RESULT: all artifacts have valid exports.")
        return 0
    if any_missing:
        print("RESULT: at least one artifact has NO valid export. Re-run export_all.py "
              "and read the tracebacks; if FBX keeps failing, use the GLB output.")
        return 1
    print("RESULT: every artifact has at least one valid export.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
