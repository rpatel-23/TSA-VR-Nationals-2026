#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  decimate_enigma.py  (Blender pipeline)
#  DECRYPTED - the imported Enigma model is ~300K verts (far over the Quest
#  budget). This decimates it to a Quest-friendly poly count and re-exports the
#  FBX, for use as a DECORATIVE backdrop (the procedural Enigma still drives the
#  actual puzzle).
#
#  Run:  blender --background --python decimate_enigma.py
# -----------------------------------------------------------------------------

import bpy
import os

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
IMP = os.path.join(PROJECT, "Assets", "_Project", "Art", "Imported")
TARGET_RATIO = 0.045         # ~300K -> ~13K verts

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=os.path.join(IMP, "enigma_machine_1934.glb"))

# GLB import can share mesh data between objects, which makes modifier_apply fail
# (silently leaving the big meshes undecimated). Make every mesh single-user first.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.make_single_user(object=True, obdata=True)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
before = sum(len(o.data.vertices) for o in meshes)
for o in meshes:
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("Decimate", 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.ratio = TARGET_RATIO
    try:
        bpy.ops.object.modifier_apply(modifier=m.name)
        print(f"  decimated {o.name}: -> {len(o.data.vertices)} v")
    except RuntimeError as e:
        print(f"  decimate FAILED on {o.name}: {e}")
after = sum(len(o.data.vertices) for o in bpy.data.objects if o.type == 'MESH')
print(f"DECIMATE: {before} -> {after} verts")

out = os.path.abspath(os.path.join(IMP, "enigma_machine_1934.fbx"))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=True, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', bake_space_transform=False,
    mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
    path_mode='COPY', embed_textures=False,
    object_types={'MESH', 'EMPTY', 'ARMATURE'}, axis_forward='-Z', axis_up='Y')
print(f"EXPORTED: {out}")
