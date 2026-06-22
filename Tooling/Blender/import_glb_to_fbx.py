#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  import_glb_to_fbx.py  (Blender pipeline)
#  DECRYPTED - converts imported .glb artifact models to .fbx so Unity 2022.3
#  (which has no native glTF importer) can use them as model prefabs, and prints
#  each model's full child hierarchy so the interactive parts can be identified.
#
#  Run:  blender --background --python import_glb_to_fbx.py
# -----------------------------------------------------------------------------

import bpy
import os

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
IMP = os.path.join(PROJECT, "Assets", "_Project", "Art", "Imported")

MODELS = ["enigma_machine_1934", "vault_door"]


def reset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def walk(o, d):
    v = len(o.data.vertices) if (o.type == 'MESH' and o.data is not None) else 0
    line = "  " * d + f"- {o.name} [{o.type}]" + (f" v{v}" if v else "")
    print(line)
    for c in o.children:
        walk(c, d + 1)


def main():
    for base in MODELS:
        reset()
        glb = os.path.join(IMP, base + ".glb")
        print(f"\n==================== {base}.glb hierarchy ====================")
        bpy.ops.import_scene.gltf(filepath=glb)
        roots = [o for o in bpy.data.objects if o.parent is None]
        for r in roots:
            walk(r, 0)

        out = os.path.abspath(os.path.join(IMP, base + ".fbx"))
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.export_scene.fbx(
            filepath=out, use_selection=True, apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL', bake_space_transform=False,
            mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
            path_mode='COPY', embed_textures=False,
            object_types={'MESH', 'EMPTY', 'ARMATURE'},
            axis_forward='-Z', axis_up='Y')
        print(f"EXPORTED: {out}")


if __name__ == "__main__":
    main()
