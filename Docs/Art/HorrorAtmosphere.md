# COLA_COLA 호러 조명과 화면 효과

## 적용 범위

`Assets/Scenes/MainScene.unity`의 피자리아를 어둡고 긴장감 있는 분위기로 조정한다. 테이블과 콜라, 압력계·버튼은 읽을 수 있게 두고, 보스 뒤의 붉은 역광과 주방의 녹색 형광등으로 공간의 깊이를 만든다. 기존 캐릭터 자세·Idle·아이템·장치 방향과 앉은 카메라 구도를 유지한다.

카메라 구도는 위치 `(0, 1.32, -1.40)`, 회전 `(11, 0, 0)`, 수직 FOV `50`, Near Clip `0.035`다. 화면은 기존처럼 16:9를 기준으로 한다.

## 패키지와 카메라

- `Packages/manifest.json`에 `com.unity.cinemachine: 6.6.0`을 명시한다. Unity 6000.6.4f1에 번들된 버전으로 Editor가 해결한 버전이다.
- `07_Cameras/CM_SeatedDuel`의 `Unity.Cinemachine.CinemachineCamera`를 Main Camera의 `CinemachineBrain`이 사용한다. 고정된 앉은 구도이며 Follow·LookAt 대상이나 이동 파이프라인은 추가하지 않는다.
- Cinemachine의 기본 전환은 Ease In Out 0.65초, 업데이트는 Late Update다. 카메라 이동이나 흔들림은 현재 추가하지 않았다.
- 화면 후처리는 이미 사용하는 URP `17.6.0`의 내장 Volume 시스템으로 구성한다. URP와 호환되지 않는 구형 Post Processing Stack v2 패키지는 설치하지 않는다.
- Main Camera의 HDR와 Render Post Processing을 켠다. 저폴리 윤곽을 유지하도록 MSAA와 카메라의 후처리 안티앨리어싱은 끈다.

## 조명

`06_Lighting`에 기존 조명을 재사용하고 필요한 조명만 이름을 지정해 추가한다. 테스트 아이템용 국소 보조광을 포함해 현재 총 11개다. 조명은 실시간 조명이며, 테이블 키 라이트에 부드러운 그림자를 사용한다. 아래 강도는 Unity Light의 intensity 값이다.

| 오브젝트 | 역할 | 강도 |
|---|---|---:|
| `Warm_Table_Key` | 위에서 테이블·콜라·손을 비추는 옅은 녹색 키 라이트 | 4.1 |
| `Warm_Face_Fill` | 보스의 얼굴과 상체를 읽게 하는 차가운 보조광 | 1.25 |
| `Horror_Face_Softbox` | 보스의 얼굴 앞면을 읽게 하는 48° 스포트 보조광 | 2.3 |
| `Horror_Cola_LabelFill` | 중앙 콜라로 시선을 모으는 따뜻한 46° 스포트 보조광 | 0.90 |
| `Kitchen_Warm` | 주방의 어두운 녹색 공간광 | 0.50 |
| `Soda_Cool_Fill` | 음료 기계 방향의 청록색 보조광 | 0.95 |
| `Horror_Boss_RedRim` | 보스 뒤에서 들어오는 붉은 역광 | 2.1 |
| `Horror_Gauge_Readability` | 축소한 압력계의 가독성을 유지하는 약한 보조광 | 0.16 |
| `Horror_Kitchen_Fluorescent` | 주방 형광등의 약한 밝기 변화 | 기본 0.85 |
| `Horror_Emergency_WallGlow` | 벽의 붉은 비상등 주변광 | 0.65 |
| `TestItems_ReadabilityFill` | 양쪽 아이템 쟁반을 읽게 하는 국소 보조광 | 0.85 |

환경광은 어두운 녹색 계열의 Flat Ambient를 사용하고 Skybox는 제거한다. 반사 강도는 `0.12`다. 배경을 멀리 보낼 때는 Exponential Squared Fog를 사용하며 밀도는 `0.065`다.

