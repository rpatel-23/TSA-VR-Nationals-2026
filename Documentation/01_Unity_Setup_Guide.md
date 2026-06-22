# 01 · Unity Setup Guide

Target: **Unity 2022.3 LTS**, Universal Render Pipeline, Meta Quest standalone
(Android). This guide takes a fresh project to a buildable Quest app.

## 0. Clone with Git LFS (do this first)

This project stores binary art/audio (`*.fbx`, `*.wav`) via **Git LFS**. A plain
clone leaves those as tiny pointer files, which import as **empty/broken exhibits**.
Immediately after cloning:

```
git lfs install      # once per machine
git lfs pull         # fetch the real FBX/WAV binaries for this clone
```

If exhibits look empty/scattered or you see import errors, the cause is almost
always unresolved LFS pointers — run `git lfs pull` and reimport.

## 1. Create the project

Use Unity Hub → **2022.3 LTS** → **3D (URP)** template. (Starting from the URP
template saves you the pipeline-asset bootstrap.) Then copy this repo's `Assets/`
into the project.

## 2. Packages

Install via **Window ▸ Package Manager** (Unity Registry). Versions below are the
known-good baseline for 2022.3; patch versions may differ:

| Package | Version | Why |
|---------|---------|-----|
| Universal RP | 14.0.x | The render pipeline. Mobile/VR-friendly, single-pass. |
| XR Plugin Management | 4.4.x | Loads XR providers at runtime. |
| Oculus XR Plugin | 4.2.x | Quest runtime, foveation, refresh-rate control. |
| XR Interaction Toolkit | 2.5.x | Interactors/interactables (grab, poke, ray). |
| Input System | 1.7.x | Required by XRI 2.x. |
| TextMeshPro | 3.0.x | All in-world text/plaques. |

After adding Input System, when prompted to enable the new backend, choose **Yes**
(this restarts the editor).

> The exhibit code references XRI base types (`XRBaseInteractable`,
> `XRSimpleInteractable`). If you change XRI major versions, check those names.

## 3. One-click baseline

Run **`DECRYPTED ▸ Build ▸ Configure for Quest`**. `BuildConfigurator.cs` applies:

- Company/product/bundle identifier.
- **Color space = Linear** (correct PBR + baked lighting).
- Graphics API = **OpenGLES3** (Quest 1-safe; Vulkan optional — see below).
- Scripting backend = **IL2CPP**, target architecture = **ARM64**.
- Android **min SDK 29**, target SDK = Auto.
- GPU skinning, multithreaded rendering, direct-to-surface blit, 16-bit depth.
- Android texture compression = **ASTC**.
- Switches the active build target to Android.

It then prints the remaining package-owned steps (also listed below).

## 4. XR Plug-in Management

**Project Settings ▸ XR Plug-in Management**:

1. Install the providers if prompted.
2. On the **Android** tab, tick **Oculus**.
3. Open the **Oculus** settings sub-page and set **Stereo Rendering Mode =
   Multiview** (single-pass instanced — roughly halves per-frame CPU/draw cost vs
   multi-pass; essential for the 72 FPS budget).
4. Add the scripting define **`OCULUS_XR_PRESENT`** in **Project Settings ▸ Player
   ▸ Other Settings ▸ Scripting Define Symbols** (Android). This compiles in
   `PerformanceManager`'s fixed-foveation and refresh-rate calls. Without it the
   manager still runs; those Oculus-specific calls are simply skipped.

## 5. Universal Render Pipeline

1. **Project Settings ▸ Graphics** → assign your **URP Asset** under *Scriptable
   Render Pipeline Settings*.
2. **Project Settings ▸ Quality** → for the Android tier, assign the same URP Asset
   (or a mobile-tuned variant).
3. On the URP Asset:
   - **MSAA = 4x** (cheap on tile GPUs and far nicer than post AA in VR).
   - **HDR = Off** (saves bandwidth; we don't need it for this art style).
   - **Shadow Distance ≈ 18 m**, **1 cascade** (rooms are small; see `05`).
   - Disable extra renderer features you don't use.
4. The two custom shaders (`Hologram_URP`, `EnergyScanline_URP`) are URP
   forward-pass shaders and need no special setup beyond URP being active.

## 6. Lighting

This is a **baked-lighting** project (see `05` for the perf reasoning).

1. Mark static geometry (walls, floors, fixtures) as **Contributing GI / Static**.
2. **Window ▸ Rendering ▸ Lighting**: Mixed/Baked directional + baked area/point
   lights per room. Generate Lighting.
3. Add a **Reflection Probe** per room and reference it in that room's
   `RoomDescriptor` so `SceneController` enables the right one on entry.

## 7. Scene

Run **`DECRYPTED ▸ Build Scene Skeleton`** (`SceneBuilder.cs`). It creates:

- A **Managers** object with `GameManager`, `SceneController`, `AudioManager`,
  `PerformanceManager`, `InteractionManager`, `UIManager`, `DemoDirector`.
- A **camera rig** placeholder (`XR Origin ▸ Camera Offset ▸ Main Camera`) with
  `ScreenFader` attached — replace/parent this with your XRI/Oculus rig.
- Six **room roots** (`Room_Splash` … `Room_Reveal`), each with a `RoomActivator`,
  a `PlayerAnchor` and a floor.
- **Auto-wiring**: the `SceneController` room list, camera and fader references, and
  `GameManager`'s `SceneController` reference, all populated via `SerializedObject`.

Then add the exhibit prefabs (built from the Blender pipeline) under the matching
room roots, and place each room's `playerAnchor` where the visitor should spawn.

Save the scene as **`Decrypted_Main`** and add it to **Build Settings ▸ Scenes In
Build**.

## 8. Audio assignment

The 12 WAVs in `Assets/_Project/Audio/` are pre-baked. Select the `AudioManager`
and add each clip to its **Clip Library**, using the filename (without extension)
as the **key** (e.g. `sfx_lever_pull`, `amb_vault`). Leave **Synthesise Missing**
on as a safety net. Details and the full key list are in `04_Audio_Design.md`.

## 9. Build & deploy

1. Connect the Quest, enable Developer Mode, authorise USB.
2. **Build Settings ▸ Android ▸ Build And Run**.
3. First IL2CPP build is slow; subsequent builds are incremental.

## Troubleshooting

- **Pink materials** → URP Asset not assigned in Graphics/Quality (step 5).
- **Black screen on device** → XR provider not enabled, or Linear color space with
  an unsupported graphics API; confirm OpenGLES3 + Oculus enabled.
- **`OCULUS_XR_PRESENT` errors** → you added the define without the Oculus package;
  either install the package or remove the define (the code degrades gracefully).
- **Stutter / low FPS** → confirm Multiview stereo mode and baked (not realtime)
  lighting; see `05_Optimization.md`.
