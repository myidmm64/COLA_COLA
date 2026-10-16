# 압력 선택과 콜라 클릭 확정

현재 범위는 한 콜라의 압력 선택·예측·확정과 플레이어/보스의 기본 교대 행동이다. 피해·토큰·상점 로직은 별도이며 기존 상태 단말기의 값은 여전히 예시다.

## 플레이 순서

1. 새 콜라의 한계 압력을 마지막 눈금에서 확인한다.
2. 1·2·3 버튼을 클릭한다. 현재 압력은 그대로이고 선택 키·주황 예측 바늘·예상 폭발 확률만 바뀐다.
3. 중앙 콜라를 클릭한다. 선택한 강도를 압력에 더하고 화면에 표시했던 확률로 폭발을 한 번 판정한다.
4. 플레이어가 병을 잡고 들어 흔든 뒤 내려놓는 4.4초 동안 입력을 잠근다. 안전하면 누적 압력을 유지하고 선택을 해제한다. 폭발하면 새 콜라와 새 공개 한계로 교체한다.
5. 0.65초 뒤 보스가 임시 무작위 강도 1~3으로 자기 손을 사용해 흔든다. 압력·폭발 판정도 한 번 수행하며 완료 후 다시 플레이어가 선택할 수 있다. 보스 대기와 동작 중에도 입력을 잠근다.

미선택 상태에서 콜라를 클릭해도 진행하지 않는다. 같은 강도를 다시 누르거나 강도를 여러 번 바꿔도 압력 증가·난수 추첨은 없다. 새 행동마다 강도를 다시 선택한다.

## 계산 기준

- 강도 1·2·3의 압력 증가량은 각각 1·2·3이다.
- 새 콜라 한계는 임시로 18~24에서 정한다. 바로 직전 한계와 같으면 범위 안의 다른 값으로 바꾼다.
- 예상 확률은 `min(1, 흔든 뒤 압력 / 한계 압력)`의 제곱이다. **제곱 곡선은 사용자 선택으로 확정**했다. 예를 들어 한계 20, 흔든 뒤 압력 10이면 25%다. 한계 도달·초과 시 100%로 처리한다. 한계 범위와 강도별 증가량은 임시 설정이다.
- 예: 현재 8, 한계 20이면 강도 1/2/3 선택 시 각각 20.3%/25%/30.3%로 표시한다. 내부 판정은 반올림 전 확률을 사용한다.
- `PressureInteractionSettings.asset`의 `probabilityExponent`가 1이면 선형, 2이면 제곱 곡선이다. 설정 변경 후 새 Play 세션에서 적용된다.

## 책임과 연결

`BottlePressureModel`은 Unity UI·입력·애니메이션에 의존하지 않는다. `Predict(strength)`는 읽기 전용이며 `TryCommit(nextRandom, out result)`만 상태와 난수를 진행한다. 한 콜라가 폭발하면 `BeginBottle(limit)` 전까지 추가 행동을 거절한다.

`PressureGaugeDisplay.Show(currentPressure, limit, expectedPressure, expectedProbability)`가 실제/예측 바늘·공개 한계·확률창을 갱신한다. 예상값이 없으면 주황 바늘을 숨기고 확률은 `--%`다.

`ShakeSelectionInteraction.SelectStrength(int)`와 `ConfirmShake()`가 두 단계 입력을 분리한다. `HandlePointer(Vector2)`는 Main Camera의 Raycast로 기존 키 Collider나 중앙 콜라 Collider를 확인한다. 일반 플레이 입력은 설치된 Input System의 왼쪽 마우스 클릭이다. 키보드 단축키는 추가하지 않았다.

외부 경기 진행은 `BeginBottle(limit, pressure)`로 한계를 공급하고 `ShakeResolved` 이벤트에서 결과를 받아 피해·토큰 등에 연결한다. 이벤트는 각자의 흔들기 피드백 후 한 번씩 발생한다. `LastActionWasBoss`가 행동 주체이며 `IsBossTurn`은 보스 대기/행동을 포함한다. `automaticBossTurns`는 일반 씬에서 켜져 있고 보스의 강도 선택 난수는 폭발 판정과 분리한다. 비활성화·초기화는 보스 대기와 진행 중 연출·콜백도 취소한다.

손·병의 동기화 재생과 보스 미리보기는 `Docs/Art/ColaShakeAnimations.md`를 따른다. 보스 미리보기는 압력·난수·행동 횟수를 진행하지 않는다.

## 검증 근거

`output/pressure-interaction/Core-validation.txt`는 선택/비교 시 상태·난수 불변, 예측과 확정 계산 일치, 한 번만 확정, 한계 미만 확률 폭발과 경계 조건을 검사한다. 같은 폴더의 `Playmode-validation.txt`는 자동 보스 응답을 끈 상태에서 압력 규칙을 독립 검증한다. 실제 자동 보스까지의 전체 흐름은 `output/shake-animation/Playmode-validation.txt`의 Play 모드 상태 검사를 따른다. 화면 확인은 Play 중 실제 Game 뷰에서 한다. 검증용 상태는 씬에 저장하지 않는다.
