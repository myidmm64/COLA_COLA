# 열린 Unity 에디터에서 Play 검증

**검증은 사용자가 명시적으로 요청한 경우에만 에이전트가 실행한다.** 일반 수정 후에는 사용자가 직접 확인한다. 에이전트가 자동으로 Play에 진입하거나 검증 요청·배치 실행·테스트/컴파일 검사 명령을 보내지 않는다. 아래 메뉴와 명령은 사용자가 직접 실행하거나 검증을 요청했을 때 사용한다.

에디터를 종료하거나 프로젝트를 복사할 필요가 없다. `ProjectVerificationBridge`가 현재 프로젝트의 파일 요청을 읽고 코드·에셋 변경을 가져온 뒤 실제 Play 검증을 실행한다. 마우스·키보드나 프리뷰 렌더를 사용하지 않는다.

## 실행

Unity 메뉴 `COLA_COLA > Validation > Run Full Play Verification` 또는 프로젝트 루트 PowerShell:

```powershell
.\Tools\Request-UnityVerification.ps1 -Suite full -Wait
```

`music`, `burst`, `orders`, `items`, `duel`을 지정하면 해당 검사와 관련 회귀 검사만 실행한다. 전체 검사는 BGM → 주문서 → 보스 아이템 → 폭발·페이드 → 플레이어 아이템 → 기본 대전 → 메인 씬 순서다. 이미 검증 중이면 두 번째 요청을 거절한다. Unity가 닫혀 있으면 원본 프로젝트를 연 뒤 요청을 보낸다.

에이전트는 `Library/COLA_COLA/verification-request.json`에 `{"id":"고유한 요청 ID","suite":"full"}`을 기록할 수도 있다. 브리지는 정해진 검사 이름만 받아들이며 임의 코드·셸 명령을 실행하지 않는다. 요청은 처리 후 삭제한다. `status.json`의 요청 ID와 에디터 PID를 대조해 이번 결과인지 확인한다.

## 작업 보존

- 검증 요청 시 재생 중인 Play는 종료하고 검사 전용 Play로 진입한다. 검증 후에는 Edit 모드로 돌아온다.
- 저장하지 않은 씬이나 경로 없는 새 씬은 변경을 버리지 않고 `waiting_for_saved_scenes` 상태로 기다린다. 저장하면 자동으로 이어진다. Prefab/씬 작업은 저장 후 검증하는 것을 권장한다.
- 검사 전 열린 씬 목록과 활성 씬을 기록하고 종료 후 복원한다. 에디터를 닫지 않는다.
- 메뉴 `COLA_COLA > Validation > Cancel Verification`으로 중단한다. 실패·취소는 Play를 종료하고 요약에 이유를 기록한다.
- 최초 브리지 설치 또는 브리지 자체에 컴파일 오류가 있으면 정상 컴파일이 먼저 필요하다. 브리지가 설치된 후의 검증 요청은 열린 에디터에서 `AssetDatabase.Refresh`를 실행하므로 창에 포커스를 줄 필요가 없다.

## 결과 위치

`output/verification/latest/` 한 곳에 유지한다.

| 파일·폴더 | 내용 |
|---|---|
| `Summary.md` | 마지막 요청의 통과·실패, 실행한 검사, 에디터 PID |
| `Full-suite.md` | 마지막 전체 검사 요약. 이후 부분 검사를 실행해도 유지 |
| `status.json` | 요청 ID, 진행 상태, 실행 시각과 에디터 정보 |
| `verification.log` | 이번 요청의 Unity 로그와 오류 |
| `music/`, `order-sheet/`, `boss-items/`, `cola-burst/`, `cola-items/`, `playable-duel/`, `scene-layout/` | 검사별 최신 결과 |
| `unity-editor.log` | 에이전트가 에디터를 실행했을 때의 최신 엔진 진단 로그 |

이전 실행의 날짜별 복사본을 쌓지 않고 같은 경로를 갱신한다. 특정 검사만 실행하면 다른 검사 보고서는 이전 시각일 수 있으므로 **이번 실행의 범위는 Summary와 요청 ID를 기준**으로 판단한다. 자동 상태·Raycast·PCM 검사는 실제 Game 뷰의 시각 검수나 음악 청취를 대신하지 않는다.
