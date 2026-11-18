# The built-world kit (ADR 0029): chunky, hand-made-looking low-poly pieces in the Valheim manner, replacing the
# primitive boxes the ruins were made of. Built by script into region_kit.blend, exported one FBX per piece to
# Assets/Hearthline/Art/Greybox.
#
# Run from the repository root:
#   blender -b ArtSource/Greybox/region_kit.blend --python ArtSource/Greybox/build_kit.py
#
# Unit pieces are about 1 x 1 x 1 m, centred on the origin, so RuinKit scales them exactly as it scaled its
# boxes: stone wall runs with broken tops, plank walls made of separate boards, roof boards, corrugated tin,
# bevelled blocks. Bespoke pieces: the barrow mound, standing stones, rocks (replacing Rock_A/B) and kudzu
# made of leaf cards (replacing Kudzu_Mound/Drape).
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler

REPO = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath), "..", ".."))
LIBRARY_ONLY = True
try:
    exec(open(os.path.join(REPO, "ArtSource", "Greybox", "forest_kit.py"), encoding="utf-8").read())
except SystemExit:
    pass
OUT = os.path.join(REPO, "Assets", "Hearthline", "Art", "Greybox")
made = []


def finish_bm(name, bm, materials, collection="Kit", at=(0, 0, 0), flat=True, recalc=True):
    remove_existing(name)
    if recalc:
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0004)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        ax = max(range(3), key=lambda k: abs(f.normal[k]))
        for loop in f.loops:
            c = loop.vert.co
            loop[uv].uv = [(c.y, c.z), (c.x, c.z), (c.x, c.y)][ax]
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in materials: me.materials.append(mat(m))
    for p in me.polygons: p.use_smooth = not flat
    ob = bpy.data.objects.new(name, me)
    coll(collection).objects.link(ob)
    ob.location = at
    made.append(ob)
    return ob


def chunk(bm, centre, size, rng, mi=0, bevel=0.06, jitter=0.025, cuts=1):
    """A bevelled box with its corners nudged: a hand-cut block, never a perfect cube."""
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    if cuts:
        bmesh.ops.subdivide_edges(tmp, edges=tmp.edges[:], cuts=cuts, use_grid_fill=True)
    if bevel > 0 and cuts == 0:
        bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
    for v in tmp.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * jitter
        v.co += Vector(centre)
    for f in tmp.faces: f.material_index = mi
    me = bpy.data.meshes.new("_tmp"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)


# ---- Unit pieces (1 x 1 x 1 m, centred) -------------------------------------------------------------------

def stone_wall(name, seed, material="HL_Stone", ruined=0.35):
    """A run of fieldstone: courses of irregular blocks, the top broken and stepping down."""
    rng = random.Random(seed)
    bm = bmesh.new()
    courses = 5
    for c in range(courses):
        z0 = -0.5 + c / courses
        h = 1.0 / courses
        x = -0.5
        while x < 0.49:
            w = rng.uniform(0.18, 0.34)
            w = min(w, 0.5 - x)
            top = z0 + h
            # Broken top: the upper courses go missing toward the ends of the run.
            if c >= courses - 2 and rng.random() < ruined:
                x += w
                continue
            chunk(bm, (x + w / 2, rng.uniform(-0.02, 0.02), z0 + h / 2), (w * 0.97, rng.uniform(0.9, 1.04), h * 0.94), rng, jitter=0.012, bevel=0, cuts=0)
            x += w
    return finish_bm(name, bm, [material])


def plank_wall(name, seed, material):
    """Upright boards with gaps between, uneven tops, one or two leaning or missing."""
    rng = random.Random(seed)
    bm = bmesh.new()
    boards = 6
    for i in range(boards):
        if rng.random() < 0.08:
            continue
        x = -0.5 + (i + 0.5) / boards
        top = rng.uniform(0.82, 1.0)
        tilt = rng.uniform(-0.04, 0.04)
        tmp_c = (x, 0, -0.5 + top / 2)
        chunk(bm, tmp_c, (1.0 / boards * 0.9, 0.85, top), rng, jitter=0.01, bevel=0, cuts=0)
    # Two cross rails behind.
    for z in (-0.3, 0.25):
        chunk(bm, (0, 0.35, z), (1.0, 0.25, 0.08), rng, jitter=0.005, bevel=0, cuts=0)
    return finish_bm(name, bm, [material])


