# The forest kit (ADR 0027): low-poly Appalachian trees and understory in the Valheim manner, built by script
# into region_kit.blend and exported one FBX per model to Assets/Hearthline/Art/Greybox.
#
# Run from the repository root:
#   blender -b ArtSource/Greybox/region_kit.blend --python ArtSource/Greybox/forest_kit.py
#
# Crowns are LEAF CARDS (ADR 0020), not solid shapes: hundreds of quads, each showing a cut-out spray of leaves
# or needles, clustered on the ends of real limbs plus a shell that fills out the silhouette. Card normals bend
# outward from the crown so light falls on it as on one soft mass, and crown shading is baked into vertex
# colour (red). Light shows through the gaps and the edges are ragged, as in the Valheim reference.
#
# Every tree has a far version (<name>_Far): a five-sided trunk and a few dozen big cards in the same
# silhouette, drawn past the near distance.
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix

exec(bpy.data.texts["hl_kit"].as_string())  # PALETTE, mat, coll, add_part, cyl, T, lumpy, ...
PALETTE["HL_LeafCard"] = (0.13, 0.26, 0.06)
PALETTE["HL_LeafCardLight"] = (0.20, 0.34, 0.05)
PALETTE["HL_LeafCardDark"] = (0.06, 0.16, 0.05)
PALETTE["HL_NeedleCard"] = (0.05, 0.15, 0.07)

REPO = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath), "..", ".."))
OUT = os.path.join(REPO, "Assets", "Hearthline", "Art", "Greybox")
made = []
AO_MIN = 0.42          # darkest baked shading, deep inside a crown
BEND = 0.8             # how far card normals bend toward the crown's outward direction
JITTER = 0.5           # how far each card turns away from facing outward


def remove_existing(name):
    ob = bpy.data.objects.get(name)
    if ob:
        bpy.data.objects.remove(ob, do_unlink=True)


