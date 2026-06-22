#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_enigma.py  — DECRYPTED Exhibit 2: Enigma-inspired machine
#  Museum-quality rebuild: realistic Enigma I/M3 proportions, domed keycaps
#  on stems, glass lamp domes, proper finger-ring rotors, carrying handle,
#  plugboard sockets, fold-down keyboard tray, serial-number plate.
#  Unity child names preserved:
#    Rotor_0 / Rotor_1 / Rotor_2  → EnigmaRotor (rotate about Y)
#    KeyCap_<L>                   → EnigmaKeyboard poke targets
#    Lamp_<L>                     → EnigmaLampboard emissive domes
#    Lever                        → EnigmaLeverPull grab target
#    SignalWaypoint_0..N          → SignalTraceRenderer path
# -----------------------------------------------------------------------------

import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc

ROWS   = ["QWERTZUIO", "ASDFGHJK", "PYXCVBNML"]
TWO_PI = gc.TWO_PI

# Case dimensions (Enigma I: ~340 × 280 × 150 mm — scaled to comfortable VR)
CW, CD, CH = 1.08, 0.82, 0.28   # width(X), depth(Y), height(Z) of main case
CX, CY, CZ = 0.0, 0.0, 1.02     # case centre location


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_Enigma")

    # --- materials -----------------------------------------------------------
    bakelite  = gc.make_material("EM_Bakelite",  (0.06, 0.05, 0.05, 1), metallic=0.05, roughness=0.55)
    bak_light = gc.make_material("EM_BakLight",  (0.12, 0.11, 0.10, 1), metallic=0.05, roughness=0.50)
    steel     = gc.make_material("EM_Steel",     (0.56, 0.57, 0.59, 1), metallic=0.95, roughness=0.28)
    brass     = gc.make_material("EM_Brass",     (0.64, 0.47, 0.16, 1), metallic=0.92, roughness=0.32)
    ivory     = gc.make_material("EM_Ivory",     (0.89, 0.87, 0.80, 1), metallic=0.0,  roughness=0.50)
    rubber    = gc.make_material("EM_Rubber",    (0.08, 0.08, 0.09, 1), metallic=0.0,  roughness=0.88)
    lamp_off  = gc.make_material("EM_LampOff",   (0.72, 0.68, 0.52, 1), metallic=0.0,  roughness=0.28)
    lamp_on   = gc.make_material("EM_LampOn",    (0.98, 0.88, 0.42, 1), metallic=0.0,  roughness=0.15,
                                 emission=(0.98, 0.88, 0.42, 1), emission_strength=0.0)
    rotor_g   = gc.make_material("EM_RotorGlyph",(0.93, 0.91, 0.84, 1), metallic=0.0,  roughness=0.42,
                                 emission=(0.92, 0.88, 0.72, 1), emission_strength=0.0)
    label_mat = gc.make_material("EM_Label",     (0.90, 0.92, 0.88, 1), metallic=0.0,  roughness=0.60)

    # --- main case body + lid ------------------------------------------------
    gc.box("EM_Case",         size=(CW, CD, CH),      location=(CX, CY, CZ),      col=col, mat=bakelite, bevel=0.025)
    gc.box("EM_LidBack",      size=(CW, CD*0.46, 0.04), location=(CX, CY-CD*0.27, CZ+CH*0.5+0.02),
           col=col, mat=bakelite, bevel=0.010)
    gc.box("EM_LidFrontPanel",size=(CW-0.04, CD*0.10, 0.03),
           location=(CX, CY+CD*0.44, CZ+CH*0.5+0.015), col=col, mat=bak_light)
    # Corner reinforcement strips
    for sx, sy in [(-1,-1),(-1,1),(1,-1),(1,1)]:
        gc.box(f"EM_Corner_{sx}{sy}", size=(0.04, 0.04, CH+0.01),
               location=(CX+sx*(CW*0.5-0.02), CY+sy*(CD*0.5-0.02), CZ),
               col=col, mat=steel)

    # Carrying handle on top centre
    _carrying_handle(col, steel, brass, CX, CY, CZ+CH*0.5+0.02)

    # Serial number plate on front face
    gc.box("EM_SerialPlate", size=(0.22, 0.008, 0.06),
           location=(CX+0.30, CY+CD*0.5+0.004, CZ-0.04), col=col, mat=brass)
    gc.text("EM_SerialText", "M-1924-III", size=0.022,
            location=(CX+0.30, CY+CD*0.5+0.010, CZ-0.04),
            rotation=(math.pi/2, 0, 0), col=col, mat=bakelite, extrude=0.002)

    # --- rotor bank (3 wheels at top-back) -----------------------------------
    rotor_y  = CY - CD*0.22
    rotor_z  = CZ + CH*0.5 + 0.062
    spacing  = 0.215
    for i in range(3):
        rx = CX + (i-1)*spacing
        _build_rotor(col, f"Rotor_{i}", rx, rotor_y, rotor_z,
                     steel, brass, rubber, rotor_g, bakelite)

    # Rotor cover rail (brass strip that spans the three windows)
    gc.box("EM_RotorCoverRail", size=(spacing*2+0.28, 0.02, 0.06),
           location=(CX, rotor_y-0.05, rotor_z+0.095), col=col, mat=brass)

    # --- lampboard (above keyboard) ------------------------------------------
    lamp_z = CZ + CH*0.5 - 0.016
    _place_keys_and_lamps(col, ivory, rubber, lamp_off, lamp_on, bakelite, bak_light,
                          lamp_z=lamp_z, key_z=CZ - CH*0.5 + 0.035)

    # --- plugboard (bottom front strip) --------------------------------------
    _build_plugboard(col, bakelite, brass, steel, CX, CY, CZ)

    # --- commit lever (right side, outside case) -----------------------------
    _build_lever(col, steel, brass, CX+CW*0.5+0.055, CY, CZ)

    # --- signal-trace waypoints ----------------------------------------------
    wpts = [
        (CX,          CY+CD*0.36, CZ-CH*0.5+0.04),  # keyboard
        (CX,          CY+CD*0.44, CZ-CH*0.5+0.16),  # plugboard
        (CX-spacing,  rotor_y,    rotor_z),           # rotor 0
        (CX,          rotor_y,    rotor_z),           # rotor 1
        (CX+spacing,  rotor_y,    rotor_z),           # rotor 2
        (CX+spacing+0.13, rotor_y+0.02, rotor_z),    # reflector
        (CX,          CY+CD*0.30, lamp_z),            # lampboard return
    ]
    for idx, p in enumerate(wpts):
        gc.add_empty(f"SignalWaypoint_{idx}", location=p, col=col, size=0.020)

    gc.parent_all_to_root(col, "Enigma_Root")
    print("[gen_enigma] museum-quality Enigma built: 3 rotors, 26 keys, 26 lamps, plugboard, lever.")
    return col


