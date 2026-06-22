#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_common.py
#  DECRYPTED — A Walk Through the History of Secret Writing  (Blender pipeline)
#
#  Shared helpers for the procedural asset generators. Every museum prop is built
#  in code so the whole art set is reproducible, version-controllable and free of
#  any third-party/marketplace assets (a hard requirement of the brief). These
#  helpers wrap the verbose parts of the Blender Python API into a few readable
#  builders: meshes from raw geometry, boxes/cylinders/rings, extruded text,
#  Principled-BSDF materials (3.x and 4.x compatible), anchor empties for runtime
#  wiring, and Quest-friendly FBX/glTF export.
#
#  Run inside Blender:  blender --background --python gen_<thing>.py
#  (Each generator imports this module; see export_all.py to batch everything.)
# -----------------------------------------------------------------------------

import math
import os

try:
    import bpy
    import bmesh
    from mathutils import Vector
except ImportError:  # allows linting / import outside Blender
    bpy = None
    bmesh = None


# --------------------------------------------------------------- scene / colls

def reset_scene():
    """Wipe the default scene so a generator starts from a clean slate."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.curves):
        for b in list(block):
            if b.users == 0:
                block.remove(b)


def ensure_collection(name):
    """Get or create a top-level collection by name."""
    if name in bpy.data.collections:
        return bpy.data.collections[name]
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col


def link(obj, col):
    col.objects.link(obj)
    return obj


# ------------------------------------------------------------------ materials

def make_material(name, color=(0.8, 0.8, 0.8, 1.0), metallic=0.0, roughness=0.6,
                  emission=(0, 0, 0, 1), emission_strength=0.0):
    """Create a Principled-BSDF material. Handles the Blender 3.x → 4.x input
    rename (single 'Emission' colour → 'Emission Color' + 'Emission Strength')."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is None:
        return mat

    def set_in(key, value):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = value
            return True
        return False

    set_in("Base Color", color)
    set_in("Metallic", metallic)
    set_in("Roughness", roughness)

    # Emission: try 4.x names first, then 3.x.
    if not set_in("Emission Color", emission):
        set_in("Emission", emission)
    set_in("Emission Strength", emission_strength)
    return mat


def assign(obj, mat):
    if mat is None:
        return obj
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)
    return obj


# --------------------------------------------------------------------- meshes

def add_mesh(name, verts, faces, col, mat=None, uv_scale=2.0, generate_uvs=True):
    """Build an object from raw vertex/face lists.

    By default this now also generates a UV map via *box projection* scaled to
    real-world size (uv_scale = world-metres per texture tile). Box projection
    picks, per face, the dominant axis of the face normal and projects the other
    two world axes into UV space — exactly right for the axis-aligned boxes that
    make up the museum (walls/floor/ceiling), so a texture tiles at a consistent
    physical scale on every surface regardless of that surface's size.

    Without UVs a texture has nowhere to map and renders as a flat average colour
    (the 'sandstone but no bricks' bug). uv_scale 2.0 => the texture repeats every
    2 m; with the Ancient wall texture (4 courses/tile) that yields realistic
    ~0.5 m ashlar blocks. Pass generate_uvs=False for meshes that supply their own
    UVs."""
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([Vector(v) for v in verts], [], faces)
    mesh.update()

    if generate_uvs and bmesh is not None:
        _box_project_uvs(mesh, uv_scale)

    obj = bpy.data.objects.new(name, mesh)
    link(obj, col)
    assign(obj, mat)
    return obj


