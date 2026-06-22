#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_cipher_disk.py  — DECRYPTED Exhibit 1: Caesar cipher disk
#  Museum-quality rebuild: Roman stone column, ornate bronze rings, raised
#  letter medallions, symmetric grip handles, decorative center boss.
#  Child hierarchy preserved for Unity wiring:
#    InnerDisk  → XRGrabTwistDisk grab target (rotates about Z)
#    OuterAnchor_<A-Z> / InnerAnchor_<A-Z> → CaesarCipherController anchors
# -----------------------------------------------------------------------------

import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc

OUTER_R   = 0.58
INNER_R   = 0.41
TOP_Z     = 1.04   # height of the disk face above ground
LETTERS   = gc.letters()
TWO_PI    = gc.TWO_PI


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_CipherDisk")

    # --- materials -----------------------------------------------------------
    stone       = gc.make_material("CD_Stone",      (0.44, 0.41, 0.36, 1), metallic=0.0,  roughness=0.92)
    stone_dark  = gc.make_material("CD_StoneDark",  (0.26, 0.24, 0.21, 1), metallic=0.0,  roughness=0.95)
    bronze      = gc.make_material("CD_Bronze",     (0.48, 0.32, 0.12, 1), metallic=0.90, roughness=0.32)
    bronze_dark = gc.make_material("CD_BronzeDark", (0.22, 0.14, 0.06, 1), metallic=0.88, roughness=0.50)
    glyph       = gc.make_material("CD_Glyph",      (0.96, 0.80, 0.36, 1), metallic=0.55, roughness=0.28,
                                   emission=(0.96, 0.80, 0.36, 1), emission_strength=0.0)
    gold        = gc.make_material("CD_Gold",       (0.83, 0.68, 0.22, 1), metallic=0.98, roughness=0.18)

    # --- column base (3 stacked drums + footing) -----------------------------
    gc.cylinder("CD_Footing",    radius=0.46, depth=0.07, location=(0,0,0.035),  col=col, mat=stone_dark, segments=6)
    gc.cylinder("CD_Base",       radius=0.40, depth=0.12, location=(0,0,0.105),  col=col, mat=stone,      segments=6)
    _fluted_column(col, stone, stone_dark, base_z=0.17, top_z=0.92, radius=0.155, flutes=16)
    gc.cylinder("CD_CapBase",    radius=0.36, depth=0.06, location=(0,0,0.925),  col=col, mat=stone,      segments=48)
    gc.cylinder("CD_CapAbacus",  radius=0.42, depth=0.05, location=(0,0,0.965),  col=col, mat=stone_dark, segments=48)

    # Bronze seat plate on top of capital
    gc.cylinder("CD_SeatPlate",  radius=0.64, depth=0.04, location=(0,0,0.990),  col=col, mat=bronze_dark, segments=72)

    # --- outer (ciphertext) fixed ring ---------------------------------------
    gc.ring("CD_OuterRing",  outer=OUTER_R+0.07, inner=OUTER_R-0.04, depth=0.06,
            location=(0,0,TOP_Z), col=col, mat=bronze, segments=96)
    gc.ring("CD_OuterBevel", outer=OUTER_R+0.08, inner=OUTER_R+0.07, depth=0.03,
            location=(0,0,TOP_Z+0.015), col=col, mat=gold, segments=96)
    gc.ring("CD_OuterEdge",  outer=OUTER_R-0.04, inner=OUTER_R-0.05, depth=0.06,
            location=(0,0,TOP_Z), col=col, mat=gold, segments=96)
    _place_letter_medallions(col, bronze, glyph, gold, radius=OUTER_R-0.01, z=TOP_Z,
                              prefix="OuterGlyph", anchor_prefix="OuterAnchor", parent=None)

    # --- inner (plaintext) rotating disk ------------------------------------
    inner = gc.cylinder("InnerDisk", radius=INNER_R+0.005, depth=0.055,
                         location=(0,0,TOP_Z+0.025), col=col, mat=bronze, segments=96)
    gc.ring("ID_InnerBevel", outer=INNER_R+0.005, inner=INNER_R-0.005, depth=0.055,
            location=(0,0,TOP_Z+0.025), col=col, mat=gold, segments=96).parent = inner
    gc.cylinder("ID_Face", radius=INNER_R-0.04, depth=0.01,
                location=(0,0,TOP_Z+0.055), col=col, mat=bronze_dark, segments=96).parent = inner
    _place_letter_medallions(col, bronze_dark, glyph, gold, radius=INNER_R-0.06, z=TOP_Z+0.065,
                              prefix="InnerGlyph", anchor_prefix="InnerAnchor", parent=inner)

    # Center boss / rosette on inner disk
    boss = gc.cylinder("ID_Boss", radius=0.06, depth=0.025, location=(0,0,TOP_Z+0.065),
                        col=col, mat=gold, segments=32)
    boss.parent = inner
    for k in range(8):
        a = TWO_PI * k / 8
        petal = gc.cylinder(f"ID_BossPetal{k}", radius=0.018, depth=0.012,
                            location=(0.042*math.cos(a), 0.042*math.sin(a), TOP_Z+0.068),
                            col=col, mat=gold, segments=12)
        petal.parent = inner

    # Symmetric grip handles on inner disk (at 0° and 180°)
    for side, ang in enumerate([0, math.pi]):
        gx, gy = (INNER_R-0.06)*math.cos(ang), (INNER_R-0.06)*math.sin(ang)
        grip = gc.box(f"InnerDisk_Grip{side}", size=(0.03, 0.10, 0.038),
                      location=(gx, gy, TOP_Z+0.058), col=col, mat=bronze_dark)
        grip.rotation_euler = (0, 0, ang)
        gc.apply_transforms(grip)
        gc.parent_keep_world(grip, inner)
        knob = gc.cylinder(f"InnerDisk_GripKnob{side}", radius=0.018, depth=0.015,
                           location=(gx, gy, TOP_Z+0.080), col=col, mat=gold, segments=16)
        gc.parent_keep_world(knob, inner)

    # Tick marks around outer ring edge (every letter gap)
    for i in range(26):
        a = TWO_PI * i / 26
        tr = OUTER_R + 0.065
        gc.box(f"CD_Tick{i}", size=(0.006, 0.006, 0.024),
               location=(tr*math.cos(a), tr*math.sin(a), TOP_Z+0.045),
               col=col, mat=gold)

    gc.parent_all_to_root(col, "CipherDisk_Root")
    print("[gen_cipher_disk] museum-quality Caesar disk built.")
    return col


