#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_reveal_sculpture.py  — DECRYPTED final chamber sculpture
#  Museum-quality rebuild: majestic stepped pedestal, proper carved inscription
#  stele (Roman), complex 5-gear clockwork (WWII), detailed PCB with SMD parts
#  (Modern). Each stage is a separate child of Reveal_Pivot so FinalRevealController
#  can cross-dissolve them. Morph mesh preserved for the controller's alternate path.
# -----------------------------------------------------------------------------

import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc

try:
    import bpy
    import bmesh
    from mathutils import Vector
except ImportError:
    bpy = None
    bmesh = None

TWO_PI = gc.TWO_PI


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_Reveal")

    # --- materials -----------------------------------------------------------
    marble    = gc.make_material("RV_Marble",   (0.93, 0.91, 0.88, 1), metallic=0.02, roughness=0.28)
    stone     = gc.make_material("RV_Stone",    (0.52, 0.49, 0.44, 1), metallic=0.0,  roughness=0.88)
    stone_d   = gc.make_material("RV_StoneDark",(0.30, 0.28, 0.25, 1), metallic=0.0,  roughness=0.92)
    brass     = gc.make_material("RV_Brass",    (0.64, 0.47, 0.16, 1), metallic=0.92, roughness=0.32)
    copper    = gc.make_material("RV_Copper",   (0.72, 0.36, 0.18, 1), metallic=0.88, roughness=0.38)
    steel     = gc.make_material("RV_Steel",    (0.56, 0.57, 0.59, 1), metallic=0.95, roughness=0.28)
    glow      = gc.make_material("RV_Glow",     (0.18, 0.88, 1.00, 1), metallic=0.2,  roughness=0.28,
                                 emission=(0.18, 0.88, 1.00, 1), emission_strength=1.4)
    glow_gold = gc.make_material("RV_GlowGold", (0.98, 0.80, 0.28, 1), metallic=0.3,  roughness=0.20,
                                 emission=(0.98, 0.80, 0.28, 1), emission_strength=0.8)
    pcb_green = gc.make_material("RV_PCB",      (0.04, 0.22, 0.10, 1), metallic=0.10, roughness=0.60)
    solder    = gc.make_material("RV_Solder",   (0.72, 0.68, 0.62, 1), metallic=0.75, roughness=0.42)
    led_red   = gc.make_material("RV_LED",      (1.0,  0.15, 0.10, 1), metallic=0.0,  roughness=0.18,
                                 emission=(1.0,  0.15, 0.10, 1), emission_strength=1.8)
    chip_mat  = gc.make_material("RV_Chip",     (0.08, 0.08, 0.09, 1), metallic=0.1,  roughness=0.55)

    # --- stepped pedestal ----------------------------------------------------
    _build_pedestal(col, marble, stone, stone_d, brass)

    # Pivot for the three stages
    pivot = gc.add_empty("Reveal_Pivot", location=(0, 0, 1.42), col=col, size=0.12)

    # --- stage 1: Roman inscription stele ------------------------------------
    build_stele(col, marble, stone_d, brass, pivot)

    # --- stage 2: Clockwork gear mechanism -----------------------------------
    build_gears(col, brass, copper, steel, pivot)

    # --- stage 3: Circuit board lattice --------------------------------------
    build_circuit(col, pcb_green, solder, glow, glow_gold, chip_mat, led_red, pivot)

    # --- morph mesh (alternate path for FinalRevealController) ---------------
    if bpy is not None:
        build_morph_mesh(col, glow, pivot)

    gc.parent_all_to_root(col, "Reveal_Root")
    print("[gen_reveal_sculpture] museum-quality reveal built: stele, 5-gear clock, PCB, morph mesh.")
    return col


# ================================================================= pedestal