def concrete_wall(name, seed):
    """Cast concrete: one slab with a cracked, chipped top edge and a few chunks out of the face."""
    rng = random.Random(seed)
    bm = bmesh.new()
    segs = 6
    for i in range(segs):
        x = -0.5 + (i + 0.5) / segs
        top = 1.0 - (rng.uniform(0, 0.25) if rng.random() < 0.5 else 0.0)
        chunk(bm, (x, 0, -0.5 + top / 2), (1.0 / segs * 1.01, 1.0, top), rng, jitter=0.008, bevel=0, cuts=0)
    return finish_bm(name, bm, ["HL_Concrete"])


def boards(name, seed, material, count=5):
    """Roof or floor boards side by side, sagging a little in the middle."""
    rng = random.Random(seed)
    bm = bmesh.new()
    for i in range(count):
        y = -0.5 + (i + 0.5) / count
        sag = rng.uniform(0.0, 0.15)
        for k in range(3):
            x = -0.5 + (k + 0.5) / 3
            z = -abs(x) * 0 - (sag if k == 1 else sag * 0.4)
            chunk(bm, (x, y, z * 0.6), (1.0 / 3 * 1.02, 1.0 / count * 0.92, 1.0), rng, jitter=0.01, bevel=0, cuts=0)
    return finish_bm(name, bm, [material])


def tin_sheet(name, seed):
    """Corrugated sheet: ridges along x, a bent corner."""
    rng = random.Random(seed)
    bm = bmesh.new()
    nx, ny = 24, 4
    verts = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            x = -0.5 + i / nx
            y = -0.5 + j / ny
            z = 0.25 * math.sin(i / nx * math.tau * 8)
            z += max(0, (x + y) - 0.5) * 0.35 * rng.uniform(0.6, 1.0)  # a bent corner
            row.append(bm.verts.new((x, y, z * 0.5)))
        verts.append(row)
    for j in range(ny):
        for i in range(nx):
            bm.faces.new([verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]])
    # A sheet is seen from both sides: give it a back, a hair below.
    back = bmesh.new()
    for f in list(bm.faces):
        vs = [back.verts.new(v.co - Vector((0, 0, 0.01))) for v in reversed(f.verts)]
        back.faces.new(vs)
    me = bpy.data.meshes.new("_tmp"); back.to_mesh(me); back.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
    return finish_bm(name, bm, ["HL_TinRust"], recalc=False)


def block(name, seed, material):
    """A bevelled block with its corners nudged: posts, beams, counters, pumps, chimneys."""
    rng = random.Random(seed)
    bm = bmesh.new()
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=0.07, segments=1, affect='EDGES', clamp_overlap=True)
    for v in tmp.verts:
        v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * 0.02
    me = bpy.data.meshes.new("_tmp"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
    return finish_bm(name, bm, [material])


def standing_stone(name, seed):
    """A standing stone: a tall, tapering, weathered slab, leaning a little, top rounded off."""
    rng = random.Random(seed)
    bm = bmesh.new()
    rings = 6
    sides = 7
    prev = None
    first = None
    for r in range(rings + 1):
        t = r / rings
        z = -0.5 + t
        w = 0.5 * (1.0 - 0.35 * t) * (1.0 - 0.6 * max(0, t - 0.8) / 0.2)
        d = 0.32 * (1.0 - 0.25 * t)
        ring = []
        for s in range(sides):
            a = s / sides * math.tau
            jitter = 1.0 + rng.uniform(-0.12, 0.12)
            ring.append(bm.verts.new((math.cos(a) * w * jitter + t * 0.05, math.sin(a) * d * jitter, z)))
        if prev:
            for s in range(sides):
                bm.faces.new([prev[s], prev[(s + 1) % sides], ring[(s + 1) % sides], ring[s]])
        else:
            first = ring
        prev = ring
    bm.faces.new(list(reversed(first)))
    bm.faces.new(prev)
    return finish_bm(name, bm, ["HL_Stone"])


def rock(name, seed, size, moss=True):
    """A faceted boulder: a flattened, lumpy icosphere, moss on its upper faces."""
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    lumpy(bm, bm.verts, seed, amount=0.3, freq=1.1)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], max(v.co.z, -0.35) * size[2]))
    bm.normal_update()
    for f in bm.faces:
        f.material_index = 1 if (moss and f.normal.z > 0.55) else 0
    return finish_bm(name, bm, ["HL_Rock", "HL_RockMoss"])


