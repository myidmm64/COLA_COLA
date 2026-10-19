> 2026-10-19 규칙 갱신. 이번 변경의 Play·컴파일·가상 대전은 실행하지 않았다. 아래 과거 검증 기록은 이번 버전의 통과 근거가 아니다.

# 압력 선택과 콜라 클릭 확정

현재 MainScene에는 플레이어와 보스의 교대 행동, 실제 체력·토큰, 승패·재시작과 다음 난이도, 손에 든 콜라의 폭발 연출이 연결되어 있다. 경기 규칙과 조작은 `PlayableDuel.md`를 따른다. 현재 단일 효과 아이템과 웨이터벨은 [Items.md](Items.md)를 따른다.

## 플레이 순서

1. 새 콜라의 한계 압력을 마지막 눈금에서 확인한다.
2. 1·2·3 버튼을 클릭한다. 현재 압력은 그대로이고 선택 키·주황 예측 바늘·예상 폭발 확률만 바뀐다.
3. 중앙 콜라를 클릭한다. 선택에 따라 압력 +2/+5/+8을 적용하고 화면에 표시했던 확률로 폭발을 한 번 판정한다.
4. 플레이어가 병을 잡고 들어 흔든 뒤 내려놓는 동안 입력을 잠근다. 안전한 행동은 기존 4.4초이며, 폭발 시에는 손으로 잡은 구간에서 짧은 압력 고조·분출·반동을 재생한 뒤 복귀한다. 안전하면 누적 압력을 유지하고 선택을 해제한다. 폭발하면 연출과 손 복귀 후 새 콜라와 새 공개 한계로 교체한다.
5. 0.65초 뒤 보스가 공개된 압력·체력·토큰·효과로 강도를 고르고 아이템 사용 뒤 재판단해 자기 손으로 흔든다. 압력·폭발 판정도 한 번 수행하며 완료 후 다시 플레이어가 선택할 수 있다. 보스 대기와 동작 중에도 입력을 잠근다.

미선택 상태에서 콜라를 클릭해도 진행하지 않는다. 같은 강도를 다시 누르거나 강도를 여러 번 바꿔도 압력 증가·난수 추첨은 없다. 새 행동마다 강도를 다시 선택한다.

## 계산 기준

- 강도 1·2·3의 기본 압력 증가량은 각각 **2·5·8**이다. 플레이어와 보스에 동일하게 적용하며 탄산 시럽은 +2, 매운맛 주문서는 2배를 적용한다. 두 압력 보정은 함께 대기할 수 없다. 안전 보상은 선택 강도인 1·2·3토큰으로 유지한다.
- 새 콜라의 공개 한계는 **25~35**에서 정한다. 바로 직전 한계와 같으면 범위 안의 다른 값으로 바꾼다.
- 기본 예상 확률은 `min(1, (흔든 뒤 압력 / 한계 압력)^2)`이다. 압력 비율 20%/50%/80%에서는 4%/25%/64%이며 한계 도달·초과 시 100%다. 마개는 피해를 줄이지 않고 내게 걸린 시럽·매운맛만 제거한다. W의 디버그 100% 고정은 유지한다.
- 한계 30인 새 병에서 강도 1/2/3은 각각 약 **0.4%/2.8%/7.1%**다. 현재 압력이 6이면 각각 약 **7.1%/13.4%/21.8%**다. 내부 판정은 반올림 전 확률을 사용한다. 토큰 규칙은 안전 시 강도만큼, 폭발 시 총 5개다.
- `PressureInteractionSettings.asset`의 현재 `probabilityExponent`는 **2**이다. 초반 위험은 낮게 두고 후반에 선택 차이가 커지도록 설계했다. 설정 변경 후 새 Play 세션에서 적용된다.

## 책임과 연결

`BottlePressureModel`은 Unity UI·입력·애니메이션에 의존하지 않는다. `Predict(strength)`는 읽기 전용이며 `TryCommit(nextRandom, out result)`만 상태와 난수를 진행한다. 한 콜라가 폭발하면 `BeginBottle(limit)` 전까지 추가 행동을 거절한다.

`PressureGaugeDisplay.Show(currentPressure, limit, expectedPressure, expectedProbability)`가 실제/예측 바늘·공개 한계·확률창을 갱신한다. 예상값이 없으면 주황 바늘을 숨기고 확률은 `--%`다.

`ShakeSelectionInteraction.SelectStrength(int)`와 `ConfirmShake()`가 두 단계 입력을 분리한다. `HandlePointer(Vector2)`는 Main Camera의 Raycast로 기존 키 Collider나 중앙 콜라 Collider를 확인한다. 왼쪽 마우스 클릭 또는 숫자 1·2·3 선택 후 Space로 조작한다. `InputEnabled`가 경기 종료 후 선택·확정을 막는다.

외부 경기 진행은 `BeginBottle(limit, pressure)`로 한계를 공급하고 `ShakeResolved` 이벤트에서 결과를 받아 피해·토큰 등에 연결한다. 이벤트는 각자의 흔들기 피드백 후 한 번씩 발생한다. `LastActionWasBoss`가 행동 주체이며 `IsBossTurn`은 보스 대기/행동을 포함한다. `automaticBossTurns`는 일반 씬에서 켜져 있고 보스의 강도 선택 난수는 폭발 판정과 분리한다. 비활성화·초기화는 보스 대기와 진행 중 연출·콜백도 취소한다.

손·병의 동기화 재생과 보스 미리보기는 `Docs/Art/ColaShakeAnimations.md`를 따른다. 보스 미리보기는 압력·난수·행동 횟수를 진행하지 않는다.

## 검증 근거

현재 **25~35·지수 1.6·압력 증가 2/4/8**은 압력 수치는 체력 10에서 가상 비교했으며, 이후 사용자 요청으로 현재 시작 체력은 6이다. 체력 6의 가상 비교는 실행하지 않았다. 현재 벨은 6토큰/2개이며 [현재 아이템 밸런스](WeightedItemBalance.md)를 따른다. Unity Play·컴파일 검사는 실행하지 않았다. [이전 경기 길이 설계](PacingBalance.md)와 [최신 가상 결과](../../output/verification/latest/Summary.md)를 참고한다. 기존 `PressureBalanceVerification.Begin`과 `output/verification/latest/pressure-balance/Playmode-validation.txt`는 이전 20~30·지수 1.5 기준이며 새 밸런스의 검증 근거가 아니다. 체감 밸런스와 실제 Game 뷰는 사용자가 다음 Play에서 확인한다.

`output/verification/latest/pressure-interaction/Core-validation.txt`는 선택/비교 시 상태·난수 불변, 예측과 확정 계산 일치, 한 번만 확정, 한계 미만 확률 폭발과 경계 조건을 검사한다. 같은 폴더의 `Playmode-validation.txt`는 자동 보스 응답을 끈 상태에서 압력 규칙을 독립 검증한다. 실제 자동 보스까지의 전체 흐름은 `output/verification/latest/shake-animation/Playmode-validation.txt`의 Play 모드 상태 검사를 따른다. 화면 확인은 Play 중 실제 Game 뷰에서 한다. 검증용 상태는 씬에 저장하지 않는다.