압력계 축소 단계에서 압력계 보조광을 `0.40 → 0.16`, 콜라 라벨 보조광을 `0.55 → 0.90`으로 조정했다. 콜라·압력계의 조명 균형만 부분 적용할 때는 `TabletopFocusSceneTools`가 `HorrorSceneTools.ApplyTabletopFocusLighting`을 사용한다. 전체 후처리·배경 조명을 재생성하지 않는다. 최신 화면은 `output/gauge-focus/MainScene-Focus.png`다.

`09_Atmosphere`에는 녹색 주방 스트립과 붉은 벽 비상등의 하우징·발광부를 실제 메시로 둔다. 네 메시의 Collider는 제거한다. 재질은 `Assets/_COLA_COLA/Art/Materials/HorrorLighting`에 따로 저장하며 원본 재질을 수정하지 않는다.

## 화면 효과

전용 프로필은 `Assets/_COLA_COLA/Art/PostProcessing/PizzeriaHorror.asset`이다. `09_Atmosphere/Horror_GlobalVolume`에서 Global Volume으로 사용하며 Default 레이어, Priority `100`, Weight `1`을 사용한다. Main Camera의 Volume Layer Mask도 Default 레이어다. 공용 프로필과 렌더 파이프라인 에셋은 이 연출용으로 수정하지 않는다.

| 효과 | 설정과 의도 |
|---|---|
| Tonemapping | ACES로 조명의 밝은 부분을 정리한다. |
| Color Adjustments | Post Exposure `0.40`, Contrast `13`, Saturation `-19`. 채도를 낮추고 대비를 약간 완화해 바랜 필름 질감을 만든다. |
| White Balance | Temperature `-2`, Tint `0`. 조명 색은 유지하면서 전체 화면의 녹색 쏠림을 완화한다. |
| Lift / Gamma / Gain | Lift `(1, 1, 1, 0.006)`. 검정 바닥을 조금 올려 어두운 부분의 질감과 그레인이 남도록 한다. |
| Shadows / Midtones / Highlights | 어두운 부분은 청록색, 밝은 부분은 따뜻한 색으로 조정한다. |
| Bloom | Intensity `0.20`, Threshold `1.0`, Scatter `0.50`. 형광등과 비상등 주변에 가벼운 번짐을 만든다. |
| Film Grain | Thin2, Intensity `0.26`, Response `0.60`. 프레임마다 변하는 조금 굵은 입자로 은은한 자글거림을 더한다. |
| Vignette | Intensity `0.25`, Smoothness `0.62`. 테이블에 시선을 모으면서 가장자리의 소품도 읽히게 한다. |
| Chromatic Aberration | Intensity `0.018`로 가장자리에 약한 색 분리를 적용한다. |

심도 흐림이나 모션 블러는 현재 사용하지 않는다. 버튼·압력계·아이템은 선명하게 유지한다.

## 형광등 밝기 변화

런타임 스크립트는 `Assets/_COLA_COLA/Scripts/Presentation/HorrorPracticalFlicker.cs`다. `Horror_Kitchen_Fluorescent` 하나에 적용한다. 배경 조명에만 밝기 변화를 주고 테이블 키 라이트는 일정하게 유지한다.

- Perlin Noise로 기본 밝기의 약 `±7%`를 천천히 변화시킨다.
- 17초 주기 안에 0.3초 동안 부드럽게 어두워졌다가 돌아오는 구간을 둔다.
- 밝기는 기본값의 `70%` 아래로 내려가지 않는다. 조명을 완전히 끄지 않는다.
- 컴포넌트를 끄면 기본 밝기로 복구한다. Edit 모드에서는 깜빡임을 실행하지 않는다.

## 다시 적용하고 확인하기

에디터 메뉴 `COLA_COLA > Apply Horror Lighting and Cinemachine` 또는 `Assets/Editor/COLACOLA/HorrorSceneTools.cs`의 `Apply`를 사용한다. 저장된 MainScene을 열고 해당 씬에만 조명·카메라·Volume 구성을 적용한 뒤 저장한다. 기존 MainScene 전체를 재생성하지 않는다.

`MainSceneTools.Build`에도 `HorrorSceneTools.ApplyToScene` 호출이 포함되어 처음부터 씬을 만들 때 같은 연출을 적용한다. 이 작업 전 씬 백업은 `output/horror-atmosphere/MainScene-before-horror.unity`다.