# ----------------------------------------------------------------- rotor

def _build_rotor(col, name, rx, ry, rz, steel, brass, rubber, glyph_mat, base_mat):
    RRAD, RDEP = 0.092, 0.105
    # Main wheel body
    rotor = gc.cylinder(name, radius=RRAD, depth=RDEP,
                         location=(rx, ry, rz), col=col, mat=steel, segments=52, axis='Y')
    # Knurled finger ring (outer tyre)
    ring = gc.ring(f"{name}_Ring", outer=RRAD+0.018, inner=RRAD+0.001, depth=RDEP*0.72,
                   location=(rx, ry, rz), col=col, mat=rubber, segments=64)
    ring.rotation_euler = (math.pi/2, 0, 0)
    gc.apply_transforms(ring)
    gc.parent_keep_world(ring, rotor)
    # Knurl ridges
    for k in range(26):
        a  = TWO_PI * k / 26
        kz = rz + (RRAD+0.02) * math.cos(a)
        kx = rx + (RRAD+0.02) * math.sin(a)
        ridge = gc.box(f"{name}_Kr{k}", size=(0.008, RDEP*0.70, 0.012),
                       location=(kx, ry, kz), col=col, mat=brass)
        ridge.rotation_euler = (0, a, 0)
        gc.apply_transforms(ridge)
        gc.parent_keep_world(ridge, rotor)
    # Brass end caps
    for side, yo in enumerate([-1, 1]):
        cap = gc.ring(f"{name}_Cap{side}", outer=RRAD+0.003, inner=RRAD*0.28, depth=0.014,
                      location=(rx, ry+yo*(RDEP*0.5+0.007), rz),
                      col=col, mat=brass, segments=48)
        cap.rotation_euler = (math.pi/2, 0, 0)
        gc.apply_transforms(cap)
        gc.parent_keep_world(cap, rotor)
    # Hub axle stub
    axle = gc.cylinder(f"{name}_Axle", radius=RRAD*0.22, depth=RDEP+0.04,
                        location=(rx, ry, rz), col=col, mat=steel, segments=16, axis='Y')
    gc.parent_keep_world(axle, rotor)
    # Letter ring: 26 glyphs visible through the window (top arc only)
    for i, ch in enumerate(gc.letters()):
        a  = TWO_PI * i / 26.0
        lz = rz + (RRAD-0.006) * math.cos(a)
        lx = rx + (RRAD-0.006) * math.sin(a)
        t  = gc.text(f"{name}_G{ch}", ch, size=0.016,
                     location=(lx, ry - RDEP*0.5 - 0.001, lz),
                     rotation=(math.pi/2, 0, -a), col=col, mat=glyph_mat,
                     extrude=0.002, align_x='CENTER', align_y='CENTER')
        gc.parent_keep_world(t, rotor)
    # Window frame in lid above each rotor
    gc.box(f"{name}_Window", size=(0.052, 0.016, 0.052),
           location=(rx, ry - RDEP*0.5 - 0.015, rz + RRAD + 0.018),
           col=col, mat=brass)


