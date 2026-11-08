# 개발 워크플로우 — Unity CLI · Pipeline · CI/CD

열린 Unity 에디터를 터미널에서 조작해 **컴파일 → 테스트 → 플레이 확인 → 빌드** 를 한 흐름으로 돌린다.
같은 진입점(`BuildCommand`)을 CI 가 배치 모드로 다시 쓴다.

```
[에디터 열림]  Tools/dev.sh ──unity CLI──▶ com.unity.pipeline (HTTP) ──▶ 에디터 (demola 명령 · 기본 명령)
[CI / 배치]    Tools/ci/ci.sh · GitHub Actions ──▶ Unity -batchmode -executeMethod DemoLA.Editor.Build.BuildCommand.Build
```

## 1. 준비

| 항목 | 방법 |
|---|---|
| Unity CLI | PowerShell: `$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 \| iex` — 확인: `unity --version` |
| Pipeline 패키지 | 이미 설치 (`com.unity.pipeline` 0.10.0-exp.1). 에디터를 열면 서버가 자동 시작 |
| python | 결과 JSON 파싱용 (`Tools/data/*.py`) |
| 백그라운드 실행 | `dev.sh play` 가 `editor.playmode set_run_in_background` 를 켠다 (포커스 없이도 프레임 진행) |

## 2. 개발 루프 (`Tools/dev.sh`, Windows 는 `Tools\dev.ps1`)

| 명령 | 하는 일 |
|---|---|
| `status` | 에디터 상태 (컴파일 중 · 플레이 · 코드 로드 횟수) |
| `compile` | AssetDatabase 갱신 → 컴파일 요청 → 도메인 리로드까지 대기, **이번 컴파일 오류만** 출력 (`Tools/data/editor_compile.py`) |
| `test [editor\|playmode\|all] [필터 \| cat:분류]` | Test Runner 실행. PlayMode 는 도메인 리로드 때문에 비동기 실행 후 상태 폴링. `cat:Mechanics` 처럼 쓰면 분류(Category) 필터 |
| `scenarios` | 시나리오 에셋 생성 (없는 것만) → `Assets/_Project/Tests/Scenarios` (6장 참고) |
| `setup [overwrite]` | CSV 테이블 → 데이터 에셋 → 입력 에셋 → Addressables 그룹 → 레이드 씬 (대화창 없음) |
| `play [초] [Gate1\|Gate2]` | Raid 씬 열기 → 플레이 → 대기실 준비 완료(`demola ready`) → 입장 → N초 → `Builds/DevLoop/play.png` 스크린샷 · 레이드 상태 · 딜 미터 · 콘솔 오류 → 종료 |
| `balance [직업] [초] [시행] [behind]` | 결정적 DPS 시뮬레이션 (밸런스 툴과 같은 코어) |
| `deps` | Addressables 의존성 리포트 → `DependencyReports/dependency-report.md` |
| `build [Win64\|Android] [dev]` | 열린 에디터에서 플레이어 빌드 → `Builds/build-report.json` |

프로젝트 전용 Pipeline 명령은 `unity command demola` 로 목록을 볼 수 있다
(`setup` · `import_tables` · `select_gate` · `ready` · `dps` · `raid_status` · `balance` · `deps` · `build` · `build_status`).
코드: `Assets/_Project/Scripts/Editor.Pipeline/DemoLAPipelineCommands.cs` (패키지가 없으면 이 어셈블리는 컴파일되지 않는다).

### 실제로 확인한 흐름 (2026-11-06)

```
Tools/dev.sh compile            → 컴파일 성공 (codeLoadCount 18 → 19)
Tools/dev.sh setup overwrite    → tables 8 · GameDatabase · DemoLAControls · Raid.unity
Tools/dev.sh test editor        → EditMode 통과 (아래 "테스트" 참고)
Tools/dev.sh test playmode      → PlayMode 3/3
Tools/dev.sh play 25 Gate1      → 46줄 분열 → G1-P1 → 30줄 구슬 기믹 진행, 파티 AI 딜 미터 출력, 오류 없음
```

