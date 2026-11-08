---
description: 기존 md 문서 · git · 에디터 상태로 프로젝트 history 를 파악한 뒤 작업을 이어서 진행한다
argument-hint: [이어서 할 작업 (비우면 현황 요약만)]
---

# 프로젝트 history 파악 후 작업 진행

이 프로젝트(DemoLA, Unity 6)는 이전 대화의 내용이 **문서(`Documentation/md파일`)와 코드에만** 남아 있다.
새 세션에서는 아래 순서로 history 를 먼저 파악하고, 그 다음에 요청 작업을 진행한다.

## 1. 문서 읽기 (전부 읽는다 — 요약본만 보고 넘어가지 않는다)

`Documentation/md파일` 아래 md 파일을 Glob 으로 모두 찾은 뒤 이 순서로 읽는다.

1. `프로젝트 설정/프로젝트 초기 설정.md`, `프로젝트 설정/프로젝트 개발 사항.md` — 사용자의 원래 요구 사항 (수정하지 않는다)
2. `구현 기록/구현 현황.md` — 현재 상태 · 키 배치 · 어셈블리 · 폴더 · **차수별 변경 사항** (가장 최근 차수가 마지막)
3. `구현 기록/개발 워크플로우 (CLI · CI).md` — `Tools/dev.sh` 사용법 · 주의점 · 데이터 기반 시나리오 테스트 · 프레임워크 이전 검토
4. `구현 기록/구현 보류 사항.md` (1차 원본), `구현 기록/구현 보류 사항 답변 반영 (2차).md`, `구현 기록/구현 기록 답변/*.md` — 결정 이력
5. `구현 기록/최적화 고려 사항.md` — 성능상 알려진 문제

`프로젝트 설정/pattern_analysis_valtan.xlsx` 는 필요할 때만 본다 (보스 패턴 원본, CSV 는 `Assets/_Project/Data/CSV`).

## 2. 코드 · 저장소 상태 확인

- `git status --short`, `git log --oneline -10` — 마지막 문서 이후 바뀐 파일이 있는지. (`Assets/_Project`, `Documentation` 이 아직 커밋되지 않았을 수 있다 — 그렇다면 요약에 알린다)
- 문서에 적힌 핵심 파일이 실제로 있는지 몇 개만 확인 (예: `Assets/_Project/Scripts/Runtime/Core/GameBootstrap.cs`, `Assets/_Project/Scripts/FrameworkCandidates/Testing/ScenarioRunner.cs`)
- Unity 에디터가 열려 있으면 `bash Tools/dev.sh status`, 이어서 `bash Tools/dev.sh compile` 로 현재 컴파일 상태 확인 (에디터가 없으면 건너뛰고 그렇다고 적는다)

## 3. 요약을 사용자에게 보여 준다

다음을 짧게 정리한다 (한국어).

- 현재 구현 상태 (가장 최근 차수의 변경 요약)
- **결정을 기다리는 항목** (문서에서 "결정 대기" · "결정 필요" · ⏳ 로 표시된 것)
- 지켜야 할 작업 원칙:
  - 원작(로스트아크)의 스킬 · 각인 수치 · 맵 · UI 화면을 그대로 복제하지 않는다 — 시스템은 구현하고 내용/수치는 프로젝트 고유로 채운다 (`구현 현황.md` 3차 머리말 · `구현 보류 사항 답변 반영 (2차).md` 참고)
  - 데이터 원본은 CSV · `DefaultContent` · `PatternPresets`. `Tools/dev.sh setup overwrite` 는 `Data/Generated` 와 `Raid.unity` 를 다시 만든다
  - 기능을 바꾸면 해당 시나리오 에셋/테스트도 확인한다 (`Tools/dev.sh test playmode cat:분류`, `Tools/dev.sh test editor`)
- 문서와 실제 코드가 어긋난 곳이 있으면 그 내용

## 4. 작업 진행

요청 작업: $ARGUMENTS

- 요청이 비어 있으면 3단계 요약까지만 하고 다음 할 일을 묻는다.
- 요청이 있으면 위에서 파악한 구조 · 원칙을 따라 진행한다. 컴파일(`Tools/dev.sh compile`)과 관련 테스트 · 플레이 확인까지 한다.

## 5. 끝나면 history 를 문서에 남긴다

다음 세션이 이어받을 수 있도록 작업이 끝나면 반드시 갱신한다.

- `구현 기록/구현 현황.md` 에 새 차수 섹션(`# N차 변경 사항 (날짜)`)을 추가하거나 진행 중인 차수에 이어 쓴다: 무엇을 바꿨는지 · 주요 코드 · 확인한 결과(테스트 개수 · 통과 여부) · 결정 대기 항목
- 개발 흐름 · 도구가 바뀌었으면 `개발 워크플로우 (CLI · CI).md`, 성능 주의점이 생겼으면 `최적화 고려 사항.md`
- 사용자 원래 요구 문서(`프로젝트 설정/*.md`)는 수정하지 않는다
