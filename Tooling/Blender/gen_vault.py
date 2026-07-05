#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_vault.py  — DECRYPTED Exhibit 3: modern security vault
#  Museum-quality rebuild: massive industrial door, 3 concentric bolt rings,
#  6-spoke centre wheel, visible hinge plates, combination dial, digital keypad,
#  pressure gasket, interior archive glow revealed on open.
#  Unity child names preserved:
#    Vault_Door         → VaultController _door (rotating slab)
#    Vault_LockingRing  → VaultController _lockingRing (spins on unlock)
#    Vault_StatusLight* → VaultController _statusLights
#    VaultKey_<L/ENTER/CLEAR> → VaultKeypad keys
#    Vault_Display      → passphrase read-out
# -----------------------------------------------------------------------------

import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc

TWO_PI  = gc.TWO_PI
DOOR_W  = 2.05   # Y
DOOR_H  = 2.90   # Z
DOOR_T  = 0.38   # X  (door thickness)
HINGE_Y = -(DOOR_W * 0.5)


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_Vault")

    # --- materials -----------------------------------------------------------
    steel       = gc.make_material("VT_Steel",      (0.54, 0.55, 0.57, 1), metallic=0.96, roughness=0.26)
    steel_dark  = gc.make_material("VT_SteelDark",  (0.18, 0.19, 0.20, 1), metallic=0.90, roughness=0.40)
    concrete    = gc.make_material("VT_Concrete",   (0.38, 0.36, 0.34, 1), metallic=0.0,  roughness=0.95)
    brass       = gc.make_material("VT_Brass",      (0.64, 0.48, 0.18, 1), metallic=0.92, roughness=0.30)
    key_mat     = gc.make_material("VT_Key",        (0.20, 0.22, 0.26, 1), metallic=0.45, roughness=0.50)
    red         = gc.make_material("VT_Red",        (0.98, 0.16, 0.12, 1), metallic=0.0,  roughness=0.38,
                                   emission=(0.98, 0.16, 0.12, 1), emission_strength=2.5)
    green       = gc.make_material("VT_Green",      (0.12, 0.98, 0.28, 1), metallic=0.0,  roughness=0.38,
                                   emission=(0.12, 0.98, 0.28, 1), emission_strength=2.5)
    cyan        = gc.make_material("VT_CyanGlow",   (0.18, 0.88, 1.00, 1), metallic=0.2,  roughness=0.28,
                                   emission=(0.18, 0.88, 1.00, 1), emission_strength=1.2)
    display_mat = gc.make_material("VT_Display",    (0.08, 0.72, 0.92, 1), metallic=0.0,  roughness=0.18,
                                   emission=(0.08, 0.72, 0.92, 1), emission_strength=0.8)
    rubber      = gc.make_material("VT_Rubber",     (0.06, 0.06, 0.06, 1), metallic=0.0,  roughness=0.92)

    # --- wall frame ----------------------------------------------------------
    _build_frame(col, concrete, steel_dark)

    # --- vault door (pivot at hinge edge) ------------------------------------
    door_pivot = gc.add_empty("Vault_DoorPivot", location=(0, HINGE_Y, 0), col=col, size=0.08)
    door = gc.box("Vault_Door",
                  size=(DOOR_T, DOOR_W, DOOR_H),
                  location=(0, HINGE_Y + DOOR_W*0.5, DOOR_H*0.5),
                  col=col, mat=steel, bevel=0.025)
    gc.parent_keep_world(door, door_pivot)

    # Pressure/seal gasket ring around door face
    gasket = gc.ring("Vault_Gasket",
                     outer=min(DOOR_W, DOOR_H)*0.5 - 0.06,
                     inner=min(DOOR_W, DOOR_H)*0.5 - 0.13,
                     depth=0.04,
                     location=(DOOR_T*0.5+0.005, HINGE_Y+DOOR_W*0.5, DOOR_H*0.5),
                     col=col, mat=rubber, segments=80)
    gasket.rotation_euler = (0, math.pi/2, 0)
    gc.apply_transforms(gasket)
    gc.parent_keep_world(gasket, door)

    # Three concentric bolt rings on the door face
    for ri, (ro, rinn, nm) in enumerate([
        (0.82, 0.74, "Vault_LockingRing"),    # outer — the one Unity animates
        (0.58, 0.52, "Vault_BoltRing2"),
        (0.36, 0.31, "Vault_BoltRing3"),
    ]):
        robj = gc.ring(nm, outer=ro, inner=rinn, depth=0.06,
                       location=(DOOR_T*0.5+0.030+ri*0.005, HINGE_Y+DOOR_W*0.5, DOOR_H*0.5),
                       col=col, mat=(brass if ri == 0 else steel_dark), segments=80)
        robj.rotation_euler = (0, math.pi/2, 0)
        gc.apply_transforms(robj)
        gc.parent_keep_world(robj, door)
        # Bolt indicator tabs around each ring
        for k in range(8 - ri*2):
            a  = TWO_PI * k / (8 - ri*2)
            bz = DOOR_H*0.5 + ro*0.86 * math.cos(a)
            by = HINGE_Y + DOOR_W*0.5 + ro*0.86 * math.sin(a)
            tab = gc.box(f"{nm}_Tab{k}", size=(0.04, 0.055, 0.055),
                         location=(DOOR_T*0.5+0.058, by, bz), col=col, mat=steel_dark)
            gc.parent_keep_world(tab, door)

    # 6-spoke centre wheel
    _build_centre_wheel(col, steel, brass, steel_dark, door,
                        DOOR_T*0.5+0.055, HINGE_Y+DOOR_W*0.5, DOOR_H*0.5)

    # Hinge plates (3 knuckles on left edge)
    for k in range(3):
        hz_pos = DOOR_H * (0.18 + k*0.30)
        _build_hinge(col, steel, door, 0, HINGE_Y, hz_pos)

    # Bolt protrusions on door right edge (4 visible bolts)
    for k in range(4):
        bz = DOOR_H * (0.15 + k * 0.22)
        bolt = gc.cylinder(f"Vault_Bolt{k}", radius=0.038, depth=0.10,
                            location=(0, HINGE_Y+DOOR_W+0.05, bz),
                            col=col, mat=steel, segments=16, axis='Y')
        gc.parent_keep_world(bolt, door)

    # --- status lights (wall-mounted beside door) ----------------------------
    for i in range(3):
        lz = 2.35 - i*0.24
        gc.cylinder(f"Vault_StatusLight_{i}", radius=0.052, depth=0.045,
                    location=(DOOR_T*0.5+0.02, DOOR_W*0.5+0.22, lz),
                    col=col, mat=red, segments=20, axis='X')
        gc.ring(f"Vault_StatusRing_{i}", outer=0.060, inner=0.051, depth=0.020,
                location=(DOOR_T*0.5+0.02, DOOR_W*0.5+0.22, lz),
                col=col, mat=steel, segments=20)

    # --- keypad panel --------------------------------------------------------
    _build_keypad(col, steel_dark, key_mat, display_mat, brass)

    # --- combination dial (decorative, above keypad) -------------------------
    _build_combo_dial(col, steel, brass, steel_dark)

    # --- interior archive shelving (visible when door opens) -----------------
    for s in range(3):
        gc.box(f"Vault_Archive_Shelf{s}", size=(0.85, DOOR_W-0.35, 0.055),
               location=(-0.72, 0, 0.55+s*0.82), col=col, mat=steel_dark)
    gc.box("Vault_Archive_Glow", size=(0.025, DOOR_W-0.42, DOOR_H-0.65),
           location=(-1.10, 0, DOOR_H*0.5), col=col, mat=cyan)
    gc.box("Vault_Archive_BackWall", size=(0.02, DOOR_W-0.20, DOOR_H-0.30),
           location=(-1.20, 0, DOOR_H*0.5), col=col, mat=steel_dark)

    gc.parent_all_to_root(col, "Vault_Root")
    print("[gen_vault] museum-quality vault built: door, 3 bolt rings, 6-spoke wheel, keypad, dial.")
    return col