`HorrorSceneTools.Verify`는 저장된 MainScene을 다시 열고 Play 모드에서 실제 Cinemachine 활성 카메라, 기존 구도, HDR·후처리, URP가 평가한 Volume, 형광등의 밝기 변화를 확인한다. 첫 렌더의 셰이더 준비 시간을 거친 뒤 `Time.timeAsDouble`로 게임 시간 18초 이상을 측정해 17초 조명 주기의 어두워지는 구간을 포함한다. 결과는 다음 파일을 기준으로 확인한다.

- `output/horror-atmosphere/Placement-validation.txt`: 조명·연출 그룹 외 기존 카메라·모델·본의 배치 보존.
- `output/horror-atmosphere/Horror-validation.txt`: 저장본 재열기와 실제 Play 모드의 검증 결과.
- `output/horror-atmosphere/Before.png`: 적용 전 화면.
- `output/horror-atmosphere/MainScene-Horror.png`: 적용 후 실제 Unity 렌더.
- `output/horror-atmosphere/frames`: 실제 Play 모드의 연속 렌더.
- `output/horror-atmosphere/original-file-hashes.json`: 작업 전 보존 대상 파일의 해시.

최종 밝기는 위 조명 강도와 프로필의 Post Exposure로 조정한다. `Apply`를 다시 실행하면 이 문서의 설정을 생성하는 코드 값으로 돌아가므로, 지속할 변경은 `HorrorSceneTools.cs`에도 반영한다.

## 검증 기록

Unity 6000.6.4f1에서 저장본을 다시 열어 Cinemachine `6.6.0 (BuiltIn)`의 `CM_SeatedDuel`이 실제 활성 카메라가 되는 것을 확인했다. Main Camera의 위치·회전·FOV·Near Clip을 유지했고, HDR와 카메라 후처리 및 URP Global Volume 평가가 활성 상태였다. 렌더 카메라와 AudioListener는 각각 하나이며 누락 스크립트는 없었다.

실제 Play 모드 게임 시간 18.00초와 연속 렌더 50장을 기록했다. 형광등 강도는 `0.6092~0.8726`으로 변했고 테이블 키 라이트는 일정했다. 조명·연출 그룹 외 기존 카메라·모델·본 269개의 월드 자세를 유지했다. 세부 결과는 `Horror-validation.txt`와 `Placement-validation.txt`가 기준이다.

### 인디 화면 질감 조정

그레인을 Thin2로 바꾸고 강도를 0.26으로 조정했다. 카메라의 출력 Dithering도 활성화했다. 검정 레벨·대비·화이트밸런스를 함께 조정하여 조명 색과 저폴리 윤곽을 유지하면서 살짝 바랜 필름 느낌을 더했다. 효과는 `PizzeriaHorror.asset`에 저장되며 `HorrorSceneTools.Apply` 및 전체 씬 재생성에도 같은 값이 적용된다.

이 버전은 실제 Play 게임 시간 18초 동안 확인했고, 게임 시간 3~7초 구간에서 질감 확인용 프레임 46장을 캡처했다. 셰이더 준비 후 약 0.08초 간격으로 캡처하며, 실제 간격은 `output/horror-atmosphere/frame-times.txt`에 기록한다. URP에서 평가된 Thin2·강도 0.26·Response 0.60과 Lift·Dithering 활성 상태를 검증했다.

무손실 애니메이션 WebP에는 실제 캡처 46장과 4.026초가 들어 있다. 정지한 테이블 영역에서도 인접 프레임의 평균 차이가 8비트 기준 1.4049로 측정되어 움직이는 질감을 확인했다. 공용 설정 8개와 두 Idle 클립의 파일 해시는 조정 전과 동일하다.

- 조정 전 씬·프로필·렌더: `output/indie-look/MainScene-before-indie.unity`, `PizzeriaHorror-before-indie.asset`, `Before.png`.
- 조정 후 실제 렌더: `output/indie-look/MainScene-Indie.png`.
- 실제 프레임을 그대로 묶은 움직이는 미리보기: `output/indie-look/Indie-grain-preview.webp`.
- 검증: `output/indie-look/Indie-validation.txt`, `Preview-validation.json`, `preserved-files.json`.

