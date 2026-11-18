# The forest kit (ADR 0027): low-poly Appalachian trees and understory in the Valheim manner, built by script
# into region_kit.blend and exported one FBX per model to Assets/Hearthline/Art/Greybox.
#
# Run from the repository root:
#   blender -b ArtSource/Greybox/region_kit.blend --python ArtSource/Greybox/forest_kit.py
#
# Every tree has a far version (<name>_Far): a five-sided trunk and one or two plain crowns, drawn past the
# near distance so tens of thousands of trees stay cheap. Shapes are chunky and faceted (flat shaded);
# colour comes from the palette materials and their detail textures in Unity.
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler, noise

exec(bpy.data.texts["hl_kit"].as_string())  # PALETTE, mat, coll, new_obj, add_part, cube, cyl, ico, T, lumpy, finish

REPO = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath), "..", ".."))
OUT = os.path.join(REPO, "Assets", "Hearthline", "Art", "Greybox")
made = []


def box_uv(bm, scale=1.0):
    """Per-face box projection at one UV unit per metre, like the rest of the kit (ADR 0018)."""
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            c = loop.vert.co
            u, v = (c.y, c.z) if ax == 0 else (c.x, c.z) if ax == 1 else (c.x, c.y)
            loop[uv].uv = (u * scale, v * scale)


def remove_existing(name):
    ob = bpy.data.objects.get(name)
    if ob:
        me = ob.data
        bpy.data.objects.remove(ob)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)


def trunk(bm, rng, height, r0, r1, segments=7, bend=0.25, mi=0, start=Vector((0, 0, -0.3))):
    """A tapered trunk in a few stacked pieces, each leaning a little, so it isn't a perfect pole. Returns the top."""
    pieces = 3
    base = start.copy()
    lean = Vector((0, 0, 0))
    for i in range(pieces):
        h = height / pieces + (0.3 if i == 0 else 0.15)
        ra = r0 + (r1 - r0) * (i / pieces)
        rb = r0 + (r1 - r0) * ((i + 1) / pieces)
        lean += Vector((rng.uniform(-bend, bend), rng.uniform(-bend, bend), 0))
        top = base + Vector((lean.x, lean.y, h))
        d = top - base
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_euler()
        add_part(bm, cyl, mi, T(loc=tuple((base + top) / 2), rot=tuple(rot)), segments=segments, r1=ra, r2=rb, depth=d.length)
        base = base + d * (height / pieces / d.length) if i < pieces - 1 else top
    # Root flare: a squat wider cone at the foot.
    add_part(bm, cyl, mi, T(loc=(0, 0, 0.15)), segments=segments, r1=r0 * 1.6, r2=r0, depth=0.8)
    return base


def branch(bm, frm, to, r0, r1, mi=0, segments=5):
    d = to - frm
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_euler()
    add_part(bm, cyl, mi, T(loc=tuple((frm + to) / 2), rot=tuple(rot)), segments=segments, r1=r0, r2=r1, depth=d.length)


def clump(bm, centre, radius, squash, seed, mi, subdiv=2, lump=0.34):
    tmp = bmesh.new()
    bmesh.ops.create_icosphere(tmp, subdivisions=subdiv, radius=1.0)
    lumpy(tmp, tmp.verts, seed, amount=lump, freq=1.4)
    bmesh.ops.transform(tmp, matrix=T(loc=tuple(centre), rot=(0, 0, seed * 0.7), scale=(radius, radius * random.Random(seed).uniform(0.85, 1.15), radius * squash)), verts=tmp.verts)
    for f in tmp.faces:
        f.material_index = mi
    me = bpy.data.meshes.new("_tmp"); tmp.to_mesh(me); tmp.free()
    bm.from_mesh(me); bpy.data.meshes.remove(me)


def build(name, materials, fn, collection="Trees", at=(0, 0, 0)):
    remove_existing(name)
    bm = bmesh.new()
    fn(bm)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bm.normal_update()
    box_uv(bm)
    ob = new_obj(name, bm, materials, collection)
    finish(ob, shade_flat=True)
    ob.location = at
    made.append(ob)
    return ob


def crown_points(rng, centre, spread, count, height_jitter):
    pts = []
    for i in range(count):
        a = i / count * math.tau + rng.uniform(-0.4, 0.4)
        r = spread * rng.uniform(0.35, 1.0)
        pts.append(centre + Vector((math.cos(a) * r, math.sin(a) * r, rng.uniform(-height_jitter, height_jitter))))
    return pts


