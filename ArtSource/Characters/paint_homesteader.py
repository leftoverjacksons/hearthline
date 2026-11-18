# Paints the homesteader's textures (ADR 0031): realistic shape, painted surface, after the reference
# ArtReference/people/Appalachian Homesteader Character Turnaround.png (green jacket with red shoulders over a
# mustard shirt, maroon trousers with knee patches, brown boots, greying dark hair, a short beard).
#
# Run from the repository root:
#   blender -b ArtSource/Characters/test_homesteader.blend --python ArtSource/Characters/paint_homesteader.py
#
# How: Cycles bakes each part's surface position into its texture space, so every texel knows where on the body
# it is. The original (MakeHuman CC0) textures, kept in ArtSource/Characters/source_textures, give the folds.
# Each texel is then repainted: a flat colour for its region of the costume, shaded by the softened folds of
# the original, its tones gently banded, with broad painterly blotches. The results overwrite the textures in
# Assets/Hearthline/Art/Characters/Homesteader, which CharacterKit already uses.
import bpy, os, math
import numpy as np

REPO = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath), "..", ".."))
SRC = os.path.join(REPO, "ArtSource", "Characters", "source_textures")
OUT = os.path.join(REPO, "Assets", "Hearthline", "Art", "Characters", "Homesteader")


def srgb(c):
    return np.array(c, dtype=np.float32)


# ---- Image helpers -------------------------------------------------------------------------------------