## 3. 빌드 자동화

진입점: `DemoLA.Editor.Build.BuildCommand` (`Assets/_Project/Scripts/Editor/Build/BuildCommand.cs`)

1. 데이터/씬이 없으면 셋업, 있으면 CSV 테이블 · Addressables 등록만 갱신
2. 버전 기록 (`-versionName`, `-buildNumber` → bundleVersion / Android versionCode)
3. 빌드 타깃 전환 (필요할 때만)
4. Android 서명: `DEMOLA_KEYSTORE_PATH/PASS`, `DEMOLA_KEY_ALIAS/PASS` (없으면 디버그 서명), `-aab` 로 App Bundle
5. Addressables 콘텐츠 빌드 (로컬 번들 · 기본 카탈로그)
6. 플레이어 빌드 → `Builds/build-report.json` (결과 · 크기 · 시간 · 오류), 배치 모드 실패 시 종료 코드 1

배치 모드 인자:

```
Unity -batchmode -quit -projectPath . -buildTarget StandaloneWindows64 \
      -executeMethod DemoLA.Editor.Build.BuildCommand.Build \
      -demolaTarget Win64 -outputPath Builds/Win64 [-development] [-buildNumber 42] [-versionName 0.3.0] [-skipSetup] [-skipAddressables] [-aab]
```

### CI 스크립트

| 파일 | 용도 |
|---|---|
| `Tools/ci/ci.sh` / `ci.ps1` | 자체 호스트 러너 · 로컬 배치 모드: `test [editmode\|playmode\|all]`, `build [타깃] [dev] [번호] [버전]`, `all` |
| `.github/workflows/ci.yml` | GitHub Actions: GameCI 로 EditMode/PlayMode 테스트 → Win64 · Android 빌드 → 아티팩트. `Library` 캐시 |