def _build_pedestal(col, marble, stone, stone_d, brass):
    steps = [
        ("Reveal_Ped_Step0", 0.80, 0.10),
        ("Reveal_Ped_Step1", 0.68, 0.10),
        ("Reveal_Ped_Step2", 0.58, 0.12),
    ]
    z = 0.0
    for name, r, h in steps:
        gc.cylinder(name, radius=r, depth=h, location=(0,0,z+h*0.5),
                    col=col, mat=stone_d, segments=8)
        z += h
    gc.cylinder("Reveal_Ped_Shaft",  radius=0.32, depth=0.70, location=(0,0,z+0.35),
                col=col, mat=marble, segments=8)
    z += 0.70
    gc.cylinder("Reveal_Ped_Neck",   radius=0.36, depth=0.06, location=(0,0,z+0.03),
                col=col, mat=stone,   segments=8)
    z += 0.06
    gc.cylinder("Reveal_Ped_Cap",    radius=0.42, depth=0.08, location=(0,0,z+0.04),
                col=col, mat=stone_d, segments=8)
    z += 0.08
    # Brass accent band around top of pedestal
    gc.ring("Reveal_Ped_Band", outer=0.43, inner=0.40, depth=0.04,
            location=(0,0,z-0.02), col=col, mat=brass, segments=64)
    # Floating platform for sculpture stages
    gc.cylinder("Reveal_Platform", radius=0.46, depth=0.045, location=(0,0,z+0.022),
                col=col, mat=marble, segments=8)


# ================================================================= stage 1: stele

def build_stele(col, marble, stone_d, brass, pivot):
    parts = []
    base = gc.box("RS_Stele_Base", size=(0.76, 0.28, 0.20), location=(0,0,1.14), col=col, mat=stone_d)
    parts.append(base)
    # Slab with tapered top
    slab = gc.box("RS_Stele_Slab", size=(0.54, 0.14, 1.55), location=(0,0,2.02), col=col, mat=marble)
    parts.append(slab)
    # Pediment crown (triangular top) via smaller stacked boxes
    for li in range(4):
        w  = 0.54 - li * 0.08
        parts.append(gc.box(f"RS_Stele_Crown{li}", size=(w, 0.12, 0.07),
                            location=(0, 0, 2.82+li*0.06), col=col, mat=stone_d))
    # Inscription lines (raised ribs = simulated carved text)
    texts = ["CRYPTOGRAPHIA", "AETERNA", "VERITAS", "IN", "SECRETIS"]
    for li, txt in enumerate(texts):
        z = 2.65 - li * 0.19
        gc.box(f"RS_SteleRib{li}", size=(0.38, 0.015, 0.025), location=(0,-0.072,z),
               col=col, mat=stone_d)
        gc.text(f"RS_SteleText{li}", txt, size=0.030, location=(0,-0.080,z),
                rotation=(math.pi/2,0,0), col=col, mat=brass, extrude=0.003)
    # Brass corner mounts
    for sx, sy in [(-1,-1),(-1,1),(1,-1),(1,1)]:
        gc.box(f"RS_SteleMnt_{sx}{sy}", size=(0.035,0.035,0.12),
               location=(sx*0.22, sy*0.08, 1.20), col=col, mat=brass)

    stage = gc.join([p for p in parts if p and p.type == 'MESH'], "Reveal_Stage_Roman")
    if stage:
        gc.parent_keep_world(stage, pivot)


# ================================================================= stage 2: gears