class Builder:
    """One model: bark pieces (material 0) and leaf cards (material 1), with crown ellipsoids for shading."""

    def __init__(self, seed):
        self.rng = random.Random(seed)
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.col = self.bm.loops.layers.color.new("Col")
        self.crowns = []          # (centre, radii)
        self.tips = []

    # ---- wood ----
    def segment(self, p0, p1, r0, r1, sides=7):
        d = p1 - p0
        if d.length < 1e-4:
            return
        m = Matrix.Translation((p0 + p1) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        add_part(self.bm, cyl, 0, m, segments=sides, r1=r0, r2=r1, depth=d.length)

    def limb(self, points, r0, r1, sides=6):
        n = len(points) - 1
        for i in range(n):
            self.segment(points[i], points[i + 1], r0 + (r1 - r0) * i / n, r0 + (r1 - r0) * (i + 1) / n, sides)

    def bend(self, a, b, k, amount):
        r = self.rng
        return a.lerp(b, k) + Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(0, 1))) * amount

    def trunk(self, height, r0, r1, sway=0.25, sides=8):
        """A tapered trunk with a root flare, leaning a little at each joint. Returns the points up it."""
        r = self.rng
        self.segment(Vector((0, 0, -0.3)), Vector((0, 0, 0.7)), r0 * 1.55, r0, sides)
        pts = [Vector((0, 0, 0.7))]
        n = 4
        for i in range(1, n + 1):
            pts.append(Vector((r.uniform(-sway, sway) * i / n, r.uniform(-sway, sway) * i / n, 0.7 + (height - 0.7) * i / n)))
        self.limb(pts, r0, r1, sides)
        return pts

    # ---- leaves ----
    def crown(self, centre, radii):
        self.crowns.append((Vector(centre), Vector(radii)))

    def nearest_crown(self, p):
        best, bd = self.crowns[0], 1e9
        for c, rad in self.crowns:
            q = p - c
            d = Vector((q.x / rad.x, q.y / rad.y, q.z / rad.z)).length
            if d < bd:
                best, bd = (c, rad), d
        return best, bd

    def outward(self, p):
        (c, rad), _ = self.nearest_crown(p)
        q = p - c
        e = Vector((q.x / rad.x, q.y / rad.y, q.z / rad.z))
        o = Vector((e.x / rad.x, e.y / rad.y, e.z / rad.z))
        return o.normalized() if o.length > 1e-4 else Vector((0, 0, 1))

    def ao_at(self, p):
        (c, rad), d = self.nearest_crown(p)
        d = min(1.2, d)
        s = AO_MIN + (1 - AO_MIN) * max(0.0, min(1.0, (d - 0.25) / 0.75)) ** 1.2
        h = max(0.0, min(1.0, (p.z - (c.z - rad.z)) / (2 * rad.z)))
        return max(0.15, min(1.0, s * (0.8 + 0.2 * h)))

    def card(self, p, size, droop=0.0):
        r = self.rng
        out = self.outward(p)
        n = (out + Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(-1, 1))) * JITTER).normalized()
        up_raw = out * 0.5 + Vector((0, 0, 0.7 - droop * 1.4)) + Vector((r.uniform(-1, 1), r.uniform(-1, 1), 0)) * 0.3
        up = up_raw - n * up_raw.dot(n)
        up = up.normalized() if up.length > 1e-3 else Vector((0, 0, 1))
        right = up.cross(n).normalized()
        c = p + up * size * 0.2
        corners = [c - right * size / 2 - up * size / 2, c + right * size / 2 - up * size / 2,
                   c + right * size / 2 + up * size / 2, c - right * size / 2 + up * size / 2]
        f = self.bm.faces.new([self.bm.verts.new(x) for x in corners])
        f.material_index = 1
        for loop, t in zip(f.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]):
            loop[self.uv].uv = t
            a = self.ao_at(loop.vert.co)
            loop[self.col] = (a, a, a, 1.0)

    def leaves(self, per_tip, shell, size, spread=0.85, droop=0.0):
        r = self.rng
        for tip in self.tips:
            for _ in range(per_tip):
                self.card(tip + Vector((r.gauss(0, spread), r.gauss(0, spread), r.gauss(0, spread * 0.65))), r.uniform(*size), droop)
        vols = [rad.x * rad.y * rad.z for _, rad in self.crowns]
        total = sum(vols)
        for (c, rad), v in zip(self.crowns, vols):
            for _ in range(max(4, int(shell * v / total))):
                d = Vector((r.gauss(0, 1), r.gauss(0, 1), r.gauss(0, 1))).normalized()
                self.card(c + Vector((d.x * rad.x, d.y * rad.y, d.z * rad.z)) * r.uniform(0.7, 0.98), r.uniform(*size), droop)

    # ---- finish ----
    def build(self, name, bark, leaf, collection="Trees", at=(0, 0, 0)):
        remove_existing(name)
        bm = self.bm
        bm.faces.ensure_lookup_table()
        for f in bm.faces:
            f.smooth = True
            if f.material_index != 0:
                continue
            for loop in f.loops:
                co, n = loop.vert.co, loop.vert.normal
                u = math.atan2(n.y, n.x) / math.tau * 2.0
                if f.normal.z > 0.9 or f.normal.z < -0.9:
                    u = co.x
                loop[self.uv].uv = (u, co.z * 0.5)
                loop[self.col] = (1, 1, 1, 1)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me); bm.free()
        me.materials.append(mat(bark)); me.materials.append(mat(leaf))
        ob = bpy.data.objects.new(name, me)
        coll(collection).objects.link(ob)
        me.update()
        normals = []
        for poly in me.polygons:
            for li in poly.loop_indices:
                if poly.material_index == 1:
                    o = self.outward(me.vertices[me.loops[li].vertex_index].co)
                    face = poly.normal if poly.normal.dot(o) > 0 else -poly.normal
                    normals.append(face.lerp(o, BEND).normalized())
                else:
                    normals.append(me.corner_normals[li].vector.copy())
        me.normals_split_custom_set(normals)
        ob.location = at
        made.append(ob)
        return ob


def limbs_to_crown(b, base_pts, count, r0, r1, reach=0.75, up=(-0.1, 0.7), side_branches=True):
    """Limbs from the upper trunk out into the crowns; their ends (and side branches) carry the leaves."""
    r = b.rng
    for i in range(count):
        c, rad = b.crowns[i % len(b.crowns)]
        a = i / count * math.tau + r.uniform(-0.35, 0.35)
        d = Vector((math.cos(a), math.sin(a), r.uniform(*up))).normalized()
        tip = c + Vector((d.x * rad.x, d.y * rad.y, d.z * rad.z)) * reach
        start = base_pts[-1].lerp(base_pts[-2], r.uniform(0, 0.6))
        b.limb([start, b.bend(start, tip, 0.5, 0.35), tip], r0, r1, 6)
        b.tips.append(tip)
        if side_branches:
            mid = start.lerp(tip, 0.55)
            side = mid + Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(0.0, 0.6))).normalized() * min(rad.x, 1.6)
            b.limb([mid, side], r1 * 1.4, r1 * 0.5, 5)
            b.tips.append(side)


