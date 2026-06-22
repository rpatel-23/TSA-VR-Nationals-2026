# 03 · Asset Production Pipeline

Every asset in DECRYPTED is **original and generated from first principles** — no
Asset Store content, no royalty-free downloads, no sampled audio. This document
explains how, and why the assets are delivered as **generators** (code) rather
than only as baked binaries.

## Why assets-as-code

For a small team, procedural generators are how you keep an art set
**reproducible, reviewable and original**:

- A generator is a **diff**. A reviewer can read exactly how the Enigma's rotor
  glyphs are placed; they cannot read a `.fbx`.
- It is **parametric**. Change the disk radius, the number of vault keys, the room
  height — re-run, done. No re-modelling.
- It is **provably original**. There is no question of where a mesh "came from".
- It **co-locates the wiring contract**. The generators name their anchors and sub-
  objects exactly as the C# expects (e.g. `OuterAnchor_A`, `Rotor_0`, `Lamp_Q`,
  `VaultKey_ENTER`, `SignalWaypoint_3`), so import-time wiring is mechanical.

The binary models are then produced by **running** `export_all.py`. This is the
same separation a studio uses between source and built artefacts.

Audio is the one place where listening matters more than reading, so it is shipped
**both** as the synthesis source *and* as **12 pre-baked WAVs** already in
`Assets/_Project/Audio/`.

## Two artifacts now use IMPORTED models (not the procedural generators)

Two exhibits use third-party models from `Assets/_Project/Art/Imported/` instead of
their Blender generators (see `ATTRIBUTION.txt` there for authors/licenses):

- **Vault door** (`vault_door`) — the **interactive** vault door in Room_VaultRoom.
  Its `Gate/Door` slab is wired to `VaultController._door` (swings open on unlock)
  and the combination wheel `CD.001` to `_lockingRing` (spins) and is grabbable.
  The **keypad puzzle stays procedural** (`VaultKeypad` + the 28 `VaultKey_*`). The
  old procedural door + frame are **disabled** (not deleted) under `Room_VaultRoom/Vault`.
- **Enigma** (`enigma_machine_1934`) — a **decorative backdrop** only in
  Room_WWIIRoom. The original was ~298K verts (decimated to ~67K via
  `Tooling/Blender/decimate_enigma.py`) and has no riggable parts, so the **playable
  Enigma remains the procedural one**; the import is visual-only.

Unity 2022.3 has **no native glTF importer**, so `Tooling/Blender/import_glb_to_fbx.py`
converts the imported `.glb` to `.fbx` (which Unity reads natively). Re-run it if the
GLBs change. The cipher disk, reveal sculpture, museum architecture and the Enigma's
playable parts are still produced by the procedural generators below.

## Blender pipeline (`Tooling/Blender/`)

Plain Blender Python (3.x/4.x compatible), no add-ons.

| File | Builds (collection) |
|------|---------------------|
| `gen_common.py` | Shared helpers: scene reset, collections, Principled-BSDF materials (3.x/4.x emission rename handled), boxes/cylinders/rings built without ops, extruded text, anchor empties, transform/modifier apply, join, Unity-axis FBX/GLB export. |
| `gen_cipher_disk.py` | `Decrypted_CipherDisk` — pedestal, fixed outer ring + rotating inner disk, 26+26 extruded letters and `OuterAnchor_*`/`InnerAnchor_*` empties, grip notch. |
| `gen_enigma.py` | `Decrypted_Enigma` — case, 3 rotors with lettered rings + windows, historical keyboard (`KeyCap_*`), lampboard (`Lamp_*`), plugboard, commit `Lever`, and `SignalWaypoint_0..N`. |
| `gen_architecture.py` | `Decrypted_Architecture` — six room shells (floor/walls/doorways/ceiling/trim) at the Unity room spacing. |
| `gen_vault.py` | `Decrypted_Vault` — door, locking ring + wheel/spokes, frame, 28-key pad (`VaultKey_*` incl. `ENTER`/`CLEAR`), status lights, archive shelving + glow. |
| `gen_reveal_sculpture.py` | `Decrypted_Reveal` — three morph stages (`Reveal_Stage_Roman/Gears/Circuit`) sharing a pivot, **plus** an optional single `Reveal_MorphMesh` with shape keys `toGears` (index 0) and `toCircuit` (index 1). |
| `export_all.py` | Runs every generator and exports **both FBX and GLB** to `Assets/_Project/Art/Generated/` (GLB is the robust fallback). |
| `validate_exports.py` | Standalone check (no Blender/Unity): confirms each `.fbx`/`.glb` exists, is over 10 KB, and has the right header. Prints a PASS/FAIL report. |

