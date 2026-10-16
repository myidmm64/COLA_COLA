# 콜라 흔들기 애니메이션

플레이어와 보스가 오른손으로 콜라에 손을 뻗고, 손가락으로 병을 잡아 들어 흔든 뒤, 원래 자리에 내려놓고 손을 거두는 동작이다. 각 동작은 4.4초이며 강도 1·2·3에 대응하는 클립을 각각 둔다. 보스는 상체를 기울이고 왼손을 테이블에 지지하며, 플레이어는 1인칭 팔의 어깨와 팔꿈치를 함께 움직인다.

강도가 높을수록 흔들림의 크기와 횟수가 증가한다. 현재 상하 이동의 진폭 계수는 2 / 2.8 / 3.6cm, 기울기 계수는 9 / 12 / 15도다. 실제로 흔드는 1.1초 구간에는 각각 2 / 2.5 / 3회의 파형을 사용하고, 시작과 끝의 진폭을 부드럽게 줄인다.

## 파일과 재생 구조

- 캐릭터 클립: `Assets/_COLA_COLA/Animations/Shake/{Player,Boss}_ColaShake_{1..3}.anim`
- 병 클립: `Assets/_COLA_COLA/Animations/Shake/{Player,Boss}_BottleMotion_{1..3}.anim`
- 제작 도구: `Assets/Editor/COLACOLA/ColaShakeAnimationFactory.cs`
- 런타임 재생: `Assets/_COLA_COLA/Scripts/Presentation/ColaShakeAnimator.cs`
- 씬 연결: `COLA_COLA_MainScene/08_Devices/ColaShakeAnimation`

캐릭터와 병의 클립을 같은 시간으로 재생한다. **병을 잡은 1.15~3.35초 구간에는 각 캐릭터의 실제 오른손 본이 병의 위치·회전을 직접 결정한다.** 플레이어는 원본 팔 리그의 `Wrist.R`, 보스는 원본 보스 리그의 `hand.R`에 연결한다. 접촉 자세에서 병의 손 기준 위치·회전을 기록하고, 들어서 흔드는 동안 그 관계를 고정한다. 상대 리그의 손이 잘못 연결되면 재생을 거절한다.

플레이어는 원래 FPS 팔 모델의 **오른손으로 병의 오른쪽 옆면을 잡고**, 손가락으로 뒤쪽을 감싸 자기 몸 쪽으로 당겨 흔든다. 보스는 자신의 오른쪽인 화면 왼쪽과 몸 앞쪽으로 당겨 흔든다. 각자의 오른팔·손가락만 병을 잡고, 기다리는 상대는 양손을 테이블에 둔 기존 Idle을 계속한다.

재생 중에는 원본 캐릭터의 Animator를 활성 상태로 유지하고 PlayableGraph로 손 동작을 재생한다. 앞뒤 0.2초에는 기존 Idle과 자세를 보간한다. 완료하거나 취소하면 병과 캐릭터 루트의 부모·로컬 자세·크기를 복원하고, 기존 Idle의 재생 상태를 이어간다. 기존 Idle 클립과 Animator Controller는 수정하지 않는다.

## 실행 방법

현재 MainScene은 **Play를 누르면 약 0.75초 뒤 플레이어의 손 동작을 자동으로 반복**하도록 저장되어 있다. 원본 FPS 오른손으로 병의 오른쪽을 잡아 강도 2로 흔들고 내려놓으며, 각 4.4초 동작 사이에 1초를 쉰다. 이 확인용 반복은 압력이나 턴을 진행하지 않는다. `08_Devices/ColaShakeAnimation`의 `ColaShakeAnimator`에서 `Preview Player On Play`를 끄면 다음 Play부터 일반 입력으로 진행한다. Play 중 즉시 멈추려면 같은 컴포넌트의 컨텍스트 메뉴 `Stop Player Play Preview`를 사용한다.