# ---- Species --------------------------------------------------------------------------------------------

def white_oak(name, seed, at):
    """White oak: a thick trunk, bare to about 7 m in the forest, a broad heavy crown on spreading limbs."""
    b = Builder(seed); r = b.rng
    pts = b.trunk(r.uniform(6.5, 7.5), 0.52, 0.34, sway=0.3)
    b.crown((r.uniform(-0.4, 0.4), r.uniform(-0.4, 0.4), 11.5), (5.4, 5.0, 3.6))
    limbs_to_crown(b, pts, 7, 0.26, 0.07)
    b.tips.append(b.crowns[0][0] + Vector((0, 0, 2.2)))
    b.limb([pts[-1], b.bend(pts[-1], b.tips[-1], 0.5, 0.3), b.tips[-1]], 0.22, 0.06)
    b.leaves(per_tip=22, shell=220, size=(1.5, 2.3))
    return b.build(name, "HL_Bark", "HL_LeafCard", at=at)


def poplar(name, seed, at):
    """Tulip poplar: the tallest in the cove, a ruler-straight pale trunk and a narrow, high crown."""
    b = Builder(seed); r = b.rng
    pts = b.trunk(r.uniform(13, 14.5), 0.46, 0.24, sway=0.08)
    b.crown((0, 0, 18.0), (3.0, 3.0, 4.8))
    limbs_to_crown(b, pts, 6, 0.16, 0.05, reach=0.7, up=(0.1, 0.9))
    top = Vector((0, 0, 22.5)); b.limb([pts[-1], top], 0.22, 0.05); b.tips.append(top)
    b.leaves(per_tip=18, shell=200, size=(1.3, 2.0))
    return b.build(name, "HL_BarkLight", "HL_LeafCardLight", at=at)


def hickory(name, seed, at):
    """Hickory: a crooked trunk and an irregular, open crown in two or three masses."""
    b = Builder(seed); r = b.rng
    pts = b.trunk(r.uniform(6.0, 7.0), 0.4, 0.25, sway=0.55)
    b.crown((r.uniform(0.5, 1.2), 0, 10.0), (3.4, 3.0, 2.6))
    b.crown((r.uniform(-1.6, -0.8), r.uniform(-0.8, 0.8), 11.8), (2.6, 2.6, 2.2))
    limbs_to_crown(b, pts, 6, 0.2, 0.06)
    b.leaves(per_tip=18, shell=150, size=(1.3, 2.0), spread=0.95)
    return b.build(name, "HL_Bark", "HL_LeafCard", at=at)


def pine(name, seed, at):
    """Shortleaf pine: a straight trunk bare most of the way, dark needle tufts on short limbs at the top."""
    b = Builder(seed); r = b.rng
    h = r.uniform(12.5, 14.5)
    pts = b.trunk(h, 0.38, 0.17, sway=0.15)
    for i in range(5):
        a = i / 5 * math.tau + r.uniform(-0.3, 0.3)
        z = h - 2.4 + i * 0.75
        c = Vector((math.cos(a) * r.uniform(1.0, 1.8), math.sin(a) * r.uniform(1.0, 1.8), z + 0.4))
        b.crown(c, (1.7, 1.7, 1.0))
        b.limb([Vector((pts[-1].x, pts[-1].y, z)), c], 0.1, 0.05, 5)
        b.tips.append(c)
    top = Vector((pts[-1].x, pts[-1].y, h + 1.4)); b.crown(top, (1.3, 1.3, 1.2)); b.tips.append(top)
    b.limb([pts[-1], top], 0.16, 0.05)
    for i in range(5):                       # dead stubs low on the trunk
        a = r.uniform(0, math.tau); z = r.uniform(4, h - 4)
        b.limb([Vector((0, 0, z)), Vector((math.cos(a) * 0.8, math.sin(a) * 0.8, z + 0.2))], 0.06, 0.02, 4)
    b.leaves(per_tip=26, shell=60, size=(1.1, 1.6), spread=0.6)
    return b.build(name, "HL_Bark", "HL_NeedleCard", at=at)