### Running it

```bash
# Whole set as BOTH .fbx and .glb (default):
blender --background --python Tooling/Blender/export_all.py

# Narrow to one format if you want:
blender --background --python Tooling/Blender/export_all.py -- --glb-only
blender --background --python Tooling/Blender/export_all.py -- --fbx-only

# Confirm the files are valid (no Blender needed):
py -3.13 Tooling/Blender/validate_exports.py

# A single asset, interactively (opens it in Blender):
blender --python Tooling/Blender/gen_enigma.py
```

Each generator calls `reset_scene()` on entry and returns its collection;
`export_all.py` builds and exports one asset at a time so a single headless session
produces the entire set. The exporter converts text/curves to mesh, validates every
mesh, and wraps the export in a try/except that prints a full traceback on failure
(no silent broken files).

> **Transform settings (do not "fix" these):** export uses `axis_forward='-Z',
> axis_up='Y'`, **`bake_space_transform=False`**, and **no** global
> `transform_apply`. Parenting is done by `parent_keep_world()`, which bakes correct
> *local* transforms that survive FBX export. Turning `bake_space_transform` on, or
> applying transforms to every object before export, re-creates the "exploded
> artifacts" bug fixed in Session 8 (see `CLAUDE.md`). GLB is the recommended format
> if FBX ever misbehaves.

### Opening / importing the generated files

> The files in `Assets/_Project/Art/Generated/` are **`.fbx` and `.glb`, not Blender
> `.blend` files.** Double-clicking one does **not** open it in Blender ("not a valid
> Blender file") — that only works for native `.blend`. To bring one into Blender use
> **File → Import → FBX** (or **glTF 2.0** for `.glb`). Unity imports both formats
> natively (no extra package on 2022.3).
>
> **After cloning the repo, run `git lfs pull`** before expecting any large binary
> assets to be present — a fresh clone leaves LFS-tracked files as tiny pointer stubs
> until then.

### Import into Unity

1. Run the pipeline; import `Assets/_Project/Art/Generated/` (Unity picks up the
   FBX/GLB automatically).
2. On each model's import settings: confirm scale (1 unit = 1 m), generate
   lightmap UVs for static pieces, set mesh compression as desired.
3. Build a **prefab per exhibit**, add the matching controller component, and wire
   the named child objects/anchors into the controller's serialized fields:
   - Caesar: inner disk → `XRGrabTwistDisk` + `_disk`; `OuterAnchor_*`/`InnerAnchor_*`
     → `_outerAnchors`/`_innerAnchors`.
   - Enigma: `Rotor_0..2` → `EnigmaRotor`s; `KeyCap_*` → keyboard; `Lamp_*` →
     lampboard; `Lever` → `EnigmaLeverPull`; `SignalWaypoint_*` → `SignalTraceRenderer`.
   - Vault: `Vault_Door` → `_door`; `Vault_LockingRing` → `_lockingRing`;
     `Vault_StatusLight_*` → `_statusLights`; `VaultKey_*` → keypad; `Vault_Display`
     → the TMP read-out; `Vault_Archive_*` → reveal emissive.
   - Reveal: `Reveal_Stage_*` → `_stages[0..2]`, **or** `Reveal_MorphMesh` →
     `_morphMesh` if you prefer the single-mesh blendshape path.
4. Assign materials. The generators set sensible Principled-BSDF bases; swap the
   FX surfaces (hologram sculpture, vault data panels) to the URP shaders in
   `Assets/_Project/Shaders/`.

## Audio pipeline (`Tooling/Audio/`)

See `04_Audio_Design.md` for the sound-by-sound detail. In short: `synth_engine.py`
is a from-scratch NumPy DSP toolkit (oscillators, ADSR/percussive envelopes, RBJ
biquad filters, a Schroeder reverb, soft-clip saturation, a seamless-loop helper
and a stdlib WAV writer); `generate_all_audio.py` composes the 12 sounds and writes
them to `Assets/_Project/Audio/{SFX,Ambient}/` with filenames that match the
`AudioManager` keys. The output is already in the repo; re-run to tweak:

```bash
python Tooling/Audio/generate_all_audio.py    # NumPy is the only dependency
```

## The art-polish pass (integrator)

The generators produce correct, fully-wired **geometry with base materials**. The
remaining craft — PBR texturing, trim sheets, wear/grime, decals, set dressing and
the lighting bake — is the art pass and is intentionally left to the integrator;
the pipeline makes that pass fast because the topology and naming are stable.