# ---- Species ------------------------------------------------------------------------------------------

def white_oak(seed):
    """White oak: a thick trunk, bare to about 8 m in the forest, a broad heavy crown of big clumps."""
    def fn(bm):
        rng = random.Random(seed)
        h = rng.uniform(8.0, 9.5)
        top = trunk(bm, rng, h, 0.55, 0.36, bend=0.2)
        ctop = top + Vector((0, 0, 4.2))
        for p in crown_points(rng, ctop, 4.2, 6, 1.0):
            branch(bm, top - Vector((0, 0, 0.8)), p * 0.55 + top * 0.45, 0.24, 0.12)
            clump(bm, p, rng.uniform(2.6, 3.4), 0.72, rng.random() * 100, rng.choice([1, 2]))
        clump(bm, ctop + Vector((0, 0, 1.6)), 3.4, 0.7, seed + 7, 1)
    return fn


def poplar(seed):
    """Tulip poplar: the tallest in the cove, a ruler-straight pale trunk and a narrow crown high up."""
    def fn(bm):
        rng = random.Random(seed)
        h = rng.uniform(14.0, 16.0)
        top = trunk(bm, rng, h, 0.48, 0.24, bend=0.06)
        for i in range(8):
            a = i * 2.4
            p = top + Vector((math.cos(a) * rng.uniform(0.6, 1.5), math.sin(a) * rng.uniform(0.6, 1.5), -1.6 + i * 1.05))
            clump(bm, p, rng.uniform(2.1, 2.8) * (1.0 - i * 0.06), 0.8, rng.random() * 100, 1 if i % 2 else 2)
        for i in range(3):
            a = rng.uniform(0, math.tau)
            branch(bm, top - Vector((0, 0, 0.6 + i)), top + Vector((math.cos(a) * 1.6, math.sin(a) * 1.6, 0.4 - i)), 0.12, 0.06)
    return fn


def hickory(seed):
    """Hickory: a crooked trunk and an irregular, open crown."""
    def fn(bm):
        rng = random.Random(seed)
        h = rng.uniform(7.0, 8.5)
        top = trunk(bm, rng, h, 0.42, 0.26, bend=0.45)
        ctop = top + Vector((0, 0, 3.0))
        for p in crown_points(rng, ctop, 3.0, 5, 1.4):
            branch(bm, top - Vector((0, 0, 0.5)), p * 0.6 + top * 0.4, 0.18, 0.09)
            clump(bm, p, rng.uniform(1.8, 2.6), 0.8, rng.random() * 100, rng.choice([1, 2]), lump=0.38)
    return fn


def pine(seed):
    """Shortleaf pine: a straight trunk bare most of the way, a few stubs, dark flat tufts at the top."""
    def fn(bm):
        rng = random.Random(seed)
        h = rng.uniform(12.5, 14.5)
        top = trunk(bm, rng, h, 0.4, 0.18, bend=0.12)
        for i in range(4):
            a = rng.uniform(0, math.tau); z = rng.uniform(4, h - 3)
            branch(bm, Vector((0, 0, z)), Vector((math.cos(a) * 0.9, math.sin(a) * 0.9, z + 0.3)), 0.07, 0.03)
        for i in range(5):
            a = i / 5 * math.tau + rng.uniform(-0.3, 0.3)
            p = top + Vector((math.cos(a) * rng.uniform(0.6, 1.6), math.sin(a) * rng.uniform(0.6, 1.6), -2.2 + i * 0.95))
            branch(bm, top - Vector((0, 0, 2.4 - i * 0.9)), p, 0.09, 0.05)
            clump(bm, p, rng.uniform(1.3, 1.9), 0.55, rng.random() * 100, 1)
        clump(bm, top + Vector((0, 0, 1.6)), 1.4, 0.75, seed + 3, 1)
    return fn


