# DECRYPTED — A Walk Through the History of Secret Writing

A linear, single-player **VR museum for Meta Quest** that teaches the history of
cryptography through three hands-on exhibits — a Roman **Caesar cipher disk**, a
WWII **Enigma-inspired machine**, and a modern **digital-security vault** — ending
in a synthesis chamber where a sculpture morphs through all three eras. There is
**no spoken narration**: every idea is taught through interaction, diegetic audio,
visualisations and TextMeshPro plaques.

> *"From Caesar's alphabet shifts to modern digital security, cryptography
> protects information by transforming meaning into secrets only the intended
> recipient can reveal."* — the closing plaque

**Target:** Unity 2022.3 LTS · Universal Render Pipeline · XR Interaction Toolkit
· Oculus XR Plugin · Meta Quest standalone · **72 FPS** · baked lighting.

---

## What this package is (read this first)

This repository is a **complete, real Unity project tree** — every C# system, every
shader, the full audio toolchain, and the full 3D asset toolchain — plus a
documentation set. A few deliberate, senior-engineering choices about *form*:

- **All gameplay/runtime code is included in full** — 52 C# scripts, no stubs, no
  placeholders, no "TODO" bodies. Drop them into a Unity project and they compile
  and wire together through a single event bus.
- **3D assets are delivered as procedural generators** (Blender Python under
  `Tooling/Blender/`) rather than binary `.fbx` files. Running them rebuilds the
  entire art set — the cipher disk, Enigma machine, vault, museum shell and reveal
  sculpture — with named anchors that map 1:1 onto the C# wiring. This keeps the
  art **original, reproducible and reviewable** (a diff instead of an opaque blob),
  which is exactly how a small team would version this.
- **Audio is delivered BOTH ways.** The from-scratch NumPy synthesis toolkit is in
  `Tooling/Audio/`, **and its output is already baked** — 12 real `.wav` files live
  in `Assets/_Project/Audio/`. Nothing about the sound is hand-wavy; you can listen
  to it today, and you can regenerate or tweak it from the scripts.

Everything original. No Asset Store content, no royalty-free downloads, no sampled
audio. See `Documentation/03_Asset_Production_Pipeline.md` for the full rationale.

---

## Quickstart

1. **Create** a Unity **2022.3 LTS** project (3D URP template).
2. **Copy** the `Assets/` folder from here into your project's `Assets/`.
3. Install packages (Package Manager): **Universal RP**, **XR Plugin Management**,
   **Oculus XR Plugin**, **XR Interaction Toolkit (~2.5)**, **TextMeshPro**,
   **Input System**. Full version notes in `01_Unity_Setup_Guide.md`.
4. Run **`DECRYPTED ▸ Build ▸ Configure for Quest`** (added by `BuildConfigurator.cs`)
   to apply player/quality/build settings, then follow the printed checklist for
   the XR + URP asset assignments.
5. Run **`DECRYPTED ▸ Build Scene Skeleton`** (added by `SceneBuilder.cs`) to stamp
   out the wired manager rig, camera rig and six room roots.
5b. Run **`DECRYPTED ▸ Museum ▸ Build Full Museum`** (`Ctrl/Cmd+Shift+M`, added by
   the editor toolkit in `Scripts/Editor/Museum/`) to construct the full,
   dense museum — grand architecture, hero centerpieces, display cases, plaques,
   a portrait gallery, timelines, signage and decor in all six galleries. It is
   idempotent and reversible (`DECRYPTED ▸ Museum ▸ Clear Generated Dressing`).
   See `07_Museum_Expansion.md`.
6. **Generate the art** (optional but recommended): run the Blender pipeline
   (`blender --background --python Tooling/Blender/export_all.py`) and import the
   resulting models from `Assets/_Project/Art/Generated/`.
7. **Assign the audio**: the WAVs in `Assets/_Project/Audio/` are pre-baked — add
   them to the `AudioManager` clip library by key (or regenerate via
   `Tooling/Audio/generate_all_audio.py`).
8. Press **Play** (Demo Mode auto-plays the whole walkthrough for recording).

---

## Project layout

```
Decrypted-VR/
├── Assets/_Project/
│   ├── Scripts/
│   │   ├── Core/         GameManager, AutoProgressionController, SceneController,
│   │   │                 DemoDirector, EventBus, GameEvents, GameState, SaveSystem
│   │   ├── Managers/     AudioManager, PerformanceManager, InteractionManager, UIManager,
│   │   │                 NarrationController, RoomNarratorController, RoomMusicController
│   │   ├── Interaction/  Caesar disk, full Enigma rig, vault, final reveal, splash +
│   │   │                 tutorial, PokeButton/RoomButton, RoomDoor, CountdownPanel,
│   │   │                 VRHandPoser, PlayerHeightConfig, ArtifactAnimators, XRGrabTwistDisk
│   │   ├── Visuals/      ScreenFader, RoomActivator, PlaqueController, SignalTraceRenderer,
│   │   │                 MuseumAmbience, WorldLabel
│   │   ├── Util/         Singleton, AudioSynth (runtime fallback synthesis)
│   │   └── Editor/       BuildConfigurator, SceneBuilder, EnigmaRebuilder, Museum/ toolkit
│   ├── Shaders/          Hologram_URP, EnergyScanline_URP
│   └── Audio/            SFX/ + Ambient/  (12 pre-baked WAVs)
├── Tooling/
│   ├── Blender/          gen_common + 5 generators + export_all (+ validate/import helpers)
│   └── Audio/            synth_engine + generate_all_audio
└── Documentation/        00–08 guides (overview → setup → art → audio → optimisation → storyboard → museum → animation)
```

## Documentation index

| File | Covers |
|------|--------|
| `00_Project_Overview.md`        | Vision, learning design, architecture, the puzzle chain |
| `01_Unity_Setup_Guide.md`       | Packages, XR/URP config, build settings, scripting defines |
| `02_Environment_Design.md`      | Room-by-room layout, flow, lighting, wayfinding |
| `03_Asset_Production_Pipeline.md` | Why assets-as-code; Blender + audio pipelines; import steps |
| `04_Audio_Design.md`            | Synthesis approach, the 12 sounds, mixer + spatialisation |
| `05_Optimization.md`            | The 72 FPS budget and every technique used to hold it |
| `06_Storyboard_and_Recording.md`| Minute-by-minute walkthrough + capture workflow |
| `07_Museum_Expansion.md`        | The procedural museum-dressing toolkit: dense galleries, exhibits, plaques, signage and decor built from one menu command |
| `08_Animation_Pipeline.md`      | Blender-baked artifact animations and the C# animator routing (`ArtifactAnimators`) |
| `08_Fix_Log_and_On_Device_Verification.md` | Running log of fixes and on-Quest verification notes |

## The puzzle chain (one source of truth)

- **Caesar disk** — ciphertext `FURVV WKH UXELFRQ`, shift **+3** → `CROSS THE RUBICON`.
- **Enigma** — key **MAC**, `ZLDFDQO` → **`VICTORY`** (a reciprocal, pedagogically
  calibrated machine — see `00_Project_Overview.md`; this is *not* a bit-exact
  reproduction of a historical Enigma).
- **Vault** — passphrase is the Enigma plaintext, **`VICTORY`** (auto-sourced in
  code so the two exhibits can never drift apart).

## License / originality

All code, shader, audio and 3D-asset content here is original to this project and
free of third-party assets. Synthesised audio is generated from first principles
with NumPy; all meshes are generated from first principles with Blender Python.