def hemlock(name, seed, at):
    """Eastern hemlock: by the creeks, dark and conical, its tiers drooping almost to the ground."""
    b = Builder(seed); r = b.rng
    h = r.uniform(13, 15)
    pts = b.trunk(h, 0.4, 0.12, sway=0.1)
    tiers = 7
    for i in range(tiers):
        z = 1.8 + i * (h - 1.8) / tiers
        rad = 3.3 * (1 - i / tiers) + 0.7
        b.crown((0, 0, z + 0.5), (rad, rad, 1.0))
        for k in range(5):
            a = k / 5 * math.tau + i * 0.6 + r.uniform(-0.3, 0.3)
            tip = Vector((math.cos(a) * rad * 0.6, math.sin(a) * rad * 0.6, z - 0.1))
            b.limb([Vector((0, 0, z + 0.4)), tip], 0.05, 0.02, 4)
            b.tips.append(tip)
    b.crown((0, 0, h + 0.8), (0.8, 0.8, 1.4)); b.tips.append(Vector((0, 0, h + 1.2)))
    b.leaves(per_tip=8, shell=200, size=(1.3, 1.9), spread=0.7, droop=0.45)
    return b.build(name, "HL_Bark", "HL_NeedleCard", at=at)


def dogwood(name, seed, at):
    """Flowering dogwood: a small understory tree, low and spreading, its leaves in flat layers."""
    b = Builder(seed); r = b.rng
    pts = b.trunk(2.0, 0.15, 0.1, sway=0.35, sides=6)
    b.crown((0, 0, 4.0), (2.8, 2.8, 1.3))
    limbs_to_crown(b, pts, 5, 0.08, 0.03, reach=0.8, up=(0.0, 0.5))
    b.leaves(per_tip=14, shell=80, size=(1.0, 1.5), spread=0.7)
    return b.build(name, "HL_Bark", "HL_LeafCardLight", at=at)


def far_version(name, near, crowns, trunk_h, trunk_r, cards, size, at, droop=0.0):
    """Few, big cards in the near tree's silhouette, on a five-sided trunk: cheap, and still not a lollipop."""
    b = Builder(hash(name) & 0xffff)
    b.segment(Vector((0, 0, -0.3)), Vector((0, 0, trunk_h)), trunk_r, trunk_r * 0.6, 5)
    for c, rad in crowns:
        b.crown(c, rad)
    b.leaves(per_tip=0, shell=cards, size=size, droop=droop)
    return b.build(name, near.data.materials[0].name, near.data.materials[1].name, at=at)


def thicket(name, seed, w, h, leaf, at):
    """Rhododendron or mountain laurel: a dense evergreen mound of leaf cards on twisted stems."""
    b = Builder(seed); r = b.rng
    b.crown((0, 0, h * 0.55), (w * 0.5, w * 0.5, h * 0.5))
    for i in range(6):
        a = r.uniform(0, math.tau)
        tip = Vector((math.cos(a) * w * 0.32, math.sin(a) * w * 0.32, h * r.uniform(0.5, 0.8)))
        b.limb([Vector((0, 0, -0.2)), tip], 0.06, 0.03, 4)
        b.tips.append(tip)
    b.leaves(per_tip=10, shell=int(30 * w), size=(0.9, 1.3), spread=0.5)
    return b.build(name, "HL_Bark", leaf, collection="Cover", at=at)


def sapling(name, seed, at):
    b = Builder(seed); r = b.rng
    pts = b.trunk(2.4, 0.07, 0.04, sway=0.2, sides=5)
    b.crown((0, 0, 3.0), (0.9, 0.9, 1.0))
    b.tips += [Vector((0, 0, 3.0)), Vector((0.4, 0.2, 2.6))]
    b.leaves(per_tip=8, shell=12, size=(0.7, 1.0), spread=0.4)
    return b.build(name, "HL_Bark", "HL_LeafCardLight", collection="Cover", at=at)


