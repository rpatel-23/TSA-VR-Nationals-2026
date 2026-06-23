# HANDOFF — Enigma visuals + Unity MCP bridge

_Created 2026-06-22. Read this if the chat/session that created it ended before the
work was finished (e.g. the Unity MCP bridge wouldn't load in that session). It captures
exact state so a fresh session can continue without re-investigating._

---

## 0. TL;DR — STATUS: ✅ COMPLETE (2026-06-22, bridge session)

Both items below are DONE, visually verified via the bridge, and the scene was saved
(`Decrypted_Main.unity`). Not yet committed. Details in §5.

1. ~~Get the Unity MCP bridge loaded~~ — DONE. Server was registered in `.mcp.json`
   AND project-local scope (`UnityMCP`, http://127.0.0.1:8080/mcp); after a reload the
   `mcp__UnityMCP__*` tools attached.
2. **Enigma exhibit visual fixes** — DONE on the generated/interactive Enigma
   (`DECRYPTED/Rooms/Room_WWIIRoom/Enigma`, the single EnigmaController instance):
   - Keycaps + their BoxCollider press-targets + letter labels enlarged (caps scaled
     1.3×, grid spread 1.25× so no overlap, labels reseated onto the cap tops).
   - Rotor letters rebuilt into a legible cipher-disk ring (was collapsed to the hub on
     FBX import). Alignment verified exactly: step 12/0/2 → M/A/C at the windows.

---

## 1. Unity MCP bridge status

**Server:** running & reachable — `http://127.0.0.1:8080/mcp` (HTTPLocal transport,
MCP-for-Unity v9.7.3, "Session Active" in the Unity window). Verified: HTTP 200 to an
`initialize` POST, and `claude mcp get UnityMCP` → **✔ Connected**.

**Config (DONE this session):** UnityMCP is registered in the project file
[.mcp.json](.mcp.json):
```json
{ "mcpServers": { "UnityMCP": { "type": "http", "url": "http://127.0.0.1:8080/mcp" } } }
```
A stale **local-scope** duplicate (in `~/.claude.json` under the project key, with
forward-slash/drive-case `C:/Users/...`) was removed because it shadowed the project
entry and was NOT being loaded by the VSCode extension. Backup: `~/.claude.json.bak`.

**The remaining problem:** the VSCode-extension Claude session only loads MCP servers at
startup. In the session that wrote this, available MCP servers were only
`claude_design, Higgsfield, Canva, Microsoft 365, Figma, Google Drive, claude-vscode`
— **UnityMCP was absent**, so none of the `manage_scene` / `manage_gameobject` /
`read_console` tools existed.

**To bring it online (do this first):**
1. Reload the window (`Ctrl+Shift+P` → "Developer: Reload Window") OR start a fresh
   Claude Code session in this folder.
2. If prompted to **trust/enable** the project MCP server from `.mcp.json`, **approve it.**
3. Verify in-session with a ToolSearch for `unity` — you should see
   `mcp__UnityMCP__manage_scene`, `mcp__UnityMCP__manage_gameobject`,
   `mcp__UnityMCP__read_console`, etc. If still absent, the extension isn't loading
   project `.mcp.json` servers — fall back to the terminal `claude` CLI (which already
   resolves UnityMCP as "Connected") or the MCP-for-Unity window's "Configure" + a full
   app restart.
4. The `unity-mcp-skill` is installed — read it for tool schemas/workflow patterns.

If the bridge truly cannot be loaded, the Enigma work can still be done **without** it by
editing the Blender generator + scene directly (see §2.4), but you lose live visual
verification, which the user explicitly wants.

---

## 2. The Enigma task (the actual request)

### 2.1 Where the geometry comes from
- **Blender generator:** [Tooling/Blender/gen_enigma.py](Tooling/Blender/gen_enigma.py).
  Output FBX → `Assets/_Project/Art/Generated/Enigma.fbx` (regenerate via
  `Tooling/Blender/export_all.py`; see CLAUDE.md §7). Key functions:
  - `place_keys_and_lamps()` — builds `KeyCap_<L>` cylinders (**radius 0.035, depth
    0.03, axis Z**), child `KeyLabel_<L>` text (size 0.022), and `Lamp_<L>` discs
    (radius 0.026). Rows = historical QWERTZ `["QWERTZUIO","ASDFGHJK","PYXCVBNML"]`.
  - `place_rotor_glyphs()` — **THIS is the bunched-letters culprit.** Places 26
    `Rotor_i_G<L>` text meshes (size **0.018**) on a ring of **radius 0.092** in the
    X/Z plane at `y = cy - 0.052`, each rotated `(pi/2, 0, -ang)`. Because every glyph
    gets a different `-ang` rotation around a near-flat ring, from the player's view
    they overlap and sit at all different tilts → illegible. Rotor cylinder itself:
    radius 0.085, depth 0.10, axis Y.
- **Layout helper (Unity editor):**
  [Assets/_Project/Scripts/Editor/EnigmaRebuilder.cs](Assets/_Project/Scripts/Editor/EnigmaRebuilder.cs)
  — menu `DECRYPTED ▸ Rebuild Enigma Layout`. Only **repositions** existing children
  (`KeyCap_`, `Lamp_`, `Rotor_0/1/2`); it does NOT create geometry or the rotor glyphs.

### 2.2 Interaction components (what must keep working)
- **Keycaps:** `KeyCap_<L>` are the poke targets, driven by
  [EnigmaKeyboard.cs](Assets/_Project/Scripts/Interaction/EnigmaKeyboard.cs) (+ likely
  `PokeButton.cs`). When enlarging a keycap, **enlarge its collider too** (the thing
  that registers the press) — verify whether the press is a `PokeButton`/collider on
  the KeyCap or driven centrally by EnigmaKeyboard.
- **Rotors:** each `Rotor_0/1/2` has
  [XRGrabTwistDisk.cs](Assets/_Project/Scripts/Interaction/XRGrabTwistDisk.cs)
  (`_localAxis` default = up/Y, 26 detents) + `EnigmaRotor` (distinct Rotor Index
  0/1/2). The grabbable letter ring must stay rigid to the rotor so it spins with it.
- **Brain:** [EnigmaController.cs](Assets/_Project/Scripts/Interaction/EnigmaController.cs)
  + [EnigmaMachine.cs](Assets/_Project/Scripts/Interaction/EnigmaMachine.cs).
  Puzzle (DO NOT CHANGE): key **MAC**, ciphertext **ZLDFDQO** → **VICTORY**.

### 2.3 Identify the right Enigma instance (generated vs imported)
Per CLAUDE.md §9, the scene may contain BOTH the real interactive exhibit and Tejas's
decorative fake `Hero`. The **generated/interactive** one is the GameObject hierarchy
whose root (or children) carry `EnigmaController` / `EnigmaRotor` / `EnigmaKeyboard`
and the `KeyCap_*`, `Rotor_*` children. Confirm via the bridge
(`manage_gameobject` search for `EnigmaController`) before editing. Scene:
[Assets/_Project/Scenes/Decrypted_Main.unity](Assets/_Project/Scenes/Decrypted_Main.unity),
room `Room_WWIIRoom`.

### 2.4 Recommended fix approach
Prefer a **procedural Unity-side fix** so it's editable, viewable in-editor, and
operates on the in-scene generated instance (matches "generated not imported" + lets
you visually verify):
- **Rotor letters:** for each rotor, replace the bunched `Rotor_i_G*` glyphs with 26
  evenly-spaced, larger labels arranged as a cipher-disk ring around the rim, all
  oriented to read facing outward (uniform up-vector), parented rigidly to the rotor so
  they spin. Either (a) extend `EnigmaRebuilder` / add a new `DECRYPTED ▸ Fix Enigma
  Rotor Letters` editor command, or (b) fix `place_rotor_glyphs()` in gen_enigma.py
  (bigger size, larger radius, consistent orientation) and regenerate+reimport. Option
  (a) is faster to iterate and avoids the Blender/LFS round-trip.
- **Keycaps + buttons:** scale up `KeyCap_<L>` (radius ~0.035 → larger) and increase
  the poke collider to match; re-run `Rebuild Enigma Layout` (or bump `keySpacingX/Z`)
  so enlarged caps don't overlap.
- **TEST:** use the bridge to view the WWII room / Enigma after each change; iterate
  until legible and hittable BEFORE telling the user it's good.

---

## 3. Already completed earlier this session (do NOT redo)
Player-height / "start high → fall → low" bug fixed (changes in the scene + script,
**not yet committed**):
- [PlayerHeightConfig.cs](Assets/_Project/Scripts/Interaction/PlayerHeightConfig.cs):
  `_floorHeightOffset` default → 0, `_extraHeightBoost` default → 0.3 (the single
  height lever; 0.3≈+1ft, 0.6≈+2ft, 0.9≈+3ft).
- Scene `Decrypted_Main.unity`: ContinuousMoveProvider `m_UseGravity: 1→0` (stops the
  fall); XR rig root `localPosition.y 2.039→0` (stops high spawn); PlayerHeightConfig
  serialized `_floorHeightOffset -1.14→0`, `_extraHeightBoost 1.06→0.3`; Camera Offset
  child `localPosition.y -0.074→0.3`.

## 4. Housekeeping
- Save the scene in Unity (Ctrl+S) before committing. User commits via GitHub Desktop;
  commit/push only when asked.
- `git status` at handoff: modified `Decrypted_Main.unity`, `PlayerHeightConfig.cs`;
  new `.mcp.json`, `HANDOFF.md`.

## 5. Enigma fix — exactly what was changed (2026-06-22, bridge session)
All edits were applied to the in-scene GameObjects via the MCP bridge (execute_code),
then the scene was saved. No Blender/FBX regeneration was done.

**Rotor letters (Rotor_0/1/2 `Rotor_i_G<A..Z>` glyphs):** root cause was the 26 baked
glyphs all collapsed to localPosition (0,0,0) on FBX import (overlapping blob). Rebuilt
each rotor's ring procedurally:
- Spin axis is local **Y** (`_localAxis=(0,1,0)`, 26 detents, +13.85°/step). Window
  (reading reference) is at local angle ≈96° (top), on the player-facing −Y side.
- Each glyph k placed by rotating a "window template" back by k steps:
  `localPos = AngleAxis(-k*13.846, up) * basePoint`, `localRot = AngleAxis(-k*13.846, up) * (Inverse(rotor.rotation)*LookRotation(back, up))`.
  basePoint = ring at **R=0.072**, **faceY=-0.057**, glyph **localScale=1.2**.
- Guarantees letter k sits **upright at the window at step k** → puzzle still readable
  (verified: steps 12/0/2 = M/A/C at windows). Radial cipher-disk look, not mirrored.
- To re-tune: re-run the same placement loop with different R / scale / faceY.

**Keycaps (`KeyCap_<A..Z>`, each = MeshFilter+MeshRenderer+BoxCollider+PokeButton):**
- localScale set to **(1.3, 1.3, 1.0)** — grows the poke face + BoxCollider (press
  target) + child label by 30%, keeps height (cap local Z = world up).
- Grid spread **1.25×** about centroid (0, 1.265, 0.233) in local X & Z so the bigger
  caps don't overlap (nearest-neighbour 0.085→0.106, footprint 0.07→0.091, gap ~0.015).
  Still inside the 1.14-wide case.
- `KeyLabel_<L>` were floating ~3.5 cm above the caps; reseated to each cap's top face
  (`labelY = capTopY + 0.003`), now ~1.9 cm tall.

**Optional follow-up (NOT done):** mirror these numbers into
`Tooling/Blender/gen_enigma.py` (`place_rotor_glyphs` ring + keycap radius/spacing) so a
future FBX regen doesn't reintroduce the bunched glyphs / small keys. Current scene
instance is independent of that regen, so it's safe as-is for the showcase.
