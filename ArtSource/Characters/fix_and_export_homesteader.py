# Fixes the homesteader's jacket hem and exports him to Unity (ADR 0031).
#
# Run from the repository root:
#   blender -b ArtSource/Characters/test_homesteader.blend --python ArtSource/Characters/fix_and_export_homesteader.py
#
# A few jacket hem vertices were weighted to the forearms (the hands rest by the hips in MakeHuman's A-pose), so
# when the rig was re-rested to a T-pose they went up with the arms and stuck out as spikes at the hips. Then the
# rig and the Homesteader meshes are exported with the settings the model has always used.
import bpy, os

REPO = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath), "..", ".."))
OUT = os.path.join(REPO, "Assets", "Hearthline", "Art", "Characters", "Homesteader", "Homesteader.fbx")

suit = bpy.data.objects["Homesteader.male_casualsuit05"]
me = suit.data
groups = {g.index: g.name for g in suit.vertex_groups}
def arm_weight(v):
    return sum(g.weight for g in v.groups if "ForeArm" in groups[g.group] or "Hand" in groups[g.group])
# Hem vertices that went up with the forearms when the rig was re-rested from A-pose to T-pose: weighted to a
# forearm or hand but sitting below the arms. They are pulled back into the surface between their good
# neighbours, and take those neighbours' weights.
bad = {v.index for v in me.vertices if arm_weight(v) > 0.5 and v.co.z < 1.33}
neigh = {i: set() for i in range(len(me.vertices))}
for e in me.edges:
    a, b = e.vertices
    neigh[a].add(b); neigh[b].add(a)
for _ in range(40):
    for i in bad:
        good = [n for n in neigh[i]]
        if good:
            me.vertices[i].co = sum((me.vertices[n].co for n in good), me.vertices[i].co * 0) / len(good)
for i in bad:
    good = [n for n in neigh[i] if n not in bad] or list(neigh[i])
    weights = {}
    for n in good:
        for g in me.vertices[n].groups:
            weights[g.group] = weights.get(g.group, 0) + g.weight / len(good)
    for g in list(me.vertices[i].groups):
        if not groups[g.group].startswith("Delete."):
            suit.vertex_groups[g.group].remove([i])
    for gi, w in weights.items():
        if not groups[gi].startswith("Delete.") and w > 0.01:
            suit.vertex_groups[gi].add([i], w, 'REPLACE')
fixed = len(bad)

bpy.ops.object.select_all(action="DESELECT")
rig = bpy.data.objects["Homesteader_Rig"]
rig.select_set(True)
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith("Homesteader"):
        o.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True,
    object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', path_mode='STRIP', mesh_smooth_type='FACE', primary_bone_axis='Y', secondary_bone_axis='X')
bpy.ops.wm.save_mainfile()
print("EXPORTED hem vertices fixed: %d" % fixed)
