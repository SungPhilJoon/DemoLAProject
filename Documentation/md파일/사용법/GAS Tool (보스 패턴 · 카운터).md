# GAS Tool 사용법 (보스 패턴 · 카운터)

메뉴: **DemoLA → Tools → GAS Tool** (또는 Window → DemoLA → GAS Tool)
코드: `Assets/_Project/Scripts/Editor/Tools/GasTool/` — ActionRPGProject 의 GAS Tool(`Assets/Game/Editor/Tools/GasTool`)을 가져와 DemoLA 데이터에 연결한 것

어빌리티를 **무엇을 하는지**(GAS Tool)와 **언제 쓰는지**(StateTree Editor)로 나눠 만든다. 원본과 같은 방식이다.

| 할 일 | 툴 |
|---|---|
| 패턴의 타격 · 장판 · 피해 · **어느 타격이 카운터 가능한지** · 대상 규칙 · 쿨다운 | **GAS Tool** — Ability 탭 |
| GameplayEffect (버프 · 디버프 · 무적 등) | GAS Tool — GameplayEffect 탭 |
| 보스가 가진 패턴 · 페이즈별 랜덤 후보 | GAS Tool — Ability Set 탭 |
| 언제 · 누구에게 어떤 패턴을 쓰는지 | **StateTree Editor** (`UsePatternTask` · `SelectTargetTask` · `TargetRuleCondition`) |

---

## 1. 원본 GAS Tool 과의 대응

| 원본 (ActionRPG) | DemoLA |
|---|---|
| AbilityGraphAsset + AbilityTable 행 | `BossPatternDefinition` 에셋 (보스 패턴 = GameplayAbility 기본값) · `SkillDefinition` (플레이어 스킬) |
| ActivateAbility 단계 (그래프 Steps) | 패턴 단계 `Steps` — 단계 하나 = [대기 → 전조 장판 → 판정] |
| EffectTable 행 | `GameplayEffectAsset` |
| Ability Set (CharacterTable.Abilities) | `BossDefinition.Patterns` + 페이즈 목록 · `CharacterClassDefinition.Skills` |
| CSV 작업 사본 → 저장 | 에셋에 바로 기록 (Undo 가능). **저장** = `AssetDatabase.SaveAssets` |
| StateTree Tool 몬스터 패널 → GAS Tool | StateTree Editor 의 패턴 ID 칸 **GAS** 버튼 · 정보 패널의 **GAS** / **Ability Set** 버튼 |

---

## 2. Ability 탭 — 보스 패턴 편집

왼쪽 목록에서 패턴을 고른다. 목록에서 ◆ 는 카운터 가능한 타격이 있는 패턴이고, 흐린 이름은 아직 구현되지 않은 패턴이다.
오른쪽은 언리얼 Details 패널 순서로 보여 준다.

| 섹션 | 내용 |
|---|---|
| 기본 | 이름 · 분류 · 대응 (CSV 원본이라 읽기 전용) · 쿨다운 · 랜덤 가중치 · AI 사용 거리 · 랜덤 선택 대상 · 후딜 |
| Tags (발동 규칙) | Ability Tags · Block · Activation Owned (`State_SuperArmor`, `Boss_Untargetable` 은 체크로 켜고 끔) · Activation Blocked · 카운터 구간 태그 |
| 대상 | 기본 대상 규칙 (StateTree 가 지정하지 않을 때). 자세한 내용: [패턴 대상 선택 (어그로 규칙).md](패턴%20대상%20선택%20(어그로%20규칙).md) |
| 카운터 (패턴 공통) | 성공 시 그로기 · 연속 회차 · 실패당 피해 증가 |
| **ActivateAbility 단계** | 타격 목록. 줄마다 ▲▼ ⧉(복제) ✕ · **◆ 카운터** 토글 · 펼치면 모든 필드 |
| 타임라인 | 전조(주황) · 판정(빨강) · 카운터 구간(파랑) · 종료(흰색) |
| 무력화 체크 | 게이지 · 제한 시간 · 실패 결과 |
| 사용처 | 이 패턴을 가진 보스 · 페이즈, 이 패턴을 쓰는 StateTree 상태 → **StateTree Editor** 버튼. 플레이 중에는 **지금 실행** 버튼 |

### 2-1. 다단 공격에서 일부 타격만 카운터 가능하게 하기

1. 패턴을 연다. 예: `G2-24 도끼 앞뒤 찍기 → 카운터 강타`
2. **ActivateAbility 단계** 에서 카운터를 허용할 타격 줄의 **◆ 카운터** 를 켠다. 나머지 타격은 꺼 둔다.
3. 켠 타격 아래에 **카운터 구간** 슬라이더가 생긴다. 구간은 **그 타격의 전조(장판) 시작 기준** 초다.
   - 경고 `판정 뒤까지 열려 있음` = 구간 끝이 전조 시간보다 길다. 맞은 뒤에도 카운터할 수 있게 된다.
4. 타임라인에서 파란 구간이 원하는 타격 위에 있는지 확인한다.

게임에서는 그 타격의 구간 동안 보스에게 `Boss_CounterWindow` 태그가 붙는다. 그동안 다음이 함께 일어난다.
- 보스 몸이 파랗게 빛난다.
- HUD 에 `◆ 카운터 가능 ◆` 이 뜬다.
- 카운터 스킬이나 AI 의 카운터가 통한다.

