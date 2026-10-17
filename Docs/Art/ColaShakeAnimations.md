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

현재 MainScene은 **직접 플레이하는 경기**로 저장되어 있으며 손의 자동 반복 미리보기는 껐다. 원본 FPS 오른손으로 병의 오른쪽을 잡아 흔드는 동작은 실제 행동을 확정했을 때만 재생한다. 조작과 경기 규칙은 `Docs/Gameplay/PlayableDuel.md`를 따른다.

경기 컨트롤러는 도메인·씬 재로드를 끈 설정에서도 Play 진입마다 상태와 이벤트 구독을 초기화한다. 최신 경기 검증은 `output/verification/latest/playable-duel/Playmode-validation.txt`다. 과거 자동 미리보기 기록은 현재의 경기 시작 동작을 뜻하지 않는다.

플레이어는 기존처럼 강도 1·2·3 버튼을 선택한 뒤 중앙 콜라를 클릭한다. 확정 클릭에서 압력·확률 판정을 한 번 수행하고 FPS 모델의 손으로 흔든다. 동작과 폭발 피드백이 끝나면 0.65초 뒤 보스가 강도 1~3 중 하나를 골라 같은 병을 자기 손으로 흔든다. 보스 행동도 실제 압력·폭발 판정을 한 번 수행한다. 보스가 병을 내려놓은 뒤 다음 플레이어 입력을 받는다. 두 동작과 대기 사이에는 추가 클릭을 막는다.

보스 동작은 일반 플레이에서 자동 재생된다. 동작만 따로 볼 때는 Play 모드의 `COLA_COLA > Preview Boss Cola Shake (Play Mode)` 또는 `ColaShakeAnimator` 컴포넌트 컨텍스트 메뉴를 사용할 수 있다. 이 별도 미리보기는 강도 2를 사용하며 압력이나 턴을 진행하지 않는다.

다른 시스템에서 연결할 때는 `PlayShake(bool boss, int strength)` 코루틴을 사용한다. `boss`가 `false`이면 플레이어, `true`이면 보스다. `CancelAndRestore()`는 진행 중인 동작을 중단하고 자세를 복원한다. 플레이어 입력 컴포넌트의 비활성화·세션 초기화에도 취소 처리가 연결되어 있다.

`ActiveHand`로 현재 동작하는 원본 손 본을, `BottleHeld`로 손에 고정된 구간인지 확인한다. 이 상태는 표현용이며 턴이나 압력 판정을 진행하지 않는다.

`ShakeSelectionInteraction.automaticBossTurns`가 일반 플레이의 자동 보스 응답을 켠다. 강도 선택용 난수는 폭발 판정용 난수와 분리했다. `IsBossTurn`과 `LastActionWasBoss`로 차례와 마지막 행동 주체를 확인한다. `ShakeResolved`는 각 행동이 끝날 때 한 번씩 발생하며 경기 컨트롤러가 피해·토큰·승패를 반영한다. 초기화·비활성화는 보스 대기와 진행 중인 손 동작도 취소한다. 현재 보스 선택은 임시 무작위이며 아이템 효과·상점·보스 전술은 제외한다.

## 원본 FPS 모델 사용

추가했던 `PlayerSleeveContinuation` 메시와 생성 코드를 제거했다. 플레이어는 `Assets/2.Assets/02_Player_Arms/Models/Player_Arms.fbx`의 원본 스킨 메시·텍스처·손가락 본을 사용한다. 오른손 손바닥이 병의 오른쪽 옆면을 향하도록 수정했다. 짧게 제작된 FPS 위팔은 동작 중 기존 본 길이를 시점에 맞게 보정하여 잘린 어깨 끝이 원래 화면 밖 위치에 남도록 한다. 전완·손·손가락의 비율과 원본 메시 자체는 유지하며 새 팔 메시를 만들지 않는다. 보스는 `Assets/2.Assets/03_Simple_Character_PSX`의 원본 스킨 메시를 사용한다. 원본 FBX·임포터·재질·프리팹을 변경하지 않는다. 보스는 병을 잡기 위해 숙이는 동안 왼손을 테이블 앞으로 조금 밀어 지지한다.

## 검증 기준

검증은 Unity Play 모드에서 실제 입력·Animator·원본 손 본·병 위치·턴 상태를 관찰한다. 자동 반복 두 번, 오른쪽 파지, 병 고정, 입력 잠금, 플레이어→보스→플레이어 순서, 취소 후 Idle 복귀와 압력 규칙을 검사한다.

`output/verification/latest/shake-animation/Playmode-validation.txt`는 상태와 본 이동 및 CPU 스키닝 계산의 자동 검사 결과다. `Automatic-preview-validation.txt`는 Play 자동 시작과 반복 결과다. 이 기록만으로 실제 Game 뷰의 화면까지 확인했다고 판단하지 않는다. 시각적 확인은 Play 중 Game 뷰에서 한다.

별도 카메라 렌더·Edit 모드 클립 샘플링·연속 프리뷰 캡처 생성 코드를 흔들기 검증에서 제거했다. 임시 실행 빌드, 진단 코드, 중복 미리보기, 과거 백업과 캡처는 정리했다. 최신 Play 로그와 검증 결과만 유지한다. 공통 작업 규칙은 프로젝트 루트 `AGENTS.md`를 따른다.