Editor의 Play 진입 이벤트에서 시작하므로 도메인·씬 재로드를 끈 설정에서도 반복 확인할 수 있다. 자동 시작·두 번 반복·원본 오른손 이동 검증 기록은 `output/shake-animation/Automatic-preview-validation.txt`, 엔진 로그는 `unity-auto-play-preview.log`다.

플레이어는 기존처럼 강도 1·2·3 버튼을 선택한 뒤 중앙 콜라를 클릭한다. 확정 클릭에서 압력·확률 판정을 한 번 수행하고 FPS 모델의 손으로 흔든다. 동작과 폭발 피드백이 끝나면 0.65초 뒤 보스가 강도 1~3 중 하나를 골라 같은 병을 자기 손으로 흔든다. 보스 행동도 실제 압력·폭발 판정을 한 번 수행한다. 보스가 병을 내려놓은 뒤 다음 플레이어 입력을 받는다. 두 동작과 대기 사이에는 추가 클릭을 막는다.

보스 동작은 일반 플레이에서 자동 재생된다. 동작만 따로 볼 때는 Play 모드의 `COLA_COLA > Preview Boss Cola Shake (Play Mode)` 또는 `ColaShakeAnimator` 컴포넌트 컨텍스트 메뉴를 사용할 수 있다. 이 별도 미리보기는 강도 2를 사용하며 압력이나 턴을 진행하지 않는다.

다른 시스템에서 연결할 때는 `PlayShake(bool boss, int strength)` 코루틴을 사용한다. `boss`가 `false`이면 플레이어, `true`이면 보스다. `CancelAndRestore()`는 진행 중인 동작을 중단하고 자세를 복원한다. 플레이어 입력 컴포넌트의 비활성화·세션 초기화에도 취소 처리가 연결되어 있다.

`ActiveHand`로 현재 동작하는 원본 손 본을, `BottleHeld`로 손에 고정된 구간인지 확인한다. 이 상태는 표현용이며 턴이나 압력 판정을 진행하지 않는다.

`ShakeSelectionInteraction.automaticBossTurns`가 일반 플레이의 자동 보스 응답을 켠다. 강도 선택용 난수는 폭발 판정용 난수와 분리했다. `IsBossTurn`과 `LastActionWasBoss`로 차례와 마지막 행동 주체를 확인한다. `ShakeResolved`는 각 행동이 끝날 때 한 번씩 발생한다. 초기화·비활성화는 보스 대기와 진행 중인 손 동작도 취소한다. 현재 보스 선택은 임시 무작위이며 피해량·토큰·아이템 효과·상점·보스 전술은 별도다.

## 원본 FPS 모델 사용

추가했던 `PlayerSleeveContinuation` 메시와 생성 코드를 제거했다. 플레이어는 `Assets/2.Assets/02_Player_Arms/Models/Player_Arms.fbx`의 원본 스킨 메시·텍스처·손가락 본을 사용한다. 오른손 손바닥이 병의 오른쪽 옆면을 향하도록 수정했다. 짧게 제작된 FPS 위팔은 동작 중 기존 본 길이를 시점에 맞게 보정하여 잘린 어깨 끝이 원래 화면 밖 위치에 남도록 한다. 전완·손·손가락의 비율과 원본 메시 자체는 유지하며 새 팔 메시를 만들지 않는다. 보스는 `Assets/2.Assets/03_Simple_Character_PSX`의 원본 스킨 메시를 사용한다. 원본 FBX·임포터·재질·프리팹을 변경하지 않는다. 보스는 병을 잡기 위해 숙이는 동안 왼손을 테이블 앞으로 조금 밀어 지지한다.

## 최종 검증과 미리보기

최신 검증은 Unity 6000.6.4f1의 실제 Play에서 강도 버튼과 콜라를 Raycast로 클릭하여 **플레이어 → 자동 보스 → 플레이어 입력 대기** 전체 경로를 확인한다. 본 이동뿐 아니라 원본 FBX에 속한 실제 스킨 메시 정점이 움직이는지 측정한다. 측정값과 최종 통과 여부는 `output/shake-animation/Playmode-validation.txt`를 따른다.