def mound(name, seed):
    """The barrow mound: a long earth shell over the hall, closed at the back, open at the front (the
    stones fill the face). Unit size: 1 m long (y), 1 m wide (x), 1 m high; RuinKit scales it."""
    rng = random.Random(seed)
    bm = bmesh.new()
    nu, nv = 18, 10
    grid = []
    for j in range(nv + 1):            # along the length, from the back (y = -0.5) to the open front (0.5)
        y = -0.5 + j / nv
        back = max(0.0, 1.0 - (y + 0.5) / 0.22)          # the back end rounds down to the ground
        row = []
        for i in range(nu + 1):        # across, from one foot to the other
            a = math.pi * i / nu
            x = math.cos(a) * 0.5
            z = math.sin(a)
            shrink = math.sqrt(max(0.0, 1.0 - back * back))
            n = noise.noise(Vector((x * 3 + seed, y * 3, z * 3))) * 0.05
            row.append(bm.verts.new((x * (0.6 + 0.4 * shrink) + n, y, -0.5 + z * shrink + n)))
        grid.append(row)
    for j in range(nv):
        for i in range(nu):
            bm.faces.new([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]])
    return finish_bm(name, bm, ["HL_GrassDry"], flat=False)


# ---- Homestead dwellings (same footprints as the blockouts they replace; front toward -y) -------------------

def rod(bm, a, b, r, mi, sides=7, jitter=0.0, rng=None):
    """A log, pole or beam from a to b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    m = Matrix.Translation((a + b) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    tmp = bmesh.new()
    bmesh.ops.create_cone(tmp, cap_ends=True, segments=sides, radius1=r, radius2=r * 0.92, depth=d.length)
    if jitter and rng:
        for v in tmp.verts: v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * jitter
    bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
    for f in tmp.faces: f.material_index = mi
    me = bpy.data.meshes.new("_tmp"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)


def slab(bm, centre, size, rot, mi, rng, jitter=0.01):
    """A board or panel: a block turned by rot (radians, xyz)."""
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    for v in tmp.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * jitter
    bmesh.ops.transform(tmp, matrix=Matrix.Translation(Vector(centre)) @ Euler(rot).to_matrix().to_4x4(), verts=tmp.verts)
    for f in tmp.faces: f.material_index = mi
    me = bpy.data.meshes.new("_tmp"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)


def pitched_roof(bm, x0, x1, y0, y1, eave, ridge, mi, rng, boards=8, along_x=True):
    """A gable roof of boards: two slopes from the eaves (z = eave) up to a ridge along y."""
    half = (x1 - x0) / 2
    cx = (x0 + x1) / 2
    length = y1 - y0
    pitch = math.atan2(ridge - eave, half)
    run = math.hypot(half, ridge - eave) + 0.35
    for side in (-1, 1):
        for i in range(boards):
            y = y0 + (i + 0.5) * length / boards
            mid = Vector((cx + side * half / 2 + side * 0.12, y, (eave + ridge) / 2 + 0.1 + rng.uniform(-0.03, 0.03)))
            slab(bm, mid, (run, length / boards * 0.96, 0.12), (0, side * pitch, 0), mi, rng)


def tent(name, seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    # Poles: an A at each end and a ridge pole.
    for y in (-2.9, 1.6):
        rod(bm, (-1.5, y, 0), (0, y, 2.05), 0.05, 1, 5)
        rod(bm, (1.5, y, 0), (0, y, 2.05), 0.05, 1, 5)
    rod(bm, (0, -3.2, 2.05), (0, 1.9, 2.05), 0.05, 1, 5)
    # Canvas: two sagging sheets.
    for side in (-1, 1):
        nx, ny = 4, 8
        grid = []
        for j in range(ny + 1):
            y = -2.9 + 4.5 * j / ny
            row = []
            for i in range(nx + 1):
                t = i / nx
                x = side * 1.55 * t
                z = 2.0 - 2.0 * t
                sag = 0.12 * math.sin(math.pi * t) * (0.6 + 0.4 * math.sin(math.pi * j / ny))
                row.append(bm.verts.new((x * (1 - sag * 0.3), y, z - sag)))
            grid.append(row)
        for j in range(ny):
            for i in range(nx):
                f = bm.faces.new([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]])
                f.material_index = 0
    # A back wall of canvas, and a bedroll inside.
    back = [bm.verts.new(v) for v in [(-1.5, 1.6, 0), (1.5, 1.6, 0), (0, 1.6, 2.0)]]
    bm.faces.new(back).material_index = 0
    slab(bm, (0.4, 0.2, 0.12), (0.8, 1.9, 0.22), (0, 0, 0.05), 2, rng)
    return finish_bm(name, bm, ["HL_TentCanvas", "HL_WoodDark", "HL_Blanket"], recalc=False)


def log_wall(bm, a, b, height, rng, mi, gaps=(), log_r=0.17, overhang=0.35):
    """Stacked logs from a to b (along x or y), notched past the corners, with openings as (start, end, bottom, top) along the run."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    length = (b - a).length
    courses = int(height / (log_r * 1.9))
    for c in range(courses):
        z = log_r + c * log_r * 1.9
        pieces = [(-overhang, length + overhang)]
        for (s0, s1, z0, z1) in gaps:
            if z0 <= z <= z1:
                nxt = []
                for (p0, p1) in pieces:
                    if s1 <= p0 or s0 >= p1: nxt.append((p0, p1)); continue
                    if s0 > p0: nxt.append((p0, s0))
                    if s1 < p1: nxt.append((s1, p1))
                pieces = nxt
        for (p0, p1) in pieces:
            if p1 - p0 < 0.2: continue
            rod(bm, a + d * p0 + Vector((0, 0, z)), a + d * p1 + Vector((0, 0, z)), log_r * rng.uniform(0.92, 1.08), mi, 7, 0.02, rng)