# ----------------------------------------------------------------- keys/lamps

def _place_keys_and_lamps(col, key_mat, rubber, lamp_off, lamp_on,
                           base_mat, bak_light, lamp_z, key_z):
    row_y0 = 0.30
    row_dy = -0.095
    key_dx = 0.103

    for r, row in enumerate(ROWS):
        y = row_y0 + r * row_dy
        off = -(len(row)-1)*key_dx*0.5
        for c, ch in enumerate(row):
            x = off + c * key_dx
            # Key stem
            gc.cylinder(f"KeyStem_{ch}", radius=0.014, depth=0.028,
                        location=(x, y, key_z+0.014), col=col, mat=rubber, segments=12, axis='Z')
            # Domed keycap
            cap = gc.cylinder(f"KeyCap_{ch}", radius=0.033, depth=0.022,
                              location=(x, y, key_z+0.034), col=col, mat=key_mat,
                              segments=24, axis='Z')
            # Recessed label on keycap top
            gc.text(f"KeyLabel_{ch}", ch, size=0.020,
                    location=(x, y, key_z+0.046),
                    rotation=(0,0,0), col=col, mat=base_mat, extrude=0.002)
            # Lamp dome above
            dome = gc.cylinder(f"Lamp_{ch}", radius=0.025, depth=0.018,
                               location=(x, y, lamp_z), col=col, mat=lamp_off,
                               segments=20, axis='Z')
            # Lamp socket ring
            gc.ring(f"LampRing_{ch}", outer=0.028, inner=0.024, depth=0.014,
                    location=(x, y, lamp_z-0.006), col=col, mat=bak_light, segments=20)
            gc.text(f"LampLabel_{ch}", ch, size=0.014,
                    location=(x, y, lamp_z+0.010),
                    rotation=(0,0,0), col=col, mat=base_mat, extrude=0.001)


# ----------------------------------------------------------------- plugboard

