# 대기 동작·카메라 호흡·압력 예측 전환

보스의 30초 Idle에서 가슴 호흡과 상체 체중 이동을 키우고, 병 쪽으로 고개를 낮추거나 좌우를 잠깐 보는 동작을 늘렸다. 양손은 테이블에 지지한 채로 유지한다. `CharacterIdleFactory.RebuildBossOnly`로 보스 클립만 갱신하며 FPS 팔 Idle·흔들기 클립·모델은 유지한다.

`CM_SeatedDuel`의 `ColaCameraBreathing`이 약 4.8초 주기로 카메라에 최대 4mm의 수직 움직임과 약 0.16도의 피치 움직임을 더한다. 작은 좌우·앞뒤 움직임도 함께 들어간다. Cinemachine의 보정 채널을 사용하므로 기준 Transform과 FOV는 바뀌지 않고 위치가 누적되지 않는다. 컴포넌트를 끄면 기준 시점으로 돌아온다.

강도 선택 시 주황 예측 바늘과 확률 숫자가 약 0.38초 동안 부드럽게 목표값으로 이동한다. 빠르게 다른 버튼을 선택하면 현재 표시 위치에서 새 값으로 이어진다. 같은 버튼을 다시 눌러도 진행 중 전환이 초기화되지 않는다. 새 병·재시작은 미리보기를 지우며, 행동 확정 시에는 정확한 최종 확률로 표시한다. 선택과 전환은 압력·토큰·턴·폭발 난수를 진행하지 않는다.

`PressureGaugeDisplay.ProbabilityText`는 정확한 목표값이고 `DisplayedProbabilityText`는 현재 실제 메시 숫자 표시값이다. `DisplayedPreviewAngle`, `DisplayedProbability`, `IsPreviewAnimating`으로 전환 상태를 확인한다. 실시간 전환은 `forecastTransitionSeconds`로 조정한다.

적용 메뉴는 `COLA_COLA > Apply Livelier Idle and Animated Forecast`다. 검증은 Play 모드에서 전체 Idle 반복, 손 지지, Cinemachine의 실제 출력 위치·회전, 버튼 1→3→2의 연속 선택과 중간 표시값, 확정 후 보스 차례 및 재시작을 검사한다. 결과는 `output/verification/latest/presentation-motion/Playmode-validation.txt`다. 프리뷰 카메라·별도 렌더·연속 캡처는 생성하지 않는다. 자동 상태 검사 결과를 Game 뷰의 시각 검증으로 표현하지 않는다.