def window(bm, centre, facing, rng, frame_mi, glow_mi, w=0.9, h=0.9):
    """A frame with a glowing pane (lit at night by the window material)."""
    cx, cy, cz = centre
    if facing == "x":
        slab(bm, (cx, cy, cz), (0.06, w, h), (0, 0, 0), glow_mi, rng, 0)
        for dz in (-h / 2, h / 2): slab(bm, (cx, cy, cz + dz), (0.2, w + 0.2, 0.1), (0, 0, 0), frame_mi, rng)
        for dy in (-w / 2, w / 2): slab(bm, (cx, cy + dy, cz), (0.2, 0.1, h + 0.2), (0, 0, 0), frame_mi, rng)
    else:
        slab(bm, (cx, cy, cz), (w, 0.06, h), (0, 0, 0), glow_mi, rng, 0)
        for dz in (-h / 2, h / 2): slab(bm, (cx, cy, cz + dz), (w + 0.2, 0.2, 0.1), (0, 0, 0), frame_mi, rng)
        for dx in (-w / 2, w / 2): slab(bm, (cx + dx, cy, cz), (0.1, 0.2, h + 0.2), (0, 0, 0), frame_mi, rng)


def cabin(name, seed):
    """A log cabin: notched logs, a door and two lit windows, a board roof, a stone chimney, a porch."""
    rng = random.Random(seed)
    bm = bmesh.new()
    # Materials: 0 logs, 1 stone, 2 tin, 3 dark wood, 4 window glow.
    x0, x1, y0, y1, wall = -3.4, 3.4, -2.4, 3.0, 2.9
    slab(bm, (0, (y0 + y1) / 2, -0.05), (x1 - x0 + 0.4, y1 - y0 + 0.4, 0.5), (0, 0, 0), 1, rng, 0.03)     # stone footing
    log_wall(bm, (x0, y0, 0.2), (x1, y0, 0.2), wall, rng, 0, gaps=[(2.9, 3.9, 0, 2.2), (0.9, 2.0, 1.1, 2.1)])  # front: door, window
    log_wall(bm, (x0, y1, 0.2), (x1, y1, 0.2), wall, rng, 0)
    log_wall(bm, (x0, y0, 0.37), (x0, y1, 0.37), wall, rng, 0, gaps=[(2.1, 3.2, 1.1, 2.1)])
    log_wall(bm, (x1, y0, 0.37), (x1, y1, 0.37), wall, rng, 0)
    # Gable ends: boards in the triangles.
    for y in (y0, y1):
        for k in range(7):
            x = x0 + 0.25 + k * (x1 - x0 - 0.5) / 6
            hgt = (1.0 - abs(x) / 3.6) * 2.0
            if hgt > 0.15: slab(bm, (x, y, wall + 0.2 + hgt / 2), (0.95, 0.12, hgt), (0, 0, 0), 3, rng)
    pitched_roof(bm, x0 - 0.5, x1 + 0.5, y0 - 0.6, y1 + 0.5, wall + 0.25, wall + 2.35, 3, rng, boards=9)
    # Door, windows.
    slab(bm, (-0.0, y0 - 0.05, 1.15), (0.95, 0.1, 2.1), (0, 0, 0), 3, rng)
    window(bm, (-1.95, y0 - 0.12, 1.6), "y", rng, 3, 4)
    window(bm, (x0 - 0.12, 0.25, 1.6), "x", rng, 3, 4)
    # Chimney.
    for k in range(9):
        slab(bm, (x1 + 0.55, 1.2, 0.35 + k * 0.62), (1.0 - k * 0.02, 1.1 - k * 0.02, 0.6), (0, 0, rng.uniform(-0.05, 0.05)), 1, rng, 0.03)
    # Porch: boards, two posts, a tin roof, a step.
    for k in range(10):
        slab(bm, (x0 + 0.35 + k * (x1 - x0 - 0.7) / 9, (y0 - 1.3), 0.32), ((x1 - x0) / 10 * 0.95, 2.4, 0.1), (0, 0, 0), 3, rng)
    for x in (x0 + 0.3, x1 - 0.3):
        rod(bm, (x, y0 - 2.35, 0.3), (x, y0 - 2.35, 2.55), 0.1, 3, 6)
    slab(bm, (0, y0 - 1.35, 2.65), (x1 - x0 + 0.6, 2.7, 0.08), (math.radians(-12), 0, 0), 2, rng, 0.01)
    slab(bm, (0, y0 - 2.75, 0.12), (1.6, 0.5, 0.22), (0, 0, 0), 3, rng)
    return finish_bm(name, bm, ["HL_WoodWeathered", "HL_Stone", "HL_TinRust", "HL_WoodDark", "HL_WindowGlow"])