def build_gears(col, brass, copper, steel, pivot):
    gears = []
    configs = [
        # (name, radius, teeth, thickness, cx, cz, mat)
        ("Gear_Main",    0.48, 24, 0.095,  0.00, 1.82, brass),
        ("Gear_Mid",     0.30, 16, 0.085,  0.60, 1.60, copper),
        ("Gear_Small",   0.20, 12, 0.080, -0.50, 2.18, brass),
        ("Gear_Tiny",    0.13, 9,  0.070,  0.50, 2.22, steel),
        ("Gear_Escape",  0.09, 6,  0.065, -0.60, 1.60, copper),
    ]
    for name, r, teeth, thick, cx, cz, mat in configs:
        g = _make_gear(col, name, r, teeth, thick, (cx, 0.0, cz), mat)
        gears.append(g)

    # Axle posts connecting gears to a common backplane
    backplane = gc.box("RS_Gears_BackPlate", size=(1.40, 0.045, 1.50),
                       location=(0, 0.06, 1.90), col=col, mat=steel)
    gears.append(backplane)
    for name, r, teeth, thick, cx, cz, mat in configs:
        gears.append(gc.cylinder(f"{name}_Axle", radius=0.022, depth=0.16,
                                 location=(cx, 0.0, cz), col=col, mat=steel,
                                 segments=12, axis='Y'))

    stage = gc.join([g for g in gears if g and g.type == 'MESH'], "Reveal_Stage_Gears")
    if stage:
        gc.parent_keep_world(stage, pivot)


def _make_gear(col, name, radius, teeth, thickness, location, mat):
    cx, cy, cz = location
    parts = []
    # Main body disc
    parts.append(gc.cylinder(f"{name}_Body", radius=radius*0.82, depth=thickness,
                             location=location, col=col, mat=mat, segments=40, axis='Y'))
    # Teeth
    tooth_r = radius
    tooth_w = TWO_PI * radius / (teeth * 2.2)
    for i in range(teeth):
        a  = TWO_PI * i / teeth
        tx = cx + tooth_r * math.cos(a)
        tz = cz + tooth_r * math.sin(a)
        tooth = gc.box(f"{name}_T{i}", size=(tooth_w*1.1, thickness, tooth_w*1.3),
                       location=(tx, cy, tz), col=col, mat=mat)
        tooth.rotation_euler = (0, a, 0)
        gc.apply_transforms(tooth)
        parts.append(tooth)
    # Hub
    parts.append(gc.cylinder(f"{name}_Hub", radius=radius*0.18, depth=thickness*1.08,
                              location=location, col=col, mat=mat, segments=16, axis='Y'))
    # 4 spokes
    for k in range(4):
        a  = TWO_PI * k / 4
        sx = cx + radius*0.45 * math.cos(a)
        sz = cz + radius*0.45 * math.sin(a)
        spoke = gc.box(f"{name}_Sp{k}", size=(radius*0.12, thickness*0.65, radius*0.90),
                       location=(sx, cy, sz), col=col, mat=mat)
        spoke.rotation_euler = (0, a, 0)
        gc.apply_transforms(spoke)
        parts.append(spoke)
    return gc.join([p for p in parts if p and p.type == 'MESH'], name)


# ================================================================= stage 3: PCB