def hemlock(seed):
    """Eastern hemlock: by the creeks, dark and conical, tiers drooping almost to the ground."""
    def fn(bm):
        rng = random.Random(seed)
        h = rng.uniform(13.0, 15.0)
        trunk(bm, rng, h, 0.4, 0.12, bend=0.08)
        tiers = 7
        for i in range(tiers):
            z = 1.6 + i * (h - 1.6) / tiers
            r = 3.4 * (1 - i / tiers) + 0.6
            tmp_mi = 1 if i % 2 else 2
            add_part(bm, cyl, tmp_mi, T(loc=(rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), z + 1.0), rot=(0, 0, rng.uniform(0, 1))), segments=7, r1=r, r2=r * 0.25, depth=2.6)
        add_part(bm, cyl, 1, T(loc=(0, 0, h + 1.3)), segments=6, r1=0.7, r2=0.0, depth=2.4)
    return fn


def dogwood(seed):
    """Flowering dogwood: a small understory tree, low and spreading, with flat layered crowns."""
    def fn(bm):
        rng = random.Random(seed)
        top = trunk(bm, rng, 2.2, 0.16, 0.11, segments=6, bend=0.3)
        for i in range(4):
            a = i / 4 * math.tau + rng.uniform(-0.4, 0.4)
            p = top + Vector((math.cos(a) * 1.4, math.sin(a) * 1.4, rng.uniform(0.6, 1.8)))
            branch(bm, top, p, 0.08, 0.04)
            clump(bm, p, rng.uniform(1.2, 1.6), 0.42, rng.random() * 100, 1)
        clump(bm, top + Vector((0, 0, 2.4)), 1.3, 0.45, seed + 11, 1)
    return fn