def farmhouse(name, seed):
    """A two-storey board farmhouse: lit windows in two rows, a tin roof, a porch along the front, a chimney."""
    rng = random.Random(seed)
    bm = bmesh.new()
    x0, x1, y0, y1, wall = -4.8, 4.8, -2.8, 3.8, 5.6
    slab(bm, (0, (y0 + y1) / 2, 0.1), (x1 - x0 + 0.3, y1 - y0 + 0.3, 0.6), (0, 0, 0), 1, rng, 0.03)
    # Board walls: vertical boards on four sides.
    def boards_side(a, b, facing):
        n = int((b - a) / 0.3)
        for k in range(n):
            t = a + (k + 0.5) * (b - a) / n
            h = wall + rng.uniform(-0.06, 0.06)
            if facing == "front": slab(bm, (t, y0, 0.4 + h / 2), ((b - a) / n * 0.96, 0.12, h), (0, 0, 0), 0, rng, 0.006)
            elif facing == "back": slab(bm, (t, y1, 0.4 + h / 2), ((b - a) / n * 0.96, 0.12, h), (0, 0, 0), 0, rng, 0.006)
            elif facing == "left": slab(bm, (x0, t, 0.4 + h / 2), (0.12, (b - a) / n * 0.96, h), (0, 0, 0), 0, rng, 0.006)
            else: slab(bm, (x1, t, 0.4 + h / 2), (0.12, (b - a) / n * 0.96, h), (0, 0, 0), 0, rng, 0.006)
    boards_side(x0, x1, "front"); boards_side(x0, x1, "back"); boards_side(y0, y1, "left"); boards_side(y0, y1, "right")
    # Windows: two rows on the front, one per floor on each side; a door.
    for z in (1.9, 4.6):
        for x in (-3.0, -1.3, 1.6, 3.2):
            if z < 3 and abs(x - 1.6) < 0.1: continue
            window(bm, (x, y0 - 0.1, z), "y", rng, 3, 4, w=0.85, h=1.1)
    for z in (1.9, 4.6):
        window(bm, (x0 - 0.1, 0.5, z), "x", rng, 3, 4, w=0.85, h=1.1)
        window(bm, (x1 + 0.1, 0.5, z), "x", rng, 3, 4, w=0.85, h=1.1)
    slab(bm, (1.6, y0 - 0.08, 1.45), (1.0, 0.1, 2.1), (0, 0, 0), 3, rng)
    # Tin roof on a gable, with board gables.
    for y in (y0, y1):
        for k in range(9):
            x = x0 + 0.3 + k * (x1 - x0 - 0.6) / 8
            hgt = (1.0 - abs(x) / 5.0) * 2.6
            if hgt > 0.15: slab(bm, (x, y, wall + 0.4 + hgt / 2), (1.1, 0.12, hgt), (0, 0, 0), 0, rng)
    half = (x1 - x0) / 2 + 0.4
    pitch = math.atan2(2.7, half)
    for side in (-1, 1):
        slab(bm, (side * half / 2, (y0 + y1) / 2, wall + 0.4 + 1.4), (math.hypot(half, 2.7) + 0.3, y1 - y0 + 1.0, 0.1), (0, side * pitch, 0), 2, rng, 0.01)
    # Chimney.
    for k in range(14):
        slab(bm, (-2.6, 2.2, 0.4 + k * 0.62), (0.9, 0.9, 0.6), (0, 0, rng.uniform(-0.04, 0.04)), 1, rng, 0.03)
    # Porch along the front.
    for k in range(14):
        slab(bm, (x0 + 0.3 + k * (x1 - x0 - 0.6) / 13, y0 - 1.6, 0.45), ((x1 - x0) / 14 * 0.95, 3.0, 0.1), (0, 0, 0), 3, rng)
    for x in (x0 + 0.3, -1.6, 1.6, x1 - 0.3):
        rod(bm, (x, y0 - 2.9, 0.45), (x, y0 - 2.9, 3.0), 0.1, 3, 6)
    slab(bm, (0, y0 - 1.7, 3.15), (x1 - x0 + 0.6, 3.4, 0.08), (math.radians(-10), 0, 0), 2, rng, 0.01)
    slab(bm, (1.6, y0 - 3.4, 0.18), (1.8, 0.6, 0.3), (0, 0, 0), 3, rng)
    return finish_bm(name, bm, ["HL_WoodWeathered", "HL_Stone", "HL_TinRust", "HL_WoodDark", "HL_WindowGlow"])


