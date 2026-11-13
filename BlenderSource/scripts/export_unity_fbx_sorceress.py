import bpy, os, shutil
import numpy as np
# Export like Boss_Minotaur: Sorceress_Skeleton (DEF-only, root DEF-spine) + meshes -> Assets/Models/DarkSorceress.fbx,
# textures -> Assets/Models/Textures/T_<Part>_{BaseColor,MetallicSmoothness,Normal[,Emission]}.png (flat).
PROJECT = r"E:\PhilJoon\UnityProject\DemoLAProject"
FBX_OUT = os.path.join(PROJECT, "Assets", "Models", "DarkSorceress.fbx")
TEX_OUT = os.path.join(PROJECT, "Assets", "Models", "Textures")
RIG, SKELETON = "Sorceress_rig", "Sorceress_Skeleton"
MESHES = ["Sorceress_Body", "Sorceress_Armor", "Sorceress_Hair", "Sorceress_Staff"]
PARTS = ["SorceressBody", "SorceressArmor", "SorceressHair", "SorceressStaff"]

ctx = bpy.context
if ctx.object and ctx.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
rig = bpy.data.objects[RIG]; sk = bpy.data.objects[SKELETON]
rig.data.pose_position = 'REST'; sk.hide_set(False); sk.hide_viewport = False
dups, srcs = [], []
for n in MESHES:
    src = bpy.data.objects[n]; d = src.copy(); d.data = src.data.copy()
    ctx.scene.collection.objects.link(d); d.parent = sk; d.matrix_world = src.matrix_world
    for m in d.modifiers:
        if m.type == 'ARMATURE': m.object = sk
    src.name = n + "_SRC"; d.name = n; srcs.append(src); dups.append(d)
ctx.view_layer.update()
bpy.ops.object.select_all(action='DESELECT'); sk.select_set(True)
for d in dups: d.select_set(True)
ctx.view_layer.objects.active = sk
try:
    bpy.ops.export_scene.fbx(filepath=FBX_OUT, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=True,
        use_armature_deform_only=False, add_leaf_bones=False, armature_nodetype='NULL',
        bake_anim=False, path_mode='STRIP', embed_textures=False)
finally:
    for d in dups: bpy.data.objects.remove(d, do_unlink=True)
    for s, n in zip(srcs, MESHES): s.name = n
    rig.data.pose_position = 'POSE'; sk.hide_set(True)

# Unity textures (BaseColor, Normal, Emission copied; MetallicSmoothness = RGB metallic, A = 1 - roughness)
os.makedirs(TEX_OUT, exist_ok=True)
for p in PARTS:
    for key in ("BaseColor", "Normal", "Emission"):
        img = bpy.data.images.get(f"T_{p}_{key}")
        if img is None: continue
        shutil.copy2(bpy.path.abspath(img.filepath), os.path.join(TEX_OUT, f"T_{p}_{key}.png"))
    m = bpy.data.images[f"T_{p}_Metallic"]; r = bpy.data.images[f"T_{p}_Roughness"]; W, H = m.size
    a = np.empty(W*H*4, np.float32); m.pixels.foreach_get(a); a = a.reshape(-1, 4)
    b = np.empty(W*H*4, np.float32); r.pixels.foreach_get(b); b = b.reshape(-1, 4)
    out = np.zeros_like(a); out[:, 0] = out[:, 1] = out[:, 2] = a[:, 0]; out[:, 3] = 1 - b[:, 0]
    tmp = bpy.data.images.new("_ms_tmp", W, H, alpha=True); tmp.colorspace_settings.name = 'Non-Color'
    tmp.pixels.foreach_set(out.ravel()); tmp.filepath_raw = os.path.join(TEX_OUT, f"T_{p}_MetallicSmoothness.png")
    tmp.file_format = 'PNG'; tmp.save(); bpy.data.images.remove(tmp)
print("exported", FBX_OUT)