def stump(name, at):
    remove_existing(name)
    bm = bmesh.new()
    add_part(bm, cyl, 0, T(loc=(0, 0, 0.2)), segments=7, r1=0.55, r2=0.45, depth=1.0)
    add_part(bm, cyl, 0, T(loc=(0, 0, -0.1)), segments=7, r1=0.8, r2=0.55, depth=0.4)
    add_part(bm, cyl, 1, T(loc=(0, 0, 0.72)), segments=7, r1=0.46, r2=0.46, depth=0.04)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat("HL_Bark")); me.materials.append(mat("HL_WoodWeathered"))
    ob = bpy.data.objects.new(name, me); coll("Cover").objects.link(ob)
    for p in me.polygons: p.use_smooth = False
    ob.location = at
    made.append(ob)


# ---- Build and export -------------------------------------------------------------------------------------

def tree(fn, name, seed, x, far_crowns, trunk_h, trunk_r, cards, size, droop=0.0):
    near = fn(name, seed, (x, -200, 0))
    far_version(name + "_Far", near, far_crowns, trunk_h, trunk_r, cards, size, (x, -215, 0), droop)

V = Vector
tree(white_oak, "Tree_WhiteOak_A", 11, 0, [(V((0, 0, 11.5)), V((5.4, 5.0, 3.6)))], 8, 0.5, 46, (3.0, 4.0))
tree(white_oak, "Tree_WhiteOak_B", 23, 14, [(V((0, 0, 11.5)), V((5.4, 5.0, 3.6)))], 8, 0.5, 46, (3.0, 4.0))
tree(poplar, "Tree_Poplar_A", 5, 28, [(V((0, 0, 18)), V((3.0, 3.0, 4.8)))], 14, 0.4, 36, (2.6, 3.4))
tree(poplar, "Tree_Poplar_B", 9, 38, [(V((0, 0, 18)), V((3.0, 3.0, 4.8)))], 14, 0.4, 36, (2.6, 3.4))
tree(hickory, "Tree_Hickory_A", 31, 48, [(V((0.6, 0, 10.5)), V((3.8, 3.4, 3.0)))], 7, 0.36, 36, (2.6, 3.4))
tree(hickory, "Tree_Hickory_B", 37, 58, [(V((0.6, 0, 10.5)), V((3.8, 3.4, 3.0)))], 7, 0.36, 36, (2.6, 3.4))
tree(pine, "Tree_ShortleafPine_A", 41, 68, [(V((0, 0, 13.0)), V((2.6, 2.6, 2.2)))], 13, 0.32, 26, (2.2, 2.8))
tree(pine, "Tree_ShortleafPine_B", 47, 76, [(V((0, 0, 13.5)), V((2.6, 2.6, 2.2)))], 13, 0.32, 26, (2.2, 2.8))
tree(hemlock, "Tree_Hemlock_A", 53, 86, [(V((0, 0, 4.0)), V((3.6, 3.6, 2.4))), (V((0, 0, 8.5)), V((2.6, 2.6, 2.4))), (V((0, 0, 12.5)), V((1.4, 1.4, 2.2)))], 13, 0.36, 40, (2.4, 3.0), droop=0.45)
tree(dogwood, "Tree_Dogwood_A", 61, 96, [(V((0, 0, 4.0)), V((2.8, 2.8, 1.3)))], 3, 0.13, 22, (1.8, 2.4))

thicket("Rhododendron_A", 71, 3.6, 2.6, "HL_LeafCardDark", (0, -230, 0))
thicket("Rhododendron_B", 73, 4.2, 2.9, "HL_LeafCardDark", (6, -230, 0))
thicket("Laurel_A", 79, 2.4, 1.8, "HL_LeafCard", (12, -230, 0))
sapling("Sapling_A", 83, (17, -230, 0))
stump("Stump_A", (21, -230, 0))

# Store this script in the blend, beside hl_kit, so the file says how its forest was made.
src = os.path.join(REPO, "ArtSource", "Greybox", "forest_kit.py")
txt = bpy.data.texts.get("hl_forest") or bpy.data.texts.new("hl_forest")
txt.from_string(open(src, encoding="utf-8").read())

for ob in made:
    bpy.ops.object.select_all(action="DESELECT")
    was = ob.location.copy()
    ob.location = (0, 0, 0)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="OFF", colors_type="LINEAR",
                             add_leaf_bones=False, bake_anim=False)
    ob.location = was

bpy.ops.wm.save_mainfile()
print("FOREST " + ", ".join("%s:%d" % (o.name, len(o.data.polygons)) for o in made))
