# 08 · Artifact Animation Pipeline (Procedural ↔ Blender)

The three interactive exhibits no longer move their own transforms. Each routes
all motion through a small swappable `IArtifactAnimator` (see
`Assets/_Project/Scripts/Interaction/ArtifactAnimators.cs`):

| Artifact | Script | Interface | Procedural (default) | Blender |
|---|---|---|---|---|
| Caesar disk + Enigma rotors | `XRGrabTwistDisk` | `IDiskAnimator` | rotates the transform | `Animator.SetFloat("DiskAngle", deg)` |
| Enigma exit door | `EnigmaController` | `IEnigmaAnimator` | slides the transform | `Animator.SetTrigger("Open")` |
| Vault door + locking ring | `VaultController` | `IVaultAnimator` | tweens the transform | `Animator.SetTrigger("Open")` |

Each owning MonoBehaviour has a **`_useBlenderAnimations`** bool (default **false** =
current behaviour, nothing changes) and an Animator field. Flip the bool to **true**
and assign the Animator to use Blender-baked motion. **No C# changes are needed to
switch.**

---

## 1. Export the animation from Blender as FBX (baked keyframes)

In Blender, with the artifact + its animation in the scene:

1. Select the artifact's objects. **Do NOT** double-click an `.fbx` in Explorer to
   "open" it — Blender only opens native `.blend` files. To bring an existing FBX
   back in, use **File → Import → FBX**.
2. **File → Export → FBX (.fbx)** and set, in the export panel:
   - **Bake Animation: ON**, **Force Start/End Keying: ON**, **NLA Strips / All
     Actions** as needed.
   - **!Apply Transform: ON** (apply object transforms so Unity import matches).
   - **Sampling Rate: 30** (30 fps — match `Project Settings ▸ Time` references).
   - **Object Types:** Mesh + Empty + Armature only (exclude Light/Camera).
   - **Apply Scalings: FBX All**, **Forward: -Z**, **Up: Y**.
3. Name the moving objects exactly as the Animator expects (below). Object/bone
   names must be stable across re-exports or the AnimatorController loses its bind.

> The procedural generators in `Tooling/Blender/` build the geometry; this animation
> pass is authored on top of that geometry in Blender and exported here.

## 2. AnimatorController parameters (must match these names)

Create an `AnimatorController` per artifact and add:

- **Caesar disk / Enigma rotor** — a **Float** parameter named **`DiskAngle`**.
  Wire it to a 1-frame "rotate" state via a blend or a script-driven time, so
  scrubbing `DiskAngle` (degrees, 0..360) rotates the dial. The interaction code
  writes this float every frame the dial turns.
- **Enigma exit door** — a **Trigger** named **`Open`**. Default state = closed;
  a transition on `Open` plays the baked door-open clip.
- **Vault door + ring** — a **Trigger** named **`Open`**. One clip baking both the
  door swing and the locking-ring spin; default state = closed.

## 3. Import into Unity and assign

1. Drop the exported `.fbx` (or `.glb`) into `Assets/_Project/Art/Generated/`. Unity
   2022.3 imports both natively.
2. On the imported model: **Rig → Animation Type = Generic**, **Animation → Import
   Animation = ON**; confirm the clip(s) appear.
3. Put the model in the scene under the artifact, add an **Animator** component, and
   assign the AnimatorController.
4. On the artifact's root script (`XRGrabTwistDisk` / `EnigmaController` /
   `VaultController`): assign the **Animator** to the Blender-animator field and tick
   **`_useBlenderAnimations`**.

## 4. Flip the toggle

- **`_useBlenderAnimations = false`** → procedural C# motion (current, default).
- **`_useBlenderAnimations = true`** (+ Animator assigned) → Blender FBX/GLB motion.

If `true` but no Animator is assigned, it safely falls back to procedural. The
puzzle logic, win conditions, EventBus events and game state are identical either
way — only *where the parts move* changes.