카운터에 성공하면 패턴이 취소되고 **성공 시 그로기** 만큼 무력화된다.
구간을 놓치면 실패 횟수가 1 늘고, 이후 타격의 피해가 **실패당 피해 +** 만큼 커진다.

> 이전 방식(패턴 하나에 구간 하나, 회차 시작 기준)은 단계 카운터가 하나도 없을 때만 쓰인다. G2-07 · G2-08 은 단계 카운터로 옮겼다.

### 2-2. 단계 추가

**+ 단계** 메뉴에서 템플릿을 고른다.

| 메뉴 | 동작 |
|---|---|
| 공격/앞 찍기 | 정면 원형, 앞쪽 찍은 곳 (`ForwardOffset` +4.5m) |
| 공격/뒤 찍기 | 뒤쪽 원형 (`ForwardOffset` −4.5m) |
| 공격/주변 원형 | 보스 중심 원형 |
| 공격/부채꼴 | 어그로 방향 부채꼴 |
| 공격/대상 발밑 원형 | 대상 발밑, 추적 장판 |
| 공격/돌진 | 직선 돌진 |
| 카운터/앞 강타 | 카운터 가능한 앞 강타 |

`ForwardOffset` 은 판정 원점을 정면(양수) · 뒤(음수)로 옮긴다. 도끼를 찍은 곳에 범위가 생기는 공격에 쓴다.

### 2-3. 새 패턴 (+ 새 Ability…)

보스 · 템플릿 · 패턴 ID(기본 `G2-C01` 형식) · 이름 · 랜덤 후보 페이즈를 고르고 **만들기** 를 누른다.

| 템플릿 | 내용 |
|---|---|
| 빈 패턴 | 단계 없음 |
| 내려찍기 1회 | 앞 찍기 하나 |
| 앞 찍기 → 뒤 찍기 → 앞 강타(카운터) | 마지막 타격만 카운터 가능 |
| 돌진 | 직선 돌진 |
| 카운터 강타 1회 | 카운터 가능한 강타 하나 |

에셋은 `Data/Authored/Boss/Patterns` 에 만들어진다. 이 폴더는 전체 셋업이 지우지 않으며, 셋업 때 보스와 자기 페이즈 목록에 다시 들어간다.

---

## 3. GameplayEffect 탭

GE 를 Duration · Modifiers/Execution · Tags · Stacking/Cue 순서로 편집한다.
**새 GameplayEffect** 프리셋: 피해 증가 버프 · 보호막 표시 · 무적 · 받는 피해 증가 디버프 · 빈 GE. 만들면 `Data/Authored/Effects` 에 저장된다.
흐린 이름은 `Data/Generated` 의 GE 로, 전체 셋업을 하면 코드 값으로 돌아간다.

## 4. Ability Set 탭

- **보스**: 패턴 × 페이즈 표. 칸을 체크하면 그 페이즈에서 랜덤 패턴 공격의 후보가 된다. 패턴 이름을 누르면 Ability 탭으로 간다. GAS Tool 로 만든 패턴은 ✕ 로 지울 수 있다.
- **직업**: 스킬 목록 (쿨다운 · 마나). **Ability** 버튼을 누르면 그 스킬의 전체 필드를 편집한다.

---

## 5. StateTree Editor 와 오가기

- `UsePatternTask` · `PatternReadyCondition` 의 **Pattern Id** 칸은 모든 보스 패턴의 드롭다운이다 (◆ = 카운터).
  오른쪽 **GAS** 버튼을 누르면 GAS Tool 에서 그 패턴이 열린다.
- 트리 위 정보 패널에 이 트리를 쓰는 보스가 나온다. **GAS Tool: Ability Set** 버튼과, 트리가 직접 쓰는 패턴마다 요약(타격 수 · ◆ 수 · 대상 규칙)과 **GAS** 버튼이 있다.
- 블록 메뉴에 두 블록을 추가했다.
  - `대상 지정 → 패턴 (SelectTargetTask + UsePatternTask)`
  - `원거리 견제 (가장 먼 대상 ≥ 8m → 돌진)`

---

## 6. 데이터를 다시 만들 때

GAS Tool 에서 패턴을 고치면 `ToolEdited` 가 켜진다. 이후 실행 데이터는 그 에셋이 원본이 된다.

| 작업 | GAS Tool 에서 고친 값 |
|---|---|
| **DemoLA → Data → 보스 패턴 테이블 → 패턴 에셋 갱신** | ✅ 유지. CSV 메타데이터(이름 · 분류 · 대응 · 메모)만 갱신 |
| **전체 셋업 / `setup overwrite`** | ✅ 유지. 에셋을 복사해 두었다가 새 에셋에 덮고 CSV 메타데이터만 다시 넣는다 |
| GAS Tool 로 만든 패턴 (`Data/Authored`) | ✅ 셋업이 지우지 않고 보스 · 페이즈 목록에 다시 넣는다 |
| 페이즈 목록 체크 (Ability Set) | ⚠️ CSV 패턴은 셋업 시 `BossPhaseTable.csv` 값으로 돌아간다. 계속 쓸 거면 CSV 에도 넣는다 |
| 플레이어 스킬 · `Data/Generated` GE | ⚠️ 셋업 시 코드(`DefaultContent`) 값으로 돌아간다 |
