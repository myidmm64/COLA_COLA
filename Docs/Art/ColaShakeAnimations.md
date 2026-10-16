# 콜라 흔들기 애니메이션

플레이어와 보스가 오른손으로 콜라에 손을 뻗고, 손가락으로 병을 잡아 들어 흔든 뒤, 원래 자리에 내려놓고 손을 거두는 동작이다. 각 동작은 4.4초이며 강도 1·2·3에 대응하는 클립을 각각 둔다. 보스는 상체를 기울이고 왼손을 테이블에 지지하며, 플레이어는 1인칭 팔의 어깨와 팔꿈치를 함께 움직인다.

강도가 높을수록 흔들림의 크기와 횟수가 증가한다. 현재 상하 이동의 진폭 계수는 2 / 2.8 / 3.6cm, 기울기 계수는 9 / 12 / 15도다. 실제로 흔드는 1.1초 구간에는 각각 2 / 2.5 / 3회의 파형을 사용하고, 시작과 끝의 진폭을 부드럽게 줄인다.

## 파일과 재생 구조

- 캐릭터 클립: `Assets/_COLA_COLA/Animations/Shake/{Player,Boss}_ColaShake_{1..3}.anim`
- 병 클립: `Assets/_COLA_COLA/Animations/Shake/{Player,Boss}_BottleMotion_{1..3}.anim`
- 제작 도구: `Assets/Editor/COLACOLA/ColaShakeAnimationFactory.cs`
- 런타임 재생: `Assets/_COLA_COLA/Scripts/Presentation/ColaShakeAnimator.cs`
- 씬 연결: `COLA_COLA_MainScene/08_Devices/ColaShakeAnimation`

캐릭터와 병의 클립을 같은 시간으로 재생해 잡고 있는 손과 병의 움직임을 맞춘다. 재생 중에는 해당 캐릭터의 Animator를 잠시 멈추며, 앞뒤 0.2초에 기존 Idle과 자세를 보간한다. 완료하거나 취소하면 병과 캐릭터 루트의 부모·로컬 자세·크기를 복원하고, 기존 Idle의 재생 상태를 이어간다. 기존 Idle 클립과 Animator Controller는 수정하지 않는다.

## 실행 방법

플레이어는 기존처럼 강도 1·2·3 버튼을 선택한 뒤 중앙 콜라를 클릭한다. 확정 클릭에서 기존 압력·확률 판정을 한 번 수행하고, 전체 흔들기 동작이 끝나면 기존 폭발 연출 또는 다음 입력 대기로 이어진다. 동작 중에는 추가 입력을 막는다.

보스 동작은 Play 모드에서 `COLA_COLA > Preview Boss Cola Shake (Play Mode)` 또는 `ColaShakeAnimator` 컴포넌트의 `Preview Boss Shake (Play Mode)` 컨텍스트 메뉴로 확인한다. 컴포넌트에는 플레이어 미리보기도 있다. 미리보기는 강도 2를 사용하며 압력이나 턴을 진행하지 않는다.

다른 시스템에서 연결할 때는 `PlayShake(bool boss, int strength)` 코루틴을 사용한다. `boss`가 `false`이면 플레이어, `true`이면 보스다. `CancelAndRestore()`는 진행 중인 동작을 중단하고 자세를 복원한다. 플레이어 입력 컴포넌트의 비활성화·세션 초기화에도 취소 처리가 연결되어 있다.

보스 AI, 자동 턴 전환, 피해량·토큰·아이템 효과는 이번 애니메이션에 추가하지 않았다. Unity에서 생성한 검증 보고서와 미리보기는 `output/shake-animation`에 저장한다. 최종 통과 여부와 측정값은 해당 보고서를 기준으로 확인한다.

## 소매와 지지 자세

플레이어 팔을 뻗을 때 원본 모델의 잘린 위팔 끝이 보이지 않도록 `PlayerSleeveContinuation`을 추가했다. 원본 소매의 열린 테두리 10개 정점을 같은 본·바인드 포즈로 계산하고, 원본 재질과 UV를 사용하는 다섯 단의 메시로 카메라 뒤까지 잇는다. 원본 FBX·읽기 설정·재질·리그 계층은 변경하지 않는다. 보스는 병을 잡기 위해 숙이는 동안 왼손을 테이블 앞으로 조금 밀고 팔꿈치를 바깥쪽 아래로 배치한다.

## 최종 검증과 미리보기

Unity 6000.6.4f1의 저장본 재열기와 실제 Play 검증을 통과했다. 플레이어는 실제 강도 선택·콜라 확정 경로로, 보스는 미리보기 경로로 전체 동작을 재생했다. 손목에 대한 병의 최대 상대 이동 오차는 플레이어 1.329mm / 0.213°, 보스 0.209mm / 0°였다. 병을 든 높이는 각각 약 15.9cm / 17.7cm이며 캐릭터 루트·부모·크기는 유지했다.

보스 미리보기의 입력 잠금과 압력·난수 불변, 동작 중 드라이버 또는 입력 컴포넌트 비활성화 후 병·리그·Idle 복귀, 중단 후 오래된 콜백 취소를 확인했다. 기존 압력 선택·예측·폭발·새 콜라 테스트와 전체 MainScene 검사도 통과했다. 상세 수치는 `output/shake-animation/Playmode-validation.txt`, 제작 시 자세 검사는 `Animation-authoring-validation.txt`, 소매 테두리 정보는 `Sleeve-continuation-authoring.txt`에 있다.

기존 Idle·설정·패키지·장치/아이템 프리팹 등 보호 파일 32개의 SHA-256이 작업 전후 일치했다. 파일별 기록은 `preserved-files-before.json`, `preserved-files-after.json`이다.

- `Player-ColaShake.webp`, `Boss-ColaShake.webp`: 메인 카메라에서 실제 Unity 클립을 24fps로 샘플링한 900×506 미리보기. 각 107장의 렌더를 크기 변경 없이 WebP로 압축한다. 원본 PNG는 `player-smooth-frames`, `boss-smooth-frames`에 보존한다.
- `player-frames`, `boss-frames`: 실제 Play 모드에서 캡처한 동작별 22장. 런타임 바인딩·동기화 검증의 근거다.
- `Player-Reach.png`, `Player-Shake.png`, `Boss-Reach.png`, `Boss-Shake.png`: 1600×900 핵심 자세 렌더.
- `unity-final-animation.log`: 최종 애니메이션·압력 입력·전체 씬 통과 로그.