def _box_project_uvs(mesh, scale=0.5):
    """Create/overwrite a UV layer using per-face box (planar-by-dominant-axis)
    projection in object space. `scale` is world metres per UV tile."""
    inv = 1.0 / max(1e-6, scale)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    uv_layer = bm.loops.layers.uv.verify()
    for face in bm.faces:
        n = face.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        # choose the two axes to keep based on the dominant normal axis
        for loop in face.loops:
            co = loop.vert.co
            if az >= ax and az >= ay:        # facing up/down  -> use X,Y
                u, v = co.x, co.y
            elif ax >= ay:                   # facing +/-X     -> use Y,Z
                u, v = co.y, co.z
            else:                            # facing +/-Y     -> use X,Z
                u, v = co.x, co.z
            loop[uv_layer].uv = (u * inv, v * inv)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def box(name, size=(1, 1, 1), location=(0, 0, 0), col=None, mat=None, bevel=0.0):
    """Axis-aligned box centred on `location`. Optional bevel modifier (applied)."""
    sx, sy, sz = (s * 0.5 for s in size)
    v = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
         (-sx, -sy,  sz), (sx, -sy,  sz), (sx, sy,  sz), (-sx, sy,  sz)]
    f = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4),
         (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    obj = add_mesh(name, v, f, col, mat)
    obj.location = location
    if bevel > 0:
        m = obj.modifiers.new("Bevel", 'BEVEL')
        m.width = bevel
        m.segments = 2
        apply_modifiers(obj)
    return obj


def cylinder(name, radius=0.5, depth=1.0, location=(0, 0, 0), col=None, mat=None,
             segments=48, axis='Z'):
    """A capped cylinder built procedurally (no ops), oriented along an axis."""
    verts, faces = [], []
    half = depth * 0.5
    for i in range(segments):
        a = TWO_PI * i / segments
        x, y = radius * math.cos(a), radius * math.sin(a)
        if axis == 'Z':
            verts.append((x, y, -half)); verts.append((x, y, half))
        elif axis == 'Y':
            verts.append((x, -half, y)); verts.append((x, half, y))
        else:  # 'X'
            verts.append((-half, x, y)); verts.append((half, x, y))
    for i in range(segments):
        a0, b0 = 2 * i, 2 * i + 1
        a1, b1 = 2 * ((i + 1) % segments), 2 * ((i + 1) % segments) + 1
        faces.append((a0, a1, b1, b0))            # side quad
    # Caps as fans.
    bottom = list(range(0, 2 * segments, 2))
    top = list(range(1, 2 * segments, 2))
    faces.append(tuple(reversed(bottom)))
    faces.append(tuple(top))
    obj = add_mesh(name, verts, faces, col, mat)
    obj.location = location
    return obj


def ring(name, outer=1.0, inner=0.6, depth=0.2, location=(0, 0, 0), col=None,
         mat=None, segments=64):
    """A flat annulus (washer) with thickness — used for dial rings, locking rings."""
    verts, faces = [], []
    half = depth * 0.5
    for i in range(segments):
        a = TWO_PI * i / segments
        co, so = math.cos(a), math.sin(a)
        verts += [(outer * co, outer * so, -half), (outer * co, outer * so, half),
                  (inner * co, inner * so, -half), (inner * co, inner * so, half)]
    for i in range(segments):
        j = (i + 1) % segments
        o0b, o0t, i0b, i0t = 4 * i, 4 * i + 1, 4 * i + 2, 4 * i + 3
        o1b, o1t, i1b, i1t = 4 * j, 4 * j + 1, 4 * j + 2, 4 * j + 3
        faces.append((o0b, o1b, o1t, o0t))  # outer wall
        faces.append((i0t, i1t, i1b, i0b))  # inner wall
        faces.append((o0t, o1t, i1t, i0t))  # top
        faces.append((i0b, i1b, o1b, o0b))  # bottom
    obj = add_mesh(name, verts, faces, col, mat)
    obj.location = location
    return obj


def text(name, body, size=0.2, location=(0, 0, 0), rotation=(0, 0, 0), col=None,
         mat=None, extrude=0.01, align_x='CENTER', align_y='CENTER'):
    """Extruded 3D text (kept as a font object; convert in export if needed)."""
    curve = bpy.data.curves.new(name, type='FONT')
    curve.body = body
    curve.size = size
    curve.extrude = extrude
    curve.align_x = align_x
    curve.align_y = align_y
    obj = bpy.data.objects.new(name, curve)
    obj.location = location
    obj.rotation_euler = rotation
    link(obj, col)
    assign(obj, mat)
    return obj


def add_empty(name, location=(0, 0, 0), col=None, size=0.05):
    """An empty used as a runtime anchor (letter slots, signal-trace waypoints…)."""
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'PLAIN_AXES'
    e.empty_display_size = size
    e.location = location
    link(e, col)
    return e


def parent_all_to_root(col, root_name):
    """Give a collection a single named ROOT empty at the origin and parent every
    current top-level object under it (children of children are left alone — they
    already have their parent).

    Why: each exhibit is exported as its own FBX, but its parts were a flat set of
    sibling objects with no single handle. After this call the whole exhibit hangs
    off one root, so in Unity you can grab/position/rotate the entire prop with one
    transform, and the prefab has a clean single top node. Uses parent_keep_world so
    nothing shifts and the relationship survives FBX export. Call this LAST in a
    generator's build(), just before `return col`."""
    root = add_empty(root_name, location=(0, 0, 0), col=col, size=0.2)
    # Snapshot the current top-level objects (those with no parent yet), excluding
    # the root we just made, so we don't reparent mid-iteration.
    tops = [o for o in list(col.objects) if o.parent is None and o is not root]
    for o in tops:
        parent_keep_world(o, root)
    return root


# ------------------------------------------------------------------- helpers

TWO_PI = 2.0 * math.pi


def select_only(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifiers(obj):
    select_only(obj)
    for m in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except RuntimeError:
            pass


def apply_transforms(obj):
    """Bake the object's current world-space location/rotation/scale into the
    mesh vertices so the object transform resets to identity.  After this call
    the object sits at its original world position but with a clean transform,
    which means parent-child relationships in the FBX will be correct."""
    select_only(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def parent_keep_world(child, parent):
    """Parent `child` to `parent` while keeping `child` exactly where it is —
    in a way that SURVIVES FBX EXPORT.

    Why this exists (the 'exploded artifacts' bug):
    The old pattern was `apply_transforms(child); child.parent = parent`. That
    bakes the child's WORLD position into its mesh vertices and zeroes the object
    transform, THEN parents it. Blender keeps the child visually in place by
    storing a hidden 'parent inverse' matrix — but the FBX exporter does NOT write
    that matrix out. On import, Unity computes the child's position as
    parent_position + child_local_position, and since the child's vertices are
    already baked to world AND the object's local position is zero, the result is
    that the parent's offset gets added on top => the child is flung to roughly
    double its intended offset. Every baked-then-parented part 'explodes'; the only
    exhibit that looked right (the cipher disk) was the one that never baked.

    The fix: parent first, clear the parent-inverse (the thing FBX drops), then
    restore the child's true world transform by writing it into matrix_world. Now
    the child's LOCAL transform genuinely encodes 'where it sits relative to the
    parent', which is exactly what FBX exports and what Unity reads back. Blender's
    viewport and the exported FBX now agree.

    Use this INSTEAD OF `apply_transforms(child); child.parent = parent`."""
    if parent is None:
        child.parent = None
        return child
    world = child.matrix_world.copy()              # remember current world pose
    child.parent = parent
    child.matrix_parent_inverse.identity()         # drop the inverse FBX won't export
    child.matrix_world = world                     # re-encode pose as a real local xform
    return child


def join(objects, name):
    """Join a list of mesh objects into the first one and rename it."""
    meshes = [o for o in objects if o and o.type == 'MESH']
    if not meshes:
        return None
    select_only(meshes[0])
    for o in meshes[1:]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    meshes[0].name = name
    return meshes[0]


def letters():
    return [chr(ord('A') + i) for i in range(26)]


# -------------------------------------------------------------------- export

def _convert_nonmesh_to_mesh(col):
    """Convert FONT / CURVE / SURFACE / META objects in the collection to real
    meshes. Non-mesh objects (especially text/curves) are a known cause of FBX
    export crashes, and exporting them is unreliable across importers; converting
    keeps the text labels as solid mesh geometry while making the export mesh-only."""
    if bpy is None:
        return
    bpy.ops.object.select_all(action='DESELECT')
    targets = [o for o in col.all_objects if o.type in {'FONT', 'CURVE', 'SURFACE', 'META'}]
    if not targets:
        return
    for o in targets:
        o.select_set(True)
    bpy.context.view_layer.objects.active = targets[0]
    try:
        bpy.ops.object.convert(target='MESH')
    except RuntimeError as e:
        print(f"[gen_common] convert-to-mesh skipped: {e}")


def _validate_meshes(col):
    """Repair/report broken geometry and bad UVs before export so the exporter does
    not fail silently on an invalid mesh."""
    if bpy is None:
        return
    for obj in col.all_objects:
        if obj.type == 'MESH' and obj.data is not None:
            obj.data.validate(verbose=True)


def export_collection(col, out_path, fmt='FBX'):
    """Export a single collection to FBX or glTF (GLB) with Quest-friendly settings.

    Returns True on success. The export is wrapped in try/except so a failure prints
    a full traceback instead of leaving a silent / broken file on disk. Non-mesh
    objects are converted to mesh and all meshes are validated first.

    TRANSFORMS - intentional: this pipeline does NOT run a global transform_apply and
    keeps bake_space_transform=False. Part parenting is handled by parent_keep_world(),
    which already bakes correct LOCAL transforms that survive FBX export. Re-adding
    transform_apply on every object + bake_space_transform=True is exactly what caused
    the historical 'exploded artifacts' bug (CLAUDE.md, Session 8), so they are left
    off here on purpose.
    """
    import traceback
    out_path = os.path.abspath(out_path)            # absolute (relative paths silently fail on some systems)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)

    _convert_nonmesh_to_mesh(col)                   # text/curves -> mesh (no non-mesh in the export)
    _validate_meshes(col)                           # repair/report bad geometry first

    bpy.ops.object.select_all(action='DESELECT')
    for o in col.all_objects:
        o.select_set(True)

    try:
        if fmt.upper() == 'FBX':
            bpy.ops.export_scene.fbx(
                filepath=out_path, use_selection=True, apply_unit_scale=True,
                apply_scale_options='FBX_SCALE_ALL', bake_space_transform=False,
                mesh_smooth_type='FACE', use_mesh_modifiers=True, use_tspace=True,
                add_leaf_bones=False, path_mode='COPY', embed_textures=False,
                # Mesh-only export. LIGHT / CAMERA / OTHER are excluded (a known FBX
                # crash cause); reset_scene() already removed the default light/camera.
                object_types={'MESH', 'EMPTY', 'ARMATURE'},
                axis_forward='-Z', axis_up='Y')             # Unity axis convention
        else:
            bpy.ops.export_scene.gltf(
                filepath=out_path, use_selection=True, export_format='GLB',
                export_apply=True, export_yup=True)
        print(f"SUCCESS: exported {col.name} -> {out_path}")
        return True
    except Exception as e:
        print(f"FAILED: {out_path}  ({e})")
        traceback.print_exc()
        return False