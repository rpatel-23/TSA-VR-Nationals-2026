#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  export_all.py
#  DECRYPTED — batch asset export  (Blender pipeline)
#
#  Runs every procedural generator in turn and exports each one's collection to a
#  Quest-friendly FBX (or GLB), into Assets/_Project/Art/Generated/. Because each
#  generator resets the scene on entry, we build and export one asset at a time,
#  in order, so a single Blender session produces the whole art set.
#
#  Run:
#    blender --background --python export_all.py               # BOTH .fbx and .glb
#    blender --background --python export_all.py -- --glb-only  # GLB only
#    blender --background --python export_all.py -- --fbx-only  # FBX only
#
#  By default every artifact is exported as BOTH FBX and GLB. GLB (glTF 2.0) is the
#  modern, more stable open format and imports natively into Unity 2022.3 with no
#  extra package, so it is the recommended fallback if an FBX ever fails.
#
#  The "--" separates Blender's args from this script's args.
# -----------------------------------------------------------------------------

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.append(HERE)

import gen_common as gc
import gen_architecture
import gen_cipher_disk
import gen_enigma
import gen_vault
import gen_reveal_sculpture

PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "_Project", "Art", "Generated")

# (module, output base name). Order is arbitrary since each rebuilds the scene.
PIPELINE = [
    (gen_architecture,     "Museum_Architecture"),
    (gen_cipher_disk,      "CipherDisk"),
    (gen_enigma,           "Enigma"),
    (gen_vault,            "Vault"),
    (gen_reveal_sculpture, "RevealSculpture"),
]


def parse_formats():
    """Default: export BOTH FBX and GLB. --glb-only / --fbx-only narrow it."""
    formats = [("FBX", ".fbx"), ("GLB", ".glb")]
    if "--" in sys.argv:
        extra = sys.argv[sys.argv.index("--") + 1:]
        if "--glb-only" in extra or "--gltf-only" in extra:
            formats = [("GLB", ".glb")]
        elif "--fbx-only" in extra:
            formats = [("FBX", ".fbx")]
    return formats


def main():
    formats = parse_formats()
    os.makedirs(OUT_DIR, exist_ok=True)
    names = " + ".join(f for f, _ in formats)
    print(f"[export_all] exporting {len(PIPELINE)} assets as {names} -> {OUT_DIR}")

    results = []  # (base, fmt, ok)
    for module, base in PIPELINE:
        print(f"[export_all] building {base} via {module.__name__}.build() ...")
        col = module.build()                      # resets scene, returns its collection
        for fmt, ext in formats:
            out_path = os.path.join(OUT_DIR, base + ext)
            ok = gc.export_collection(col, out_path, fmt=fmt)   # never raises; prints traceback on failure
            results.append((base, fmt, ok))

    ok_count = sum(1 for _, _, ok in results if ok)
    print(f"\n[export_all] done: {ok_count}/{len(results)} exports succeeded.")
    for base, fmt, ok in results:
        print(f"   {'OK  ' if ok else 'FAIL'} {base}.{fmt.lower()}")
    if ok_count < len(results):
        print("[export_all] Some exports FAILED (see tracebacks above). The GLB "
              "fallback is preferred if FBX keeps failing.")
    print("[export_all] Run validate_exports.py to confirm file sizes, then import "
          "the Generated folder into Unity (set model scale/axis on import).")


if __name__ == "__main__":
    main()