def load(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return px


def save(px, path):
    h, w = px.shape[:2]
    img = bpy.data.images.new("out", w, h, alpha=True)
    img.pixels[:] = np.clip(px, 0, 1).ravel()
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def blur(a, r):
    """Box blur by r pixels, twice (close to a gaussian), on an h x w (x c) array."""
    for _ in range(2):
        for axis in (0, 1):
            c = np.cumsum(np.pad(a, [(r + 1, r) if i == axis else (0, 0) for i in range(a.ndim)], mode='edge'), axis=axis)
            hi = np.take(c, np.arange(2 * r + 1, c.shape[axis]), axis=axis)
            lo = np.take(c, np.arange(0, c.shape[axis] - 2 * r - 1), axis=axis)
            a = (hi - lo) / (2 * r + 1)
    return a


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ---- Position bake -------------------------------------------------------------------------------------

def bake_positions(obj_name, material_names, size):
    """Bakes object-space surface position (rest pose) of the given materials' faces into a float image."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 1
    scene.cycles.device = 'CPU'
    obj = bpy.data.objects[obj_name]
    target = bpy.data.images.new("pos_" + obj_name, size, size, alpha=True, float_buffer=True)
    target.pixels[:] = np.zeros(size * size * 4, dtype=np.float32)
    dummy = bpy.data.images.new("dummy", 8, 8)
    saved = []
    for slot in obj.material_slots:
        m = slot.material
        nt = m.node_tree
        out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output)
        old = [l.from_socket for l in out.inputs['Surface'].links]
        geo = nt.nodes.new('ShaderNodeNewGeometry')
        emit = nt.nodes.new('ShaderNodeEmission')
        nt.links.new(geo.outputs['Position'], emit.inputs['Color'])
        nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])
        tex = nt.nodes.new('ShaderNodeTexImage')
        tex.image = target if m.name in material_names else dummy
        nt.nodes.active = tex
        saved.append((nt, out, old, [geo, emit, tex]))
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type='EMIT', margin=6, use_clear=False)
    pos = np.array(target.pixels[:], dtype=np.float32).reshape(size, size, 4)
    for nt, out, old, made in saved:
        for n in made: nt.nodes.remove(n)
        for s in old: nt.links.new(s, out.inputs['Surface'])
    bpy.data.images.remove(target)
    bpy.data.images.remove(dummy)
    return pos


# ---- Painting ------------------------------------------------------------------------------------------

def paint(src, pos, region_colour, fold_blur=10, keep_alpha=False, detail=0.0):
    """Repaints texels: region colour (h x w x 3, from position) x softened folds of the original, banded, with
    broad blotches. Texels outside the baked faces (position all zero) keep their original colour."""
    lum = src[..., :3] @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    covered = np.abs(pos[..., :3]).sum(axis=2) > 1e-6
    soft = blur(lum, fold_blur)
    mean = soft[covered].mean() if covered.any() else 0.5
    shade = np.clip(0.78 + 0.45 * (soft - mean) / max(mean, 1e-3), 0.55, 1.18)
    shade = shade * 0.5 + (np.round(shade * 6) / 6) * 0.5                     # gently banded tones
    p = pos[..., :3]
    blotch = (np.sin(p[..., 0] * 23 + p[..., 2] * 7) * np.sin(p[..., 1] * 19 - p[..., 2] * 11)) * 0.05
    fine = (lum - blur(lum, 2)) * detail                                         # a touch of the weave, if wanted
    col = region_colour * (shade + blotch + fine)[..., None]
    out = src.copy()
    out[..., :3] = np.where(covered[..., None], col, src[..., :3])
    if not keep_alpha:
        out[..., 3] = 1.0
    return out


def run():
    os.makedirs(SRC, exist_ok=True)
    names = ["Homesteader_Skin", "Homesteader_Jacket", "Homesteader_Trousers", "Homesteader_Boots", "Homesteader_Hair", "Homesteader_Eyebrows", "Homesteader_Eyes"]
    for n in names:   # keep the originals once, so the painting can be re-run
        if not os.path.exists(os.path.join(SRC, n + ".png")):
            import shutil
            shutil.copy(os.path.join(OUT, n + ".png"), os.path.join(SRC, n + ".png"))
    S = {n: load(os.path.join(SRC, n + ".png")) for n in names}

    # ---- Clothes: jacket, shirt, trousers ----
    jacket = S["Homesteader_Jacket"]
    size = jacket.shape[0]
    pj = bake_positions("Homesteader.male_casualsuit05", {"Homesteader.male_casualsuit05"}, size)
    x, y, z = pj[..., 0], pj[..., 1], pj[..., 2]
    ax = np.abs(x)
    covered = np.abs(pj[..., :3]).sum(axis=2) > 1e-6
    reach = ax[covered].max() if covered.any() else 0.8
    src_rgb = blur(jacket[..., :3], 6)
    redness = src_rgb[..., 0] - src_rgb[..., 1]
    green = srgb((0.2, 0.25, 0.23))
    brick = srgb((0.5, 0.2, 0.15))
    tan = srgb((0.58, 0.47, 0.3))
    mustard = srgb((0.7, 0.5, 0.14))
    colour = np.broadcast_to(green, jacket.shape[:2] + (3,)).copy()
    yoke = smooth(1.33, 1.37, z) * smooth(0.1, 0.13, ax) * (1 - smooth(0.2, 0.24, ax))  # shoulders only, not down the raised arms
    colour = colour * (1 - yoke[..., None]) + brick * yoke[..., None]
    cuff = smooth(reach - 0.13, reach - 0.1, ax)
    colour = colour * (1 - cuff[..., None]) + tan * cuff[..., None]
    shirt = (redness > 0.025) & (ax < 0.13) & (z > 0.95) & (z < 1.5)
    colour[shirt] = mustard
    save(paint(jacket, pj, colour, fold_blur=8), os.path.join(OUT, "Homesteader_Jacket.png"))

    trousers = S["Homesteader_Trousers"]
    pt = bake_positions("Homesteader.male_casualsuit05", {"Homesteader.trousers"}, trousers.shape[0])
    x, y, z = pt[..., 0], pt[..., 1], pt[..., 2]
    maroon = srgb((0.34, 0.14, 0.15))
    patch = srgb((0.38, 0.27, 0.18))
    colour = np.broadcast_to(maroon, trousers.shape[:2] + (3,)).copy()
    leg_x = np.where(x > 0, 0.1, -0.1)
    knee = (z > 0.4) & (z < 0.6) & (y < -0.02) & (np.abs(x - leg_x) < 0.075)
    colour[knee] = patch
    save(paint(trousers, pt, colour, fold_blur=8), os.path.join(OUT, "Homesteader_Trousers.png"))

    # ---- Boots ----
    boots = S["Homesteader_Boots"]
    pb = bake_positions("Homesteader.shoes03", {"Homesteader.shoes03"}, boots.shape[0])
    leather = srgb((0.38, 0.22, 0.12))
    colour = np.broadcast_to(leather, boots.shape[:2] + (3,)).copy()
    sole = pb[..., 2] < 0.025
    colour[sole] = srgb((0.16, 0.12, 0.09))
    save(paint(boots, pb, colour, fold_blur=4), os.path.join(OUT, "Homesteader_Boots.png"))

    # ---- Hair and eyebrows: dark, greying ----
    hair = S["Homesteader_Hair"]
    ph = bake_positions("Homesteader.short02", {"Homesteader.short02"}, hair.shape[0])
    # Greying in soft patches (more at the temples), keeping the strands of the original.
    grey = np.clip(0.25 + 0.35 * np.sin(ph[..., 0] * 30 + ph[..., 2] * 17) * np.sin(ph[..., 1] * 23) + 0.3 * smooth(0.05, 0.09, np.abs(ph[..., 0])), 0, 0.8)
    colour = srgb((0.13, 0.11, 0.1)) * (1 - grey[..., None]) + srgb((0.44, 0.42, 0.4)) * grey[..., None]
    save(paint(hair, ph, colour, fold_blur=2, keep_alpha=True, detail=1.4), os.path.join(OUT, "Homesteader_Hair.png"))
    brows = S["Homesteader_Eyebrows"]
    brows_out = brows.copy()
    brows_out[..., :3] = brows[..., :3] * 0.45
    save(brows_out, os.path.join(OUT, "Homesteader_Eyebrows.png"))

    # ---- Eyes: the whites toned down so they don't glow in shade ----
    eyes = S["Homesteader_Eyes"].copy()
    eyes[..., :3] = eyes[..., :3] * 0.7
    save(eyes, os.path.join(OUT, "Homesteader_Eyes.png"))

    # ---- Skin: warmer, simplified, with a short greying beard ----
    skin = S["Homesteader_Skin"]
    body_mats = {"Homesteader.body", "Homesteader.ears", "Homesteader.lips", "Homesteader.fingernails", "Homesteader.nipple", "Homesteader.toenails"}
    ps = bake_positions("Homesteader", body_mats, skin.shape[0])
    x, y, z = ps[..., 0], ps[..., 1], ps[..., 2]
    ax = np.abs(x)
    covered = np.abs(ps[..., :3]).sum(axis=2) > 1e-6
    head = covered & (z > 1.5)
    cy = y[head].mean() if head.any() else 0.0
    front = head & (y < cy - 0.05) & (ax < 0.02)
    chin = z[front].min() if front.any() else 1.55
    mouth = chin + 0.045
    base = blur(skin[..., :3], 6) * 0.5 + srgb((0.6, 0.42, 0.3)) * 0.5
    # A short, close beard: jaw and chin, a moustache, feathered at the edges, brown going grey.
    beard = (z > chin - 0.03) & (z < mouth + 0.004) & (y < cy + 0.01) & (ax < 0.068)
    moustache = (z > mouth + 0.007) & (z < mouth + 0.02) & (ax < 0.026) & (y < cy - 0.05)
    lips = (ax < 0.03) & (np.abs(z - mouth) < 0.009) & (y < cy - 0.05)
    hairy = ((beard | moustache) & ~lips & covered).astype(np.float32)
    hairy = blur(hairy, 3) * covered
    grey = np.clip(0.3 + 0.3 * np.sin(x * 90) * np.sin(z * 70), 0, 0.7)
    beard_col = srgb((0.24, 0.19, 0.16)) * (1 - grey[..., None]) + srgb((0.5, 0.48, 0.45)) * grey[..., None]
    colour = base * (1 - hairy[..., None] * 0.85) + beard_col * (hairy[..., None] * 0.85)
    save(paint(skin, ps, colour, fold_blur=5), os.path.join(OUT, "Homesteader_Skin.png"))
    print("PAINTED chin %.3f head-y %.3f reach %.3f" % (chin, cy, reach))


run()
