# Brings a Tripo character (Mixamo-rigged FBX) into Unity (ADR 0031, ADR 0035).
#
# Run from the repository root:
#   blender -b --factory-startup --python ArtSource/Characters/export_tripo_character.py -- <source.fbx> <Name> [height_m]
#
# Tripo exports come out about a metre tall with a 0.01-scaled armature, and their textures sit in a .fbm folder
# beside the FBX. This scales the character to an adult height (default 1.70 m, top of the head), applies the
# scale so the rig is clean at 1.0, and exports mesh and rig to Assets/Hearthline/Art/Characters/<Name>/<Name>.fbx
# without embedded media. Textures are copied next to it: base colour and normal as they are, and metallic and
# roughness packed into one URP mask (R metallic, A smoothness = 1 - roughness).
import bpy, os, sys, glob, shutil
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
SOURCE, NAME = os.path.abspath(args[0]), args[1]
HEIGHT = float(args[2]) if len(args) > 2 else 1.70
REPO = os.getcwd()
OUT_DIR = os.path.join(REPO, "Assets", "Hearthline", "Art", "Characters", NAME)
os.makedirs(OUT_DIR, exist_ok=True)

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
meshes = [o for o in bpy.data.objects if o.type == 'MESH']

# A rigid head. Tripo's auto-rig ties the mouth, chin and cheeks partly to the neck (and faintly to the shoulders),
# and hair beside the eyes to an arm, so when an animation turns the head or raises an arm the face shears. Every
# vertex above the throat and within reach of the head goes to the head bone alone, with a blend to the neck across
# the throat so the neck still bends. Shoulders, the pack and anything else out of reach keep their weights.
bpy.context.view_layer.update()
bones = arm.data.bones
head_p = arm.matrix_world @ bones["mixamorig:Head"].head_local
neck_z = (arm.matrix_world @ bones["mixamorig:Neck"].head_local).z
span = head_p.z - neck_z
BLEND_FROM, BLEND_TO = neck_z + 0.1 * span, neck_z + 0.45 * span  # throat .. under the chin
REACH = 1.6 * span  # horizontal distance from the head joint that still counts as head and hair
def smooth(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)
for m in meshes:
    head_g, neck_g = m.vertex_groups.get("mixamorig:Head"), m.vertex_groups.get("mixamorig:Neck")
    if head_g is None or neck_g is None:
        continue
    changed = 0
    for v in m.data.vertices:
        if not any(g.weight > 0 for g in v.groups):
            continue
        p = m.matrix_world @ v.co
        if p.z <= BLEND_FROM or ((p.x - head_p.x) ** 2 + (p.y - head_p.y) ** 2) ** 0.5 > REACH:
            continue
        target = smooth(BLEND_FROM, BLEND_TO, p.z)
        for g in list(v.groups):
            if g.group not in (head_g.index, neck_g.index):
                m.vertex_groups[g.group].remove([v.index])
        head_g.add([v.index], target, 'REPLACE')
        if target >= 1.0:
            neck_g.remove([v.index])
        else:
            neck_g.add([v.index], 1.0 - target, 'REPLACE')
        changed += 1
    print("RIGID HEAD: %d vertices moved to the head (reach %.3f)" % (changed, REACH))

# Scale so the top of the mesh is at HEIGHT, feet on the ground.
bpy.context.view_layer.update()
def bounds():
    lo, hi = None, None
    for m in meshes:
        dg = m.evaluated_get(bpy.context.evaluated_depsgraph_get())
        for v in dg.data.vertices:
            p = dg.matrix_world @ v.co
            lo = p.z if lo is None else min(lo, p.z)
            hi = p.z if hi is None else max(hi, p.z)
    return lo, hi
lo, hi = bounds()
factor = HEIGHT / (hi - lo)
arm.scale = arm.scale * factor
arm.location.z -= lo * factor
bpy.context.view_layer.update()
for o in bpy.context.selected_objects:
    o.select_set(False)
for o in [arm] + meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=False, scale=True)
for m in meshes:
    bpy.context.view_layer.objects.active = m
    for o in bpy.context.selected_objects:
        o.select_set(False)
    m.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
lo, hi = bounds()
print("HEIGHT %.3f (from %.3f, factor %.3f)" % (hi - lo, HEIGHT / factor, factor))

for m in meshes:
    for i, slot in enumerate(m.material_slots):
        if slot.material:
            slot.material.name = NAME + "_Body" if i == 0 else NAME + "_" + str(i)

for o in bpy.context.selected_objects:
    o.select_set(False)
for o in [arm] + meshes:
    o.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT_DIR, NAME + ".fbx"), use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False,
    mesh_smooth_type='FACE', use_armature_deform_only=False)

# Textures: the .fbm folder Tripo (or Blender's importer) left beside the source.
fbm = glob.glob(os.path.splitext(SOURCE)[0] + ".fbm")
src = fbm[0] if fbm else os.path.dirname(SOURCE)
def find(suffix):
    hits = glob.glob(os.path.join(src, "*_" + suffix + ".png"))
    return hits[0] if hits else None
for suffix, out in (("basecolor", "_BaseColor"), ("normal", "_Normal")):
    f = find(suffix)
    if f:
        shutil.copyfile(f, os.path.join(OUT_DIR, NAME + out + ".png"))
metal, rough = find("metallic"), find("roughness")
if metal and rough:
    def channel(path):
        img = bpy.data.images.load(path)
        px = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        return tuple(img.size), px.reshape(-1, 4)[:, 0]
    size, m = channel(metal)
    size2, r = channel(rough)
    if size == size2:
        mask = bpy.data.images.new(NAME + "_Mask", size[0], size[1], alpha=True, float_buffer=False)
        mask.colorspace_settings.name = 'Non-Color'
        out = np.zeros((size[0] * size[1], 4), dtype=np.float32)
        out[:, 0] = m
        out[:, 1] = 1.0  # occlusion
        out[:, 3] = 1.0 - r
        mask.pixels.foreach_set(out.ravel())
        mask.filepath_raw = os.path.join(OUT_DIR, NAME + "_Mask.png")
        mask.file_format = 'PNG'
        mask.save()
print("EXPORTED " + OUT_DIR)