# ----------------------------------------------------------------- helpers

def _build_frame(col, concrete, steel_dark):
    FT = 0.55   # frame thickness
    gc.box("Vault_Frame_Top",  size=(FT, DOOR_W+0.90, 0.45),
           location=(0, 0, DOOR_H+0.225), col=col, mat=concrete)
    gc.box("Vault_Frame_L",    size=(FT, 0.45, DOOR_H+0.45),
           location=(0, -(DOOR_W*0.5+0.225), (DOOR_H+0.45)*0.5), col=col, mat=concrete)
    gc.box("Vault_Frame_R",    size=(FT, 0.45, DOOR_H+0.45),
           location=(0, (DOOR_W*0.5+0.225), (DOOR_H+0.45)*0.5), col=col, mat=concrete)
    gc.box("Vault_Frame_Floor",size=(FT, DOOR_W+0.90, 0.12),
           location=(0, 0, 0.06), col=col, mat=steel_dark)
    # Steel jamb liner
    gc.box("Vault_Jamb_Top",   size=(0.06, DOOR_W+0.10, 0.06),
           location=(0, 0, DOOR_H+0.03), col=col, mat=steel_dark)
    gc.box("Vault_Jamb_L",     size=(0.06, 0.06, DOOR_H),
           location=(0, -(DOOR_W*0.5+0.03), DOOR_H*0.5), col=col, mat=steel_dark)
    gc.box("Vault_Jamb_R",     size=(0.06, 0.06, DOOR_H),
           location=(0, (DOOR_W*0.5+0.03), DOOR_H*0.5), col=col, mat=steel_dark)