def _fluted_column(col, mat_shaft, mat_flute, base_z, top_z, radius, flutes):
    height = top_z - base_z
    gc.cylinder("CD_ColumnShaft", radius=radius, depth=height,
                location=(0, 0, base_z + height*0.5), col=col, mat=mat_shaft, segments=flutes*2)
    for i in range(flutes):
        a = TWO_PI * i / flutes
        fx = (radius - 0.012) * math.cos(a)
        fy = (radius - 0.012) * math.sin(a)
        gc.cylinder(f"CD_Flute{i}", radius=0.018, depth=height*0.90,
                    location=(fx, fy, base_z + height*0.5),
                    col=col, mat=mat_flute, segments=12)


def _place_letter_medallions(col, ring_mat, glyph_mat, accent_mat, radius, z, prefix, anchor_prefix, parent):
    for i, ch in enumerate(LETTERS):
        ang = math.pi/2 - TWO_PI * i / 26.0
        x, y = radius * math.cos(ang), radius * math.sin(ang)
        # Raised circular medallion behind each letter
        med = gc.cylinder(f"{prefix}_Med{ch}", radius=0.034, depth=0.012,
                          location=(x, y, z-0.004), col=col, mat=ring_mat, segments=20)
        t = gc.text(f"{prefix}_{ch}", ch, size=0.042, location=(x, y, z+0.004),
                    rotation=(0, 0, ang - math.pi/2), col=col, mat=glyph_mat,
                    extrude=0.005, align_x='CENTER', align_y='CENTER')
        a = gc.add_empty(f"{anchor_prefix}_{ch}", location=(x, y, z), col=col, size=0.018)
        if parent is not None:
            med.parent = parent
            t.parent   = parent
            a.parent   = parent


if __name__ == "__main__":
    build()
