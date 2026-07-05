# 08 · Fix Log & On-Device Verification

This document records the round of fixes applied to finale/completion, stability,
performance, coherence, and security, and lists everything that still needs a
**headset** to confirm. Nothing below is marked "verified on device" — the recent
hands / doors / labels / narrator / finale work has been validated **in-repo only**
(compiles clean, scene saved, edit-mode checks) and must be run on a Quest.

---

## What changed (resolved in-repo)

| # | Change | Status |
|---|--------|--------|
| 1 | `FinalRevealController` re-added to `RevealSculpture`; `_stages=[Roman,Gears,Circuit]`, `_spinRoot=Reveal_Pivot`. Gears/Circuit disabled at rest (no stacked overlap). | Resolved in-repo; morph needs headset confirm |
| 2 | `Room_Complete` built (floor, anchor, calm closing panel with the **verbatim** plaque), added as the 7th `SceneController` descriptor. Opening the Reveal door now advances into a real completion state via the existing `ScreenFader`. | Resolved in-repo; needs headset confirm |
| 3 | Teardown null-guards added (`RoomDoor.OnDisable` stops coroutines + drops refs; `OpenRoutine`/`LateUpdate` null-check the slab/label; `WorldLabel.OnDestroy` guarded; poser guards inputs). Missing-component null-derefs (#1, #4) that were the most likely cause are fixed. | Guards resolved in-repo; root-cause repro still needed (below) |
| 4 | `SplashScreenController` restored: a glowing **PLAY** button (PokeButton) built and wired; PLAY → `ExperienceStartedEvent` → Atrium. Splash is no longer inert. | Resolved in-repo; needs headset confirm |
| 5 | Transition fades are unscaled: `ScreenFader` uses `Time.unscaledDeltaTime`; `SceneController.TransitionTo` uses frame yields (no scaled `WaitForSeconds`). Time-scale → 0 cannot stall a fade. | Resolved in-repo; needs headset confirm under time-scaling |
| 6/16 | All `RoomDoor` values are serialized (handle reach 0.35 m, swing angle 90°, swing 0.6 s, handle height, pulse cycle 1.5 s). Door geometry is now built in edit mode (visible/tunable); placement is the `ExitDoor` transform, editable per room. | Resolved in-repo |
| 7 | Hand rest orientation (`_restEuler`, Left +90 Z / Right −90 Z) and curl direction (`_curlSign`) are serialized with "Flip Palm Roll" / "Flip Curl Direction" context-menu commands and an edit-mode `_previewCurl` slider. Nothing hardcoded. | Resolved in-repo; **direction is still a guess until headset check** |
| 8 | Door activation glow is **emissive-only** — the 6 realtime door point-lights were removed. Zero realtime lights added by the door system. | Resolved in-repo |
| 9 | `Time.fixedDeltaTime = 1/72`; URP-Quest HDR off, soft shadows off. Full bake + per-vertex additional-lights procedure documented in `05_Optimization.md`. | Settings resolved; **bake must be done in-editor** |
| 10 | Spoken narration removed: `NarratorVoice` deleted (README mandates no spoken narration; device TTS is absent on many Quests). `NarrationController` captions/plaques are the single text layer. | Resolved in-repo |
| 12 | Ancient Room ambient corrected to `amb_ancient` (RoomActivator + descriptor). | Resolved in-repo |
| 13 | `DECRYPTED/Hologram_URP` assigned to the three reveal stages (it carries the `_Dissolve`/`_EmissionColor` the controller drives). | Resolved in-repo; needs headset confirm |
| 18 | Dead `ManualFlowController.cs` deleted. | Resolved in-repo |
| 19 | `git lfs pull` added to `01_Unity_Setup_Guide.md`. | Resolved in-repo |

---

## #3 — Crash (SIGSEGV) reproduction procedure

The earlier intermittent SIGSEGV was a JNI→`libunity` render/lifecycle crash. Two
likely contributors are now removed: (a) the missing `FinalRevealController`/splash
components (null-deref on teardown), and (b) the realtime door lights churning the
render path. To capture the **actual** stack if it recurs, do NOT rely on adb
proximity toggles (they thrash the XR session in a way real wear does not):

1. **Build Settings ▸ Development Build = ON**, Autoconnect Profiler optional.
2. Build & Run to the Quest, then **wear the headset** (real proximity) and play
   Splash → Complete normally.
3. In a terminal: `adb logcat -s Unity DEBUG AndroidRuntime CRASH` (keep it running).
4. Reproduce by wearing + playing; when it crashes, the **Unity log + tombstone**
   stack is captured by logcat (Development Build symbolicates managed frames).
5. Also pull `adb shell dumpsys dropbox --print` for the native tombstone.
6. Note the StereoRenderingMode: the docs target **Multiview**, but the crash fix
   set **MultiPass** (`m_StereoRenderingModeAndroid=0`) plus SymmetricProjection/
   PhaseSync off. If you re-enable Multiview, re-test this path specifically.

If the stack lands in our code, fix the specific null/threading issue. All
main-thread-only Unity APIs (render targets, transforms, audio) must stay on the
main thread — the narrator's off-main-thread TTS callback was a risk and is now
removed entirely.

---

## #17 — Security: the leaked Personal Access Token (do this yourself)

A token is **not** present in any tracked/committed file (scanned). It only exists
in this clone's **local** `.git/config` remote URL (not pushed). Still, treat it as
compromised and rotate it. **Claude did not read, print, or store the token.**

You must, by hand (these touch a live credential):
1. **Revoke** the token on GitHub: Settings ▸ Developer settings ▸ Personal access
   tokens ▸ delete the leaked token.
2. **Generate a new** fine-scoped token and store it in a **credential manager**
   (`git config --global credential.helper manager` on Windows), never in a URL.
3. **De-embed** it from the remote + LFS URLs:
   ```
   git remote set-url origin https://github.com/<owner>/<repo>.git
   git config --unset lfs.https://github.com/<owner>/<repo>.git/info/lfs.access  # if set
   ```
   Then let the credential helper supply auth on demand.
4. `.gitignore` now blocks `.lfsconfig`, `*.pat`, `*.token`, `*.pem`, `*.key`,
   `**/credentials*`, etc., so a token file cannot be committed by accident.

---

## #20 — Demo Mode reconciliation

`DemoDirector` is unchanged and still works alongside the new door progression:
- In **Demo Mode**, `GameManager.OnExhibitSolved` auto-advances (demo-only path),
  so the director's scripted solves carry the run Splash→Complete without doors.
- In **manual play**, solves do **not** auto-advance; the physical `RoomDoor`
  activates on the win event and the player opens it.
- `DemoDirector.DoReveal` calls `FinalRevealController.BeginReveal()`, which now
  resolves (it was null before #1). After the morph it fires
  `ExhibitSolvedEvent(RevealChamber)` → demo auto-advances to Complete.
Verify a full Demo Mode pass once on device.

---

## Art items still needing a pass (precise findings)

- **#11 Mirror-flipped text** — There are **zero** Unity-side negative-scale TMP
  objects, so the museum signage is clean on the Unity side. The remaining mirror is
  in the **Blender-generated 3D text label meshes** (`Tooling/Blender/`), an axis-
  conversion/flip in the generator. This is the **collaborator's Blender area**; fix
  it in the generator and regenerate so it is clean (a Unity transform patch would
  not survive regeneration).
- **#13 Vault FX** — `DECRYPTED/EnergyScanline_URP` exists and fits the budget
  (unlit, transparent, procedural). It was **not** auto-applied to the vault because
  the target surface is an art decision (archive wall vs. status panel vs. door
  inlay); applying it blindly risks the wrong surface reading as a grid. Assign it in
  the editor to the intended digital-security surface.
- **#14 Vault readability** — The bright "blob" at top-center is the **emissive
  `Glass` skylight** (y≈7.4) plus a cluster of high fill lights (`HeroSpotA/B`,
  `WallWash`, `FillFront/Back`, the room `Spot Light`). It is dressing, not a portal
  bleed. Recommended: lower the `Glass` emission and dim the high fills; this folds
  into the lighting bake (#9). Tune with eyes in-headset.
- **#15 WorldLabels** — Confirmed **4** labels (Caesar Cipher Disk, Enigma Machine,
  The Vault, The Reveal); no labels on the 26 Enigma keys / 28 vault keys (intended).
  Placement (top-of-bounds + 0.2 m) and billboarding are computed at runtime; confirm
  visually on device.

---

## #21 — Consolidated on-device verification checklist

Run a full **Splash → Complete** pass in the headset. Treat every box as unverified
until checked here.

**Stability / frame budget**
- [ ] No crash across a full playthrough worn normally (see #3 repro if it crashes).
- [ ] OVR Metrics: 72 FPS held in every room, every door transition, and the morph.
- [ ] After the editor bake (#9): realtime light count down, look intact.

**Doors (never run on device)**
- [ ] Each door is inert until its win condition, then glows (emissive pulse) +
      plays the tone + shows "NEXT".
- [ ] "NEXT" label billboards to face you; door geometry clears walls/props in every
      room (adjust the `ExitDoor` transform if not).
- [ ] Handle highlights warm-white within ~0.35 m; grip opens it; slab swings 0.6 s.
- [ ] Door-open → fade-to-black → next room → fade-in completes **even while
      standing still** (time-scaling cannot stall it).
- [ ] Reveal door opens into `Room_Complete`; closing plaque reads correctly; holds.

**Hands**
- [ ] Palms face inward at rest; if not, "Flip Palm Roll" on that hand's VRHandPoser.
- [ ] Grip curls fingers toward the palm; if backward, "Flip Curl Direction".
- [ ] Trigger extends the index for pointing.

**Splash / flow**
- [ ] PLAY button glows/pulses and starts the tour (Splash → Atrium).
- [ ] Caesar, Enigma, Vault solve via the collision/grip hands; each win activates
      its door.
- [ ] Reveal sculpture morphs Roman → Gears → Circuit (Hologram dissolve), no
      stacked overlap at rest.

**Audio / text**
- [ ] No spoken narration anywhere. Captions/plaques carry the teaching text.
- [ ] Ancient Room plays `amb_ancient`.

**Demo Mode**
- [ ] A Demo Mode run plays itself Splash → Complete with no doors required.