def _build_centre_wheel(col, steel, brass, dark, door, wx, wy, wz):
    hub = gc.cylinder("Vault_Wheel", radius=0.175, depth=0.115,
                       location=(wx, wy, wz), col=col, mat=steel, segments=36, axis='X')
    gc.parent_keep_world(hub, door)
    gc.ring("Vault_WheelRim", outer=0.178, inner=0.150, depth=0.115,
            location=(wx, wy, wz), col=col, mat=brass, segments=36).rotation_euler=(0,math.pi/2,0)
    for k in range(6):
        a = TWO_PI * k / 6
        sy = wy + 0.30 * math.cos(a)
        sz = wz + 0.30 * math.sin(a)
        spoke = gc.box(f"Vault_Spoke{k}", size=(0.055, 0.062, 0.62),
                       location=(wx, sy, sz), col=col, mat=steel)
        spoke.rotation_euler = (a, 0, 0)
        gc.apply_transforms(spoke)
        gc.parent_keep_world(spoke, door)
    # Grip knobs at each spoke end
    for k in range(6):
        a  = TWO_PI * k / 6
        ky = wy + 0.555 * math.cos(a)
        kz = wz + 0.555 * math.sin(a)
        knob = gc.cylinder(f"Vault_WheelKnob{k}", radius=0.034, depth=0.065,
                            location=(wx, ky, kz), col=col, mat=brass, segments=16, axis='X')
        gc.parent_keep_world(knob, door)


def _build_hinge(col, steel, door, hx, hy, hz):
    gc.box(f"Vault_Hinge_{hz:.2f}_Wall", size=(0.06, 0.14, 0.12),
           location=(hx, hy, hz), col=col, mat=steel)
    gc.box(f"Vault_Hinge_{hz:.2f}_Door", size=(0.06, 0.14, 0.12),
           location=(hx, hy+0.09, hz), col=col, mat=steel)
    knuckle = gc.cylinder(f"Vault_HingeKnuckle_{hz:.2f}", radius=0.028, depth=0.18,
                           location=(hx, hy, hz), col=col, mat=steel, segments=16, axis='Y')
    gc.parent_keep_world(knuckle, door)


def _build_keypad(col, panel_mat, key_mat, display_mat, brass):
    px = DOOR_T*0.5+0.02
    py = DOOR_W*0.5+0.58
    gc.box("Vault_KeypadPanel",   size=(0.065, 0.60, 0.88),
           location=(px, py, 1.30), col=col, mat=panel_mat, bevel=0.010)
    gc.box("Vault_Display",       size=(0.025, 0.50, 0.175),
           location=(px+0.038, py, 1.65), col=col, mat=display_mat)
    gc.text("Vault_DisplayLabel", "_ _ _ _ _ _ _", size=0.028,
            location=(px+0.055, py-0.26, 1.65),
            rotation=(0, math.pi/2, 0), col=col, mat=display_mat, extrude=0.001)

    keys  = gc.letters() + ["ENTER", "CLEAR"]
    COLS, ROWS = 7, 4
    kx_s, kz_s = 0.068, 0.115
    y0 = py - (COLS-1)*kx_s*0.5
    z0 = 1.44
    for idx, key in enumerate(keys):
        r = idx // COLS
        c = idx % COLS
        ky = y0 + c*kx_s
        kz = z0 - r*kz_s
        cap = gc.box(f"VaultKey_{key}", size=(0.032, 0.055, 0.055),
                     location=(px+0.042, ky, kz), col=col, mat=key_mat, bevel=0.005)
        glyph = "E" if key == "ENTER" else ("C" if key == "CLEAR" else key)
        gc.text(f"VaultKeyLabel_{key}", glyph, size=0.028,
                location=(px+0.062, ky-0.012, kz-0.012),
                rotation=(0, math.pi/2, 0), col=col, mat=display_mat, extrude=0.001)


def _build_combo_dial(col, steel, brass, dark):
    dx = DOOR_T*0.5+0.02
    dy = DOOR_W*0.5+0.58
    dz = 1.88
    gc.cylinder("Vault_DialBody",  radius=0.082, depth=0.030,
                location=(dx, dy, dz), col=col, mat=steel, segments=48, axis='X')
    gc.ring("Vault_DialRing",      outer=0.085, inner=0.078, depth=0.030,
            location=(dx, dy, dz), col=col, mat=brass, segments=48)
    gc.cylinder("Vault_DialKnob",  radius=0.022, depth=0.050,
                location=(dx, dy, dz), col=col, mat=brass, segments=24, axis='X')
    # Tick marks
    for k in range(100):
        a  = TWO_PI * k / 100
        tz = dz + 0.074 * math.cos(a)
        ty = dy + 0.074 * math.sin(a)
        h  = 0.014 if k % 5 == 0 else 0.007
        gc.box(f"Vault_DialTick{k}", size=(0.004, h, 0.004),
               location=(dx+0.016, ty, tz), col=col, mat=dark)


if __name__ == "__main__":
    build()