별도의 Unity 실행에서 저장본을 다시 열고 검증했다. 조명 메시를 포함한 Renderer 81개의 재질·셰이더 참조, 남은 아이템 Cube 4개, 두 캐릭터의 Idle Controller와 첫 시작 씬 등록이 유지되었다. 결과는 `output/scene-layout/MainScene-validation.txt`에 있다.

기존 `SampleScene.unity`, `URPProjectSettings.asset`, `QualitySettings.asset`, `EditorSettings.asset`, `PC_RPAsset.asset`, `PC_Renderer.asset`, `DefaultVolumeProfile.asset`, `SampleSceneProfile.asset`의 SHA-256은 작업 전 기록과 동일했다. 공용 렌더 파이프라인·프로필을 수정하지 않고 MainScene의 전용 Volume으로 연출을 구성했다.

## 압력계 과도한 밝기 조정

압력계 전용 보조광 `Horror_Gauge_Readability`를 강도 0.16에서 0.035로, 범위를 1.1m에서 0.8m로 줄였다. 다이얼의 기본색은 (0.56, 0.54, 0.44)에서 (0.42, 0.405, 0.33), 안쪽 테두리는 (0.49, 0.48, 0.40)에서 (0.36, 0.35, 0.29)로 낮췄다. 두 재질의 직접광 반사 하이라이트와 환경 반사를 끄고 거칠게 조정했다. 눈금·숫자·바늘과 중앙 콜라 조명·카메라 노출은 유지한다.

재적용은 `COLA_COLA > Reduce Gauge Glare` 메뉴다. 같은 값은 조명·재질 생성 도구에도 반영하여 다시 생성해도 유지한다. 검증은 프리뷰 캡처 없이 Play 모드에서 수행하고 `output/gauge-focus/Glare-playmode-validation.txt`와 `unity-glare-playmode.log`에 기록한다. 자동 검사는 조명·재질·바인딩·입력 상태를 확인하며 실제 Game 뷰의 시각적 확인을 대신하지 않는다.

## 실제 Play 화면의 콘솔 백색 번짐 수정

사용자가 제공한 실제 Play 화면에서 압력계 아래 콘솔과 버튼 주변에 큰 백색 번짐이 남는 것을 확인했다. 앞선 다이얼 재질 조정에 더해 `Horror_Gauge_Readability`를 완전히 비활성화하고 강도를 0으로 저장한다. 콘솔·키캡·숫자에 사용되는 7개 재질 모두 직접광 하이라이트와 환경 반사를 끄고 발광을 0으로 고정한다. 크림색 키는 (0.81, 0.75, 0.59)에서 (0.56, 0.52, 0.40)으로 낮추며 금속성과 매끄러움도 줄인다.

MainScene 전용 Volume의 Bloom 강도는 0, HDR 입력 clamp는 2로 저장한다. 그레인·비네트·색 보정·카메라 노출은 유지한다. 조명·콘솔 생성 도구에도 동일하게 적용한다. 현재 값은 위의 과거 Bloom 0.20 및 압력계 보조광 0.035 기록을 대체한다.

`Reduce Gauge Glare`는 Play에서 실제 사용 중인 콘솔 재질과 평가된 Volume stack의 Bloom=0, 보조광 비활성 상태, 압력 예측 선택을 검사한 다음 기본 대전 검증을 수행한다. 이 검사는 별도 카메라나 프리뷰 렌더를 생성하지 않는다. 실제 Game 뷰의 수정 후 모습은 자동 상태 검사만으로 시각 확인했다고 표현하지 않는다.

검증 환경 메모: 사용 중인 원본 Unity를 유지하기 위해 임시 프로젝트 복사본에서 Play 검사를 실행한다. 화면을 그리지 않는 배치 환경에서는 VolumeManager의 렌더링 스택이 초기화되지 않을 수 있으므로 이때는 실제 활성 Volume의 런타임 프로필을 검사하고, 렌더링 스택이 있으면 그 평가값도 검사한다. 렌더링 스택 없이 화면을 시각 검증했다고 보고하지 않는다. 임시 복사본은 검증 후 삭제한다.
