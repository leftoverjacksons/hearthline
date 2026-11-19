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
