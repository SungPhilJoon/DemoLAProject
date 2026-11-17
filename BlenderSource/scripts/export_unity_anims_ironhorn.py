"""
IronhornWarlord(2관문 보스) 애니메이션 → Unity.
ironhorn_warlord.blend 의 Action 을 하나씩 IronhornWarlord_Skeleton 에 걸어 스켈레톤만(메시 없이) 구워 내보낸다.
  출력: Assets/_Project/Animation/Boss/Gate2/IronhornWarlord/IronhornWarlord@<Action>.fbx
  축 · 단위는 export_unity_fbx.py(모델)와 같다 → Unity 에서 모델 아바타(IronhornWarlord.fbx)를 Copy From Other 로 쓴다.
실행: blender -b BlenderSource/ironhorn_warlord.blend --python BlenderSource/scripts/export_unity_anims_ironhorn.py
"""
import os
import re

import bpy

PROJECT = r"E:\PhilJoon\UnityProject\DemoLAProject"
OUT_DIR = os.path.join(PROJECT, "Assets", "_Project", "Animation", "Boss", "Gate2", "IronhornWarlord")
SKELETON = "IronhornWarlord_Skeleton"
PREFIX = "IronhornWarlord@"


def clean_name(InName):
    """파일 이름: 영문 · 숫자 · _ 만 (공백 → _)."""
    name = re.sub(r"[^A-Za-z0-9_]+", "_", InName).strip("_")
    return name or "Action"


def main():
    ctx = bpy.context
    if ctx.object and ctx.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    sk = bpy.data.objects[SKELETON]
    sk.hide_set(False)
    sk.hide_viewport = False
    ad = sk.animation_data or sk.animation_data_create()
    saved_action = ad.action
    saved_tracks = [(t, t.mute) for t in ad.nla_tracks]
    saved_range = (ctx.scene.frame_start, ctx.scene.frame_end)
    for t, _ in saved_tracks:
        t.mute = True  # NLA 가 다른 Action 을 섞지 않게

    os.makedirs(OUT_DIR, exist_ok=True)
    exported = []
    try:
        for action in list(bpy.data.actions):
            # 이 스켈레톤 뼈를 움직이는 Action 만 (다른 오브젝트용 Action 제외)
            ad.action = action
            if action.slots if hasattr(action, "slots") else False:
                slot = next((s for s in action.slots if s.target_id_type == 'OBJECT'), None)
                if slot is not None:
                    ad.action_slot = slot
            start, end = action.frame_range
            ctx.scene.frame_start = int(round(start))
            ctx.scene.frame_end = max(int(round(end)), int(round(start)) + 1)

            bpy.ops.object.select_all(action='DESELECT')
            sk.select_set(True)
            ctx.view_layer.objects.active = sk
            path = os.path.join(OUT_DIR, f"{PREFIX}{clean_name(action.name)}.fbx")
            bpy.ops.export_scene.fbx(
                filepath=path, use_selection=True, object_types={'ARMATURE'},
                apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                use_armature_deform_only=False, add_leaf_bones=False, armature_nodetype='NULL',
                bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP')
            exported.append(f"{action.name} [{ctx.scene.frame_start}-{ctx.scene.frame_end}] -> {os.path.basename(path)}")
    finally:
        ad.action = saved_action
        for t, mute in saved_tracks:
            t.mute = mute
        ctx.scene.frame_start, ctx.scene.frame_end = saved_range
        sk.hide_set(True)

    for line in exported:
        print("EXPORTED", line)
    print(f"EXPORT_DONE {len(exported)} -> {OUT_DIR}")


main()