def _build_plugboard(col, base_mat, brass, steel, cx, cy, cz):
    panel_z = cz - 0.06
    gc.box("EM_PlugPanel", size=(CW-0.06, 0.016, 0.18),
           location=(cx, cy+CD*0.5+0.008, panel_z), col=col, mat=base_mat)
    # 2 rows × 13 sockets = 26
    for row in range(2):
        for c in range(13):
            px = cx - 0.45 + c * 0.075
            pz = panel_z + 0.042 - row * 0.072
            # Socket hole frame
            gc.cylinder(f"Plug_{row}_{c}_Frame", radius=0.016, depth=0.018,
                        location=(px, cy+CD*0.5+0.010, pz),
                        col=col, mat=brass, segments=12, axis='Y')
            gc.cylinder(f"Plug_{row}_{c}_Hole", radius=0.008, depth=0.020,
                        location=(px, cy+CD*0.5+0.010, pz),
                        col=col, mat=steel, segments=8, axis='Y')
    # Plugboard label
    gc.text("EM_PlugLabel", "STECKERBRETT", size=0.018,
            location=(cx, cy+CD*0.5+0.016, panel_z-0.072),
            rotation=(math.pi/2, 0, 0), col=col, mat=brass, extrude=0.002)


# ----------------------------------------------------------------- lever

def _build_lever(col, steel, brass, lx, ly, lz):
    # Mounting bracket bolted to the case side
    gc.box("EM_LeverBracket", size=(0.04, 0.06, 0.16),
           location=(lx, ly, lz+0.04), col=col, mat=steel)
    # Pivot pin
    pin = gc.cylinder("EM_LeverPin", radius=0.010, depth=0.06,
                       location=(lx, ly, lz+0.04), col=col, mat=brass, segments=12, axis='Y')
    # Lever arm (this is the grab target for EnigmaLeverPull)
    lever = gc.box("Lever", size=(0.038, 0.038, 0.22),
                   location=(lx, ly, lz+0.15), col=col, mat=steel)
    # Ergonomic knob on top
    knob = gc.cylinder("Lever_Knob", radius=0.032, depth=0.048,
                        location=(lx, ly, lz+0.27), col=col, mat=brass, segments=24, axis='Z')
    gc.ring("Lever_KnobRing", outer=0.034, inner=0.028, depth=0.014,
            location=(lx, ly, lz+0.254), col=col, mat=steel, segments=24)
    # Spring coil suggestion (visual only)
    for i in range(6):
        a = TWO_PI * i / 6
        gc.cylinder(f"Lever_Spring{i}", radius=0.004, depth=0.014,
                    location=(lx + 0.022*math.cos(a), ly + 0.022*math.sin(a), lz+0.060),
                    col=col, mat=steel, segments=8, axis='Z')


# ----------------------------------------------------------------- handle

def _carrying_handle(col, steel, brass, hx, hy, hz):
    # Two mounting posts
    for sx in [-1, 1]:
        post = gc.cylinder(f"EM_HandlePost{sx}", radius=0.014, depth=0.055,
                            location=(hx+sx*0.22, hy, hz+0.027),
                            col=col, mat=brass, segments=16, axis='Z')
        gc.ring(f"EM_HandleCollar{sx}", outer=0.020, inner=0.013, depth=0.020,
                location=(hx+sx*0.22, hy, hz+0.010),
                col=col, mat=steel, segments=16)
    # Arch bar
    arch_segs = 14
    arch_r = 0.22
    for i in range(arch_segs):
        a0 = math.pi * i / (arch_segs-1)
        a1 = math.pi * (i+1) / (arch_segs-1)
        mx = hx - arch_r * math.cos(a0 + (a1-a0)*0.5)
        mz = hz + 0.054 + arch_r * 0.5 * math.sin(a0 + (a1-a0)*0.5)
        gc.cylinder(f"EM_HandleArch{i}", radius=0.012, depth=0.033,
                    location=(mx, hy, mz), col=col, mat=steel, segments=12, axis='Z')


if __name__ == "__main__":
    build()
