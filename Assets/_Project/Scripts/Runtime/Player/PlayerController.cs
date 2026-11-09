using System;
using DemoLA.Character;
using DemoLA.Combat;
using Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DemoLA.Player
{
    /// <summary>
    /// 플레이어 조작 (InputActionAsset — InputBindings).
    /// PC: 우클릭 이동 · QWERASDF 스킬 · V 각성기 · Z 아이덴티티 · Space 회피 · 1~4 배틀 아이템 · 좌클릭 대상 지정 · 휠 줌 · P 빌드 창 · F8 딜 미터.
    /// 모바일: MobileControls 의 가상 패드가 같은 게임패드 경로를 누르고, 탭으로 대상을 지정한다.
    /// 대상이 지정되어 있으면 스킬 조준점 = 대상 위치, 아니면 마우스 아래 바닥(모바일은 정면).
    /// </summary>
    public class PlayerController : BaseBehaviour
    {
        private const float PickRadiusPixels = 90.0f;

        private PartyCharacter character;
        private Camera cachedCamera;
        private InputBindings bindings;
        private InputAction move;
        private InputAction stick;
        private InputAction point;
        private InputAction select;
        private InputAction dodge;
        private InputAction awakening;
        private InputAction identity;
        /// <summary>이번 프레임 마우스가 UI 위에 있는가 (UI 클릭을 이동 · 대상 선택으로 쓰지 않는다).</summary>
        private bool pointerOverUI;
        private InputAction identity2;
        private InputAction buildWindow;
        private InputAction dpsMeter;
        private InputAction zoom;
        private readonly InputAction[] skills = new InputAction[PartyCharacter.SkillSlotCount];
        private readonly InputAction[] items = new InputAction[PartyCharacter.BattleItemSlotCount];
        private readonly InputAction[] esthers = new InputAction[3];
        private float repathTimer;

        public static PlayerController Current { get; private set; }

        /// <summary>UI 창이 열려 있어 게임 입력을 막아야 하는가 (빌드 창이 설정한다).</summary>
        public static Func<bool> IsInputBlocked;
        public static event Action OnBuildWindowToggle;
        public static event Action OnDpsMeterToggle;
        /// <summary>마우스 휠 / 핀치 줌 (양수 = 확대).</summary>
        public static event Action<float> OnZoom;

        public PartyCharacter Character
        {
            get { return character; }
        }

        /// <summary>입력 → 행동 경로 (오프라인 = 바로 실행, 네트워크 = 서버 RPC).</summary>
        public IPartyCommands Commands { get; set; }

        /// <summary>지정한 대상 (없으면 null).</summary>
        public CombatActor Target { get; private set; }

        public InputBindings Bindings
        {
            get { return bindings; }
        }

        public void Setup(InputActionAsset InAsset)
        {
            bindings?.Disable();
            bindings = new InputBindings(InAsset);
            move = bindings["Move"];
            stick = bindings["Stick"];
            point = bindings["Point"];
            select = bindings["Select"];
            dodge = bindings["Dodge"];
            awakening = bindings["Awakening"];
            identity = bindings["Identity"];
            identity2 = bindings["Identity2"];
            buildWindow = bindings["BuildWindow"];
            dpsMeter = bindings["DpsMeter"];
            zoom = bindings["Zoom"];
            for (int i = 0; i < skills.Length; ++i)
            {
                skills[i] = bindings[$"Skill{i + 1}"];
            }

            for (int i = 0; i < items.Length; ++i)
            {
                items[i] = bindings[$"Item{i + 1}"];
            }

            for (int i = 0; i < esthers.Length; ++i)
            {
                esthers[i] = bindings[$"Esther{i + 1}"];
            }

            if (isActiveAndEnabled == true)
            {
                bindings.Enable();
            }
        }

        public void Bind(PartyCharacter InCharacter)
        {
            character = InCharacter;
            character.IsPlayerControlled = true;
            Target = null;
            Commands = new LocalPartyCommands(InCharacter);
            Current = this;
        }

        public void SetTarget(CombatActor InTarget)
        {
            Target = InTarget;
        }

        private void OnEnable()
        {
            bindings?.Enable();
        }

        private void OnDisable()
        {
            bindings?.Disable();
        }

        protected override void OnUnityDestroy()
        {
            bindings?.Disable();
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Update()
        {
            if (bindings == null)
            {
                return;
            }

            if (buildWindow.WasPressedThisFrame() == true)
            {
                OnBuildWindowToggle?.Invoke();
            }

            if (dpsMeter.WasPressedThisFrame() == true)
            {
                OnDpsMeterToggle?.Invoke();
            }

            float scroll = zoom.ReadValue<float>();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                OnZoom?.Invoke(Mathf.Sign(scroll));
            }

            if (character == null || character.IsDead == true)
            {
                return;
            }

            if (IsInputBlocked != null && IsInputBlocked() == true)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (Target != null && (Target.IsDead == true || Target.IsTargetable == false))
            {
                Target = null;
            }

            // HUD 칸(스킬 · 배틀 아이템)을 누를 때는 대상 선택 · 이동을 하지 않는다.
            pointerOverUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (select.WasPressedThisFrame() == true && pointerOverUI == false)
            {
                Target = PickTarget(point.ReadValue<Vector2>());
            }

            var aim = AimPoint();
            if (dodge.WasPressedThisFrame() == true)
            {
                Commands.Dodge(Target != null ? character.Position + (character.Position - Target.Position) : aim);
                return;
            }

            for (int i = 0; i < skills.Length; ++i)
            {
                if (skills[i].WasPressedThisFrame() == true)
                {
                    Commands.UseSkill(i, aim);
                    return;
                }
            }

            if (awakening.WasPressedThisFrame() == true)
            {
                Commands.UseAwakening(aim);
                return;
            }

            if (identity.WasPressedThisFrame() == true)
            {
                Commands.UseIdentity(aim);
                return;
            }

            if (identity2.WasPressedThisFrame() == true)
            {
                Commands.UseIdentityX(aim);
                return;
            }

            for (int i = 0; i < items.Length; ++i)
            {
                if (items[i].WasPressedThisFrame() == true)
                {
                    Commands.UseBattleItem(i, aim);
                    return;
                }
            }

            for (int i = 0; i < esthers.Length; ++i)
            {
                if (esthers[i] != null && esthers[i].WasPressedThisFrame() == true)
                {
                    Commands.UseEsther(i);
                    return;
                }
            }

            handleMovement(aim);
        }

        /// <summary>화면 좌표에서 가장 가까운 적대 액터 (InScreen 반경 PickRadiusPixels 안). 없으면 null.</summary>
        public CombatActor PickTarget(Vector2 InScreen)
        {
            if (cachedCamera == null || character == null)
            {
                return null;
            }

            CombatActor best = null;
            float bestDistance = PickRadiusPixels;
            var actors = CombatRegistry.All;
            for (int i = 0; i < actors.Count; ++i)
            {
                var actor = actors[i];
                if (actor == null || actor.IsDead == true || actor.IsTargetable == false || CombatRegistry.IsHostile(character.Team, actor.Team) == false)
                {
                    continue;
                }

                var screen = cachedCamera.WorldToScreenPoint(actor.Position + Vector3.up);
                if (screen.z < 0.0f)
                {
                    continue;
                }

                float distance = Vector2.Distance(InScreen, new Vector2(screen.x, screen.y)) - actor.Radius * 20.0f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = actor;
                }
            }

            return best;
        }

        private void handleMovement(Vector3 InAim)
        {
            if (character.Motor == null)
            {
                return;
            }

            var value = stick.ReadValue<Vector2>();
            if (value.sqrMagnitude > 0.04f)
            {
                var forward = cameraForward();
                var right = new Vector3(forward.z, 0.0f, -forward.x);
                var direction = forward * value.y + right * value.x;
                Commands.MoveTo(character.Position + direction.normalized * 2.0f);
                if (direction.sqrMagnitude > 0.01f && Target == null)
                {
                    character.FaceTowards(character.Position + direction);
                }

                return;
            }

            if (move.IsPressed() == false)
            {
                return;
            }

            if (move.WasPressedThisFrame() == true && pointerOverUI == true)
            {
                return;
            }

            repathTimer -= Time.deltaTime;
            if (move.WasPressedThisFrame() == true || repathTimer <= 0.0f)
            {
                repathTimer = 0.08f;
                Commands.MoveTo(mouseGround());
            }
        }

        /// <summary>스킬 조준점: 대상 &gt; 마우스 바닥 &gt; 정면.</summary>
        public Vector3 AimPoint()
        {
            if (Target != null)
            {
                return Target.Position;
            }

            return Mouse.current != null ? mouseGround() : character.Position + character.Forward * 3.0f;
        }

        /// <summary>포인터 아래 바닥(y = 0 평면). 물리 레이캐스트를 쓰지 않아 콜라이더 설정과 무관하다.</summary>
        private Vector3 mouseGround()
        {
            if (cachedCamera == null)
            {
                return character.Position + character.Forward * 3.0f;
            }

            var ray = cachedCamera.ScreenPointToRay(point.ReadValue<Vector2>());
            var plane = new Plane(Vector3.up, Vector3.zero);
            float distance;
            return plane.Raycast(ray, out distance) == true ? ray.GetPoint(distance) : character.Position + character.Forward * 3.0f;
        }

        private Vector3 cameraForward()
        {
            if (cachedCamera == null)
            {
                return Vector3.forward;
            }

            var forward = HitGeometry.Flat(cachedCamera.transform.forward);
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }
    }
}