# ---- Grass cards (ADR 0030) --------------------------------------------------------------------------------

def grass_card(name, height, width, material, seed, cards=4):
    """A clump of grass: crossed cards showing the blade texture, dark at the root and light at the tip, with
    normals pointing straight up so the grass is lit like the ground it grows from (Valheim's meadows)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    col = bm.loops.layers.color.new("Col")
    for i in range(cards):
        a = i / cards * math.pi + rng.uniform(-0.2, 0.2)
        dx, dy = math.cos(a) * width / 2, math.sin(a) * width / 2
        lean = Vector((rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), 0))
        corners = [(-dx, -dy, -0.03), (dx, dy, -0.03), (dx + lean.x, dy + lean.y, height), (-dx + lean.x, -dy + lean.y, height)]
        f = bm.faces.new([bm.verts.new(c) for c in corners])
        for loop, t in zip(f.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]):
            loop[uv].uv = t
            a_o = 0.68 + 0.32 * t[1]
            loop[col] = (a_o, a_o, a_o, 1.0)
    remove_existing(name)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    me.materials.append(mat("HL_Bark")); me.materials.append(mat(material))
    for p in me.polygons:
        p.material_index = 1
        p.use_smooth = True
    me.normals_split_custom_set([Vector((0, 0, 1))] * len(me.loops))
    ob = bpy.data.objects.new(name, me)
    coll("Cover").objects.link(ob)
    made.append(ob)
    return ob


# ---- Build ------------------------------------------------------------------------------------------------

ROW = -260
pieces = [
    lambda: stone_wall("Unit_Wall_Stone", 1),
    lambda: plank_wall("Unit_Wall_PlanksWeathered", 2, "HL_WoodWeathered"),
    lambda: plank_wall("Unit_Wall_PlanksDark", 3, "HL_WoodDark"),
    lambda: concrete_wall("Unit_Wall_Concrete", 4),
    lambda: boards("Unit_Boards_Weathered", 5, "HL_WoodWeathered"),
    lambda: boards("Unit_Boards_Dark", 6, "HL_WoodDark"),
    lambda: tin_sheet("Unit_Tin", 7),
    lambda: block("Unit_Block_Stone", 8, "HL_Stone"),
    lambda: block("Unit_Block_WoodWeathered", 9, "HL_WoodWeathered"),
    lambda: block("Unit_Block_WoodDark", 10, "HL_WoodDark"),
    lambda: block("Unit_Block_Concrete", 11, "HL_Concrete"),
    lambda: block("Unit_Block_Steel", 12, "HL_Steel"),
    lambda: block("Unit_Block_TinRust", 13, "HL_TinRust"),
    lambda: block("Unit_Block_BarrelBlue", 14, "HL_BarrelBlue"),
    lambda: block("Unit_Block_Asphalt", 15, "HL_Asphalt"),
    lambda: standing_stone("Unit_StandingStone", 16),
    lambda: mound("Unit_Mound", 17),
    lambda: rock("Rock_A", 21, (1.0, 0.85, 0.55)),
    lambda: rock("Rock_B", 22, (2.0, 1.6, 1.0)),
    lambda: tent("Dwelling_Tent", 41),
    lambda: cabin("Cabin", 42),
    lambda: farmhouse("Dwelling_Farmhouse", 43),
    lambda: grass_card("Grass_Card_Tall", 0.8, 1.3, "HL_GrassCard", 51),
    lambda: grass_card("Grass_Card_Short", 0.45, 1.1, "HL_GrassCard", 52),
    lambda: grass_card("Grass_Card_Dry", 0.7, 1.2, "HL_GrassCardDry", 53),
]
for i, build_piece in enumerate(pieces):
    ob = build_piece()
    ob.location = (i * 3.0, ROW, 0)

# Kudzu: a mound and a drape of big, light leaf cards (the vine that eats the Southeast).
def kudzu(name, seed, w, h, at):
    b = Builder(seed); r = b.rng
    b.crown((0, 0, h * 0.4), (w * 0.5, w * 0.5, h * 0.55))
    for i in range(5):
        a = r.uniform(0, math.tau)
        tip = Vector((math.cos(a) * w * 0.3, math.sin(a) * w * 0.3, h * r.uniform(0.3, 0.7)))
        b.tips.append(tip)
    b.leaves(per_tip=10, shell=int(26 * w), size=(0.8, 1.2), spread=0.6, droop=0.3)
    return b.build(name, "HL_Bark", "HL_LeafCard", collection="Cover", at=at)

kudzu("Kudzu_Mound", 31, 2.6, 1.6, (0, ROW - 8, 0))
kudzu("Kudzu_Drape", 33, 3.2, 2.4, (6, ROW - 8, 0))

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

txt = bpy.data.texts.get("hl_build_kit") or bpy.data.texts.new("hl_build_kit")
txt.from_string(open(os.path.join(REPO, "ArtSource", "Greybox", "build_kit.py"), encoding="utf-8").read())
bpy.ops.wm.save_mainfile()
print("KIT " + ", ".join("%s:%d" % (o.name, len(o.data.polygons)) for o in made))