GitHub Secrets: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` (+ Android 서명 선택: `DEMOLA_KEYSTORE_BASE64`, `DEMOLA_KEYSTORE_PASS`, `DEMOLA_KEY_ALIAS`, `DEMOLA_KEY_PASS`).

> 배치 모드는 같은 프로젝트를 연 에디터가 있으면 실패한다 → 로컬에서는 `dev.sh build`(열린 에디터) 를, CI 에서는 `ci.sh` 를 쓴다.

## 4. 정적 데이터 흐름

```
pattern_analysis_valtan.xlsx ── python Tools/data/xlsx_to_csv.py ──▶ Assets/_Project/Data/CSV/BossPatternTable.csv · BossPhaseTable.csv
기타 CSV (Honing/AdvancedHoning/Gem/Engraving/CardSet/BattleItem) ── 직접 편집
CSV 저장 ── (프레임워크 TableImportPostprocessor 자동 재변환) ──▶ Assets/_Project/Data/Tables/*.asset (TableAsset)
DemoLATables + GameDatabase ── Addressables 로컬 그룹 "DemoLA Data" ──▶ 빌드에 포함 (Resources 사용 안 함)
```

- 패턴 측정값: xlsx 의 전조/후딜 칸 또는 CSV 의 `TelegraphTime` / `Recovery` / `AreaScale` / `Measured` 에 값을 적으면 프리셋보다 우선 적용.
- 패턴 에셋 갱신: `unity command demola import_tables` (또는 메뉴 DemoLA/Data/보스 패턴 테이블 → 패턴 에셋 갱신).

## 5. 알려진 주의점

- 에디터가 이미 실패한 컴파일 상태면 `script recompile` 은 거부된다 → `dev.sh compile` 이 AssetDatabase 갱신 + 컴파일 요청을 같이 보낸다.
- 에디터 밖에서 파일을 지웠다 같은 이름으로 다시 만들면, 그 경로가 컴파일 목록에서 빠지는 경우가 있었다 → 파일을 다른 이름으로 한 번 갱신했다 되돌리면 복구된다.
- `demola build` 는 빌드가 끝날 때까지 에디터 메인 스레드를 잡는다 → CLI 는 30초 뒤 응답 시간 초과로 끝나지만 빌드는 계속된다. 결과는 `demola build_status` / `Builds/build-report.json`.
- `setup overwrite` 도 데이터가 늘어 30초를 넘기는 경우가 있다 → CLI 는 시간 초과로 끝나지만 에디터 안에서는 끝까지 진행된다. 20~30초 기다린 뒤 `Data/Generated` 를 확인하고, `setup`(덮어쓰기 없음)을 한 번 더 돌려 Addressables · 씬 단계를 마무리한다.
- `setup overwrite` 는 데이터 에셋과 `Raid.unity` 를 다시 만든다. `Data/Generated` 를 인스펙터에서 직접 고친 값은 사라진다 (원본은 CSV · `DefaultContent` · `PatternPresets`).
- 데이터 에셋끼리의 참조(직업 → 스킬, 보스 → 패턴 · AI)는 **참조되는 쪽부터** 만들어야 한다. 반대로 하면 fileID 0(빈 참조)으로 저장되고 `SetDirty` · 재직렬화로도 고쳐지지 않는 경우가 있었다 (`DemoLAProjectSetup.creationOrder`).
- 예전 저장 데이터(`LocalJsonSaveService`)의 빌드는 새 기본값(악세 수치 · 스킬 슬롯 등)을 자동으로 따라가지 않는다 → 능력치 창 "기본값으로" 또는 `GameSession.ResetBuild`.

## 6. 데이터 기반 시나리오 테스트 (3차)

테스트 · 밸런스 확인을 **코드가 아니라 ScriptableObject 에셋**(`TestScenarioAsset`)으로 만든다.
UnityTest 메서드는 하나(`ScenarioAssetTests.Run`)뿐이고, `Assets/_Project/Tests/Scenarios` 아래의 에셋을 모두 찾아 케이스로 돌린다 — 테스트를 늘릴 때는 에셋을 추가 · 편집한다.

```
TestScenarioAsset (SO)
 ├─ 설정: 제목 · 분류(Category) · 시간 배율 · 제한 시간 · 시드 · HostSettings(키=값)
 ├─ Steps        [SerializeReference] 순서대로 실행 (코루틴, 여러 프레임 대기 가능)
 ├─ Expectations [SerializeReference] 끝에 판정 (ReportOnly = 측정만 → 밸런스 리포트)
 └─ 콜백 UnityEvent: OnStarted / OnEventRecorded(이름, 값) / OnFinished(통과, 리포트)

ScenarioRunner.Run(asset, host, result)   ← UnityTest 에서 yield return, 게임 안에서는 StartCoroutine
   host.SetUp → (단계 실행 + 매 프레임 관측값 표본) → 검증 → 콜백 → host.TearDown

IScenarioHost (프로젝트 어댑터)
   게임 이벤트 → ScenarioRecorder.Record("이름", 값)      ← 이벤트 검증이 개수 · 합 · 최대 · 최소 · 평균 · 마지막 · 처음 시각을 본다
   RegisterProbe("이름", () => 값)                        ← 관측값 검증이 끝 값 · 실행 중 최소/최대 · 변화량을 본다
```

### 6-1. 구성 요소

| 구분 | 형식 | 설명 |
|---|---|---|
| 공통 단계 | `WaitSecondsStep` · `WaitForEventStep` · `WaitForProbeStep` · `InvokeUnityEventStep` · `LogStep` | 대기 · 이벤트/관측값 조건 대기 · 인스펙터에서 연결한 함수 호출 · 로그 |
| DemoLA 단계 | `SpawnBossStep` · `SpawnPartyStep` · `ActivatePatternStep` · `SetBossHpLinesStep` · `MoveMemberStep` · `KnockbackMemberStep` · `SetBossAIStep` | 보스(AI 끄기 가능) · 파티(더미/AI) · 패턴 즉시 발동 후 끝까지 대기(실패 시 이유 · 보스 상태 기록) · 체력 줄 · 이동 · 넉백 |
| 검증 | `EventExpectation` · `ProbeExpectation` · `EventIntervalExpectation` | 비교: = ≠ > ≥ < ≤ 범위. `OnEvaluated(float)` 에 함수를 연결하면 실제 값을 받아 추가 확인 |
| 이벤트 (DemoLA) | `Damage.ToParty` · `Damage.ToBoss` · `Hit.Party` · `Hit.PartyCrit` · `Pattern.Started(.ID)` · `Pattern.Ended(.ID)` · `Telegraph` · `Knockback` · `Structure.Broken(.Pillar/.Wall)` · `Died.Party/Boss` · `Message` | `DemoLAScenarioHost` 가 EventBus · 정적 이벤트에서 기록 |
| 관측값 (DemoLA) | `Boss.HpLines` · `Boss.HpPercent` · `Boss.Moved` · `Boss.Groggy` · `Party.Alive` · `Party.MinHpPercent` · `Party.KnockedBack` · `Party.CritRate` · `Party0.DistanceFromCenter` · `Telegraph.Visible` · `Structures.Destroyed` · `Pickups` | |
| 인스펙터 | `[SubclassSelector]` 드로어 | Steps · Expectations 원소마다 형식 드롭다운 (다른 어셈블리의 단계도 자동 표시). "+" 로 늘린 원소가 앞 원소와 참조를 공유하면 복사본으로 바꾼다 |

### 6-2. 만들어 둔 시나리오 (`DemoLA/Tests/시나리오 생성`, `dev.sh scenarios`)

| 분류 | 개수 | 내용 |
|---|---|---|
| `Pattern.Gate1` · `Pattern.Gate2` | 20 · 21 | 구현된 패턴마다 자동 생성: 보스(AI 끔) + 더미 4명 → 발동 → 끝날 때까지. 검증 = 시작 · 끝남 · (장판이 있으면) 장판 표시. 측정 = 파티 피해 · 넉백 · 취소 여부 |
| `Mechanics` | 5 | G2-23 동시 장판 5개 · 보스 이동 · 넉백 / G2-11 기둥 파괴 1 · 구슬 2 · 그로기 / G2-03 구조물 14개 파괴 / 넉백 낙사 / 넉백 없으면 구출 |
| `Balance` | 1 | AI 파티 4명 60초 (보스 AI 끔): 체력 줄 변화량 범위 · 치명타 비율 범위 + 피해 합 · 타격 수 측정 |

생성기는 **이미 있는 에셋을 건너뛴다** (직접 고친 값 보존). 덮어쓰려면 메뉴 "시나리오 다시 생성 (덮어쓰기)".

### 6-3. 실행

```
Tools/dev.sh test playmode cat:Mechanics      # 분류별
Tools/dev.sh test playmode cat:Pattern.Gate2
Tools/dev.sh test playmode                     # 전부 (시나리오 47 + 기존 3, 약 5~6분)
```

실패하면 테스트 메시지에 리포트 전체(검증별 실제 값 · 기대 · 단계 로그)가 남는다. 예:

```
=== 돌진 → 기둥 충돌 (3.4초) ===
  [통과] 기둥 파괴 (이벤트 'Structure.Broken.Pillar' Count) = 1 (기대 = 1)
  [통과] 생명 구슬 (관측값 'Pickups' MaxDuring) = 2 (기대 ≥ 2)
  [통과] 보스 그로기 (관측값 'Boss.Groggy' MaxDuring) = 1 (기대 = 1)
```

확인 결과 (2026-11-08): 시나리오 47/47 · 기존 PlayMode 3/3 · EditMode 67/67 통과.
만드는 과정에서 찾은 문제: 1관문 보스는 생성 직후 AI 가 G1-05(분열)를 먼저 써 다른 패턴이 막혔다 → AI 를 끌 때 진행 중 패턴도 취소. 변화량(Delta) 시작 값을 첫 단계 전에 읽어 NaN → "처음 유효한 표본" 으로 변경.

### 6-4. 새 시나리오 · 단계 추가

- **에셋만으로**: Project 창 → Create → Framework → Testing → Test Scenario → 단계/검증을 드롭다운으로 추가 → 분류를 정해 `Assets/_Project/Tests/Scenarios` 아래에 둔다. 다음 테스트 실행부터 자동 포함.
- **새 단계 형식**: `ScenarioStep` (DemoLA 는 `DemoLAStep`) 을 상속한 `[Serializable]` 클래스 + `IEnumerator Execute(ScenarioContext)` → 드롭다운에 자동 표시.
- **새 이벤트 / 관측값**: `DemoLAScenarioHost.subscribe` 에서 `Recorder.Record`, `registerProbes` 에서 `RegisterProbe`.
- **게임 안에서 밸런스 측정**: 같은 에셋을 `ScenarioRunner.Run` 으로 돌리면 된다 (호스트가 진행 중 레이드를 걷어 내고 끝나면 대기실로 돌려보냄). 결과 리포트는 `OnFinished(통과, 리포트)` 로 받는다.

### 6-5. 프레임워크(PhilJoon Custom_Unity_Framework) 이전 검토

| 대상 | 이전 가능 | 근거 / 할 일 |
|---|---|---|
| `Framework.Testing` (`Scripts/FrameworkCandidates/Testing`) | **그대로 가능** | 참조 어셈블리 없음(UnityEngine · UnityEvents 만). 프로젝트 타입을 모르고 `IScenarioHost` 로만 연결. 패키지 `Runtime/Testing` 에 asmdef 째 옮긴다 |
| `Framework.Testing.Editor` (형식 선택 드로어) | **그대로 가능** | `Framework.Testing` 만 참조. 패키지 `Editor/Testing` 으로. `[SubclassSelector]` 는 시나리오 외 다른 `[SerializeReference]` 목록에도 재사용 가능 |
| `ScenarioAssetTests` (NUnit 연결, 약 50줄) | 일반화 후 가능 | 지금은 프로젝트 폴더 · 호스트를 직접 쓴다. 프레임워크에는 `ScenarioTestBase<THost>`(폴더 경로 · 호스트 생성만 재정의) 로 제공 — `nunit.framework` · TestRunner 참조가 필요하므로 별도 asmdef(`Framework.Testing.Tests`) 로 분리 |
| `DemoLA.Testing` (호스트 · 단계) | 프로젝트에 남김 | 게임 타입(보스 · 파티 · 패턴)에 묶인 어댑터 |
| `ScenarioAssetFactory` | 프로젝트에 남김 | 이 프로젝트 콘텐츠로 에셋을 만드는 도구 |

이전할 때 주의:
1. **`[SerializeReference]` 는 에셋에 "어셈블리 이름 + 네임스페이스 + 클래스 이름" 을 저장한다.** 어셈블리 이름(`Framework.Testing`) · 네임스페이스(`Framework.Testing`) · 클래스 이름을 그대로 두면 기존 시나리오 에셋 47개가 깨지지 않는다. 바꿔야 한다면 `[UnityEngine.Scripting.APIUpdating.MovedFrom(...)]` 을 붙인다.
2. 패키지로 옮긴 뒤 프로젝트의 `Scripts/FrameworkCandidates/Testing` 폴더를 지우고 패키지 버전을 올린다 (같은 asmdef 이름이 두 곳에 있으면 컴파일 오류).
3. 프레임워크 `EventBus` 와 연결하는 범용 기록기(`EventBusRecorder<T>` — 구조체 필드를 이름 · 값으로 바꿔 기록)를 추가하면 프로젝트 호스트 코드가 더 줄어든다 (다음 단계 제안).
4. 시간 기반 실행은 `Time.timeScale` 을 바꾼다 → 여러 시나리오를 병렬로 돌리지 않는다 (Test Runner 는 순차 실행이라 문제 없음).