def build_circuit(col, pcb, solder, glow, glow_gold, chip_mat, led_red, pivot):
    parts = []
    # Main PCB board
    board = gc.box("RS_PCB_Board", size=(1.05, 0.045, 1.48),
                   location=(0, 0, 1.89), col=col, mat=pcb)
    parts.append(board)

    # Vertical trace channels
    for i in range(7):
        x = -0.44 + i * 0.147
        parts.append(gc.box(f"RS_TV{i}", size=(0.012, 0.012, 1.32),
                            location=(x, 0.028, 1.89), col=col, mat=solder))
    # Horizontal trace channels
    for j in range(9):
        z = 1.18 + j * 0.162
        parts.append(gc.box(f"RS_TH{j}", size=(0.96, 0.012, 0.011),
                            location=(0, 0.028, z), col=col, mat=solder))

    # IC packages (large chips)
    ic_positions = [(-0.28, 1.55), (0.22, 2.05), (-0.05, 1.35), (0.35, 1.68), (-0.30, 2.18)]
    for k, (ix, iz) in enumerate(ic_positions):
        w = 0.18 + (k % 2) * 0.04
        parts.append(gc.box(f"RS_IC{k}", size=(w, 0.055, w*0.55),
                            location=(ix, 0.048, iz), col=col, mat=chip_mat))
        # IC pin rows
        pins = int(w / 0.022)
        for p in range(pins):
            px = ix - w*0.5 + p * w/(pins-1) if pins > 1 else ix
            parts.append(gc.box(f"RS_IC{k}PinL{p}", size=(0.004, 0.028, 0.006),
                                location=(px, 0.036, iz - w*0.28), col=col, mat=solder))
            parts.append(gc.box(f"RS_IC{k}PinR{p}", size=(0.004, 0.028, 0.006),
                                location=(px, 0.036, iz + w*0.28), col=col, mat=solder))

    # SMD resistors/capacitors (small blobs)
    smd_pos = [(0.10,1.48),(-.18,1.80),(0.30,2.30),(-0.38,2.10),(0.05,2.45),
               (-.22,1.30),(0.40,1.50),(0.15,1.95),(-0.08,2.20),(0.28,1.70)]
    for k, (sx, sz) in enumerate(smd_pos):
        parts.append(gc.box(f"RS_SMD{k}", size=(0.035, 0.022, 0.020),
                            location=(sx, 0.037, sz), col=col,
                            mat=(glow_gold if k % 3 == 0 else chip_mat)))

    # LEDs along left edge
    for k in range(6):
        lz = 1.28 + k * 0.195
        parts.append(gc.cylinder(f"RS_LED{k}", radius=0.014, depth=0.025,
                                 location=(-0.46, 0.042, lz),
                                 col=col, mat=led_red, segments=12, axis='Y'))

    # Glowing trace highlights (the "signal" traces)
    for i in range(3):
        x = -0.20 + i * 0.20
        parts.append(gc.box(f"RS_GlowTrace{i}", size=(0.007, 0.008, 0.95),
                            location=(x, 0.034, 1.92), col=col, mat=glow))

    # Board standoffs at corners
    for sx, sz in [(-0.45,1.18),(-0.45,2.62),(0.45,1.18),(0.45,2.62)]:
        parts.append(gc.cylinder(f"RS_Standoff{sx}{sz}", radius=0.016, depth=0.06,
                                 location=(sx, -0.025, sz),
                                 col=col, mat=solder, segments=8, axis='Y'))

    stage = gc.join([p for p in parts if p and p.type == 'MESH'], "Reveal_Stage_Circuit")
    if stage:
        gc.parent_keep_world(stage, pivot)


# ================================================================= morph mesh

def build_morph_mesh(col, mat, pivot):
    mesh = bpy.data.meshes.new("Reveal_MorphMesh")
    bm_obj = bmesh.new()
    bmesh.ops.create_cube(bm_obj, size=1.1)
    bmesh.ops.subdivide_edges(bm_obj, edges=bm_obj.edges[:], cuts=8, use_grid_fill=True)
    bm_obj.to_mesh(mesh)
    bm_obj.free()

    obj = bpy.data.objects.new("Reveal_MorphMesh", mesh)
    gc.link(obj, col)
    gc.assign(obj, mat)
    obj.location = (0, 0, 1.88)
    gc.parent_keep_world(obj, pivot)

    obj.shape_key_add(name="Basis", from_mix=False)
    kg = obj.shape_key_add(name="toGears", from_mix=False)
    for i, v in enumerate(mesh.vertices):
        co = v.co
        r  = math.hypot(co.x, co.z)
        s  = (0.58 / r) if r > 1e-5 else 1.0
        kg.data[i].co = Vector((co.x * s, co.y * 0.75, co.z * s))
    kc = obj.shape_key_add(name="toCircuit", from_mix=False)
    for i, v in enumerate(mesh.vertices):
        co = v.co
        kc.data[i].co = Vector((co.x * 1.30, co.y * 0.06, co.z * 1.30))
    obj.data.shape_keys.key_blocks["toGears"].value   = 0.0
    obj.data.shape_keys.key_blocks["toCircuit"].value = 0.0
    obj.hide_render   = True
    obj.hide_viewport = True


if __name__ == "__main__":
    build()