입력 잠금, 별도 미리보기의 압력·난수 불변, 플레이어와 실제 보스 동작 도중 취소·Idle 복귀·오래된 콜백 방지도 검사한다. 기존 압력 규칙 검사는 자동 응답을 잠시 끈 Play 세션에서 독립적으로 실행한다. 제작 시 자세 검사는 `Animation-authoring-validation.txt`다.

원본 FPS·보스 FBX/프리팹/텍스처와 기존 Idle·설정·장치/아이템 프리팹의 보존 기록은 `fps-model-preserved-before.json`, `fps-model-preserved-after.json`이다. 수정 전 씬·미리보기와 제거한 소매 코드는 `fps-model-fix-before`에 보존했다.

원본 FPS 적용과 실제 자동 보스 응답, 기존 압력 입력·MainScene 검사를 통과했다. 실제 스킨 메시의 표면 이동과 손·병 고정을 검사하고, 플레이어 클릭 한 번에서 각자 한 번씩 압력 판정·결과 이벤트가 발생해 입력이 플레이어에게 돌아오는 것을 확인한다. 상세 수치는 최신 `Playmode-validation.txt`를 따른다. 원본 모델 적용 당시 보호 파일 46개의 SHA-256이 같았으며, 이후 플레이어 오른쪽 파지 수정의 보존 기록은 아래 별도 항목을 따른다.

## 플레이어 오른쪽 파지 수정

`COLA_COLA > Revise Player Right Side Grip`은 플레이어 클립만 재생성한다. 오른손 손바닥이 병의 오른쪽 옆면을 향하고 손가락이 뒤쪽을 감싸며, 잡은 동안 손목이 병의 오른쪽에 남도록 한다. 세 강도의 제작 샘플에서 최소 오른쪽 손목 간격은 약 7.66cm였다. 실제 Play에서도 파지 구간의 오른쪽 위치·병 고정·Idle 복귀와 기존 자동 보스 응답을 확인했다.

보스 클립·메타, MainScene, 런타임 동작 코드와 원본 모델 등 보호 파일 61개의 SHA-256이 모두 동일했다. 씬 저장이나 보스 클립 재생성 없이 기존 플레이어 클립의 GUID를 유지하며 갱신한다. 근거는 `Player-RightSide-authoring.txt`, `player-right-side-preserved-before.json`, `player-right-side-preserved-after.json`, `unity-player-right-side.log`다. 수정 전 파일은 `player-right-side-before`에 보존했다.

- `Player-RightSide-ColaShake.webp`: 최신 플레이어 동작 미리보기. 강도 3의 원본 Unity 렌더를 24fps로 인코딩한다.
- `Player-RightSide-encoding.json`: 해당 미리보기의 프레임·재생 시간 정보.

- `Duel-OriginalFPS-LiveBoss.webp`: 실제 Play의 플레이어 클릭부터 보스 자동 행동까지 캡처한 연속 화면. 원본은 `duel-live-frames`, 시각은 `duel-live-frame-times.txt`다.
- `Player-OriginalFPS-ColaShake.webp`, `Boss-OriginalModel-ColaShake.webp`: 강도 3의 Unity 클립과 실제 손 고정을 24fps로 샘플링한 900×506 동작별 미리보기. 원본 PNG는 `player-smooth-frames`, `boss-smooth-frames`에 보존한다.
- `player-frames`, `boss-frames`, `boss-live-turn-frames`: 실제 Play의 플레이어·별도 보스 미리보기·자동 보스 행동 캡처.
- `Player-Reach.png`, `Player-Shake.png`, `Boss-Reach.png`, `Boss-Shake.png`: 1600×900 핵심 자세 렌더.
- `unity-original-fps-presentation.log`: 원본 FPS 메시·실제 자동 보스 행동·압력 입력·전체 씬의 최종 검증 로그.