def far(height_fn):
    """The far version: a five-sided trunk and one or two plain crowns sized to the near tree."""
    def make(trunk_h, trunk_r, crowns):
        def fn(bm):
            add_part(bm, cyl, 0, T(loc=(0, 0, trunk_h / 2 - 0.3)), segments=5, r1=trunk_r, r2=trunk_r * 0.6, depth=trunk_h + 0.6)
            for (c, r, squash, mi) in crowns:
                tmp = bmesh.new(); bmesh.ops.create_icosphere(tmp, subdivisions=1, radius=1.0)
                lumpy(tmp, tmp.verts, c[2], amount=0.15, freq=1.1)
                bmesh.ops.transform(tmp, matrix=T(loc=c, scale=(r, r, r * squash)), verts=tmp.verts)
                for f in tmp.faces: f.material_index = mi
                me = bpy.data.meshes.new("_tmp"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
        return fn
    return make


def cone_far(h, r):
    def fn(bm):
        add_part(bm, cyl, 0, T(loc=(0, 0, 1.0)), segments=5, r1=0.35, r2=0.25, depth=2.6)
        add_part(bm, cyl, 1, T(loc=(0, 0, h / 2 + 1.2)), segments=6, r1=r, r2=0.0, depth=h)
    return fn


# ---- Understory ----------------------------------------------------------------------------------------

def thicket(seed, w, h, count, dark):
    """Rhododendron or mountain laurel: a dense evergreen mound of clumps on twisted stems ("laurel hell")."""
    def fn(bm):
        rng = random.Random(seed)
        for i in range(4):
            a = rng.uniform(0, math.tau)
            branch(bm, Vector((0, 0, -0.2)), Vector((math.cos(a) * w * 0.3, math.sin(a) * w * 0.3, h * 0.6)), 0.06, 0.03)
        for i in range(count):
            a = rng.uniform(0, math.tau); r = rng.uniform(0, w * 0.45)
            p = Vector((math.cos(a) * r, math.sin(a) * r, rng.uniform(h * 0.35, h * 0.7)))
            clump(bm, p, rng.uniform(w * 0.22, w * 0.32), 0.7, rng.random() * 100, 1 if (i % 3 or not dark) else 2, lump=0.32)
    return fn


def sapling(seed):
    def fn(bm):
        rng = random.Random(seed)
        top = trunk(bm, rng, 2.6, 0.07, 0.04, segments=5, bend=0.15)
        clump(bm, top + Vector((0, 0, 0.4)), 0.8, 0.9, seed, 1)
        clump(bm, top + Vector((0.4, 0.2, -0.4)), 0.6, 0.8, seed + 1, 1)
    return fn


def stump(seed):
    def fn(bm):
        rng = random.Random(seed)
        add_part(bm, cyl, 0, T(loc=(0, 0, 0.2)), segments=7, r1=0.55, r2=0.45, depth=1.0)
        add_part(bm, cyl, 0, T(loc=(0, 0, -0.1)), segments=7, r1=0.8, r2=0.55, depth=0.4)
        add_part(bm, cyl, 1, T(loc=(0, 0, 0.72)), segments=7, r1=0.46, r2=0.46, depth=0.04)
    return fn


# ---- Build and export -------------------------------------------------------------------------------------

def tree(name, mats, near_fn, far_fn, x):
    build(name, mats, near_fn, at=(x, -200, 0))
    build(name + "_Far", mats, far_fn, at=(x, -215, 0))

f = far(None)
tree("Tree_WhiteOak_A", ["HL_Bark", "HL_Leaves", "HL_LeavesDark"], white_oak(11), f(9, 0.5, [((0, 0, 13.5), 5.6, 0.62, 1)]), 0)
tree("Tree_WhiteOak_B", ["HL_Bark", "HL_Leaves", "HL_LeavesDark"], white_oak(23), f(9, 0.5, [((0.5, 0, 13.2), 5.2, 0.6, 1), ((-1.5, 1, 14.5), 3.5, 0.7, 2)]), 14)
tree("Tree_Poplar_A", ["HL_BarkLight", "HL_LeavesLight", "HL_Leaves"], poplar(5), f(15, 0.4, [((0, 0, 18.5), 2.6, 1.9, 1)]), 28)
tree("Tree_Poplar_B", ["HL_BarkLight", "HL_LeavesLight", "HL_Leaves"], poplar(9), f(15, 0.4, [((0, 0, 19), 2.5, 2.0, 1)]), 38)
tree("Tree_Hickory_A", ["HL_Bark", "HL_Leaves", "HL_LeavesLight"], hickory(31), f(8, 0.36, [((0, 0, 11), 3.8, 0.75, 1)]), 48)
tree("Tree_Hickory_B", ["HL_Bark", "HL_Leaves", "HL_LeavesLight"], hickory(37), f(8, 0.36, [((0.4, -0.3, 10.8), 3.6, 0.8, 1)]), 58)
tree("Tree_ShortleafPine_A", ["HL_BarkLight", "HL_Pine"], pine(41), f(13, 0.32, [((0, 0, 14.5), 2.2, 1.0, 1)]), 68)
tree("Tree_ShortleafPine_B", ["HL_BarkLight", "HL_Pine"], pine(47), f(14, 0.32, [((0, 0, 15.2), 2.0, 1.1, 1)]), 76)
tree("Tree_Hemlock_A", ["HL_Bark", "HL_Pine", "HL_LeavesDark"], hemlock(53), cone_far(14, 3.6), 86)
tree("Tree_Dogwood_A", ["HL_Bark", "HL_LeavesLight"], dogwood(61), f(2.6, 0.13, [((0, 0, 4.2), 2.3, 0.5, 1)]), 96)

build("Rhododendron_A", ["HL_Bark", "HL_LeavesDark", "HL_Leaves"], thicket(71, 3.6, 2.6, 9, True), collection="Cover", at=(0, -230, 0))
build("Rhododendron_B", ["HL_Bark", "HL_LeavesDark", "HL_Leaves"], thicket(73, 4.2, 2.9, 11, True), collection="Cover", at=(6, -230, 0))
build("Laurel_A", ["HL_Bark", "HL_Leaves", "HL_LeavesLight"], thicket(79, 2.4, 1.8, 7, False), collection="Cover", at=(12, -230, 0))
build("Sapling_A", ["HL_Bark", "HL_LeavesLight"], sapling(83), collection="Cover", at=(17, -230, 0))
build("Stump_A", ["HL_Bark", "HL_WoodWeathered"], stump(89), collection="Cover", at=(21, -230, 0))

# Store this script in the blend, beside hl_kit, so the file says how its forest was made.
txt = bpy.data.texts.get("hl_forest") or bpy.data.texts.new("hl_forest")
txt.from_string(open(__file__ if "__file__" in globals() else os.path.join(REPO, "ArtSource", "Greybox", "forest_kit.py"), encoding="utf-8").read())

for ob in made:
    bpy.ops.object.select_all(action="DESELECT")
    was = ob.location.copy()
    ob.location = (0, 0, 0)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    ob.location = was

bpy.ops.wm.save_mainfile()
print("FOREST " + ", ".join("%s:%d" % (o.name, len(o.data.vertices)) for o in made))
