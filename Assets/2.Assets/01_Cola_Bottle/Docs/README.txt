PSX COLA BOTTLE — 게임용 플라스틱 콜라병

500ml 스타일의 콜라 PET 병을 새로 만들었습니다.
PSX에 어울리는 각진 실루엣, 부드러운 원통 음영, 저해상도 표면 질감을 사용합니다.
실제 브랜드 대신 COLA / CLASSIC / 500 ML 라벨을 넣었습니다.

다운로드 파일
  cola_bottle.fbx                   기본형 FBX 7.4 ASCII
  cola_bottle.glb                   기본형 GLB, 텍스처 내장
  cola_bottle.obj                   기본형 OBJ
  cola_bottle_pressurized.fbx       부푼 형태 FBX
  cola_bottle_pressurized.glb       Pressure=1로 시작하는 GLB
  cola_bottle_pressurized.obj       부푼 형태 OBJ
  cola_bottle.mtl                   두 OBJ에서 공통 사용
  cola_bottle_atlas_256.png         모든 재질이 공통 사용하는 PNG
  preview.png / preview_sheet.png  내보낸 GLB를 렌더링한 이미지
  cap_separated_preview.png        분리 가능한 뚜껑의 예시 보기
  LICENSE.txt                      이 패키지의 사용 조건

FBX 및 OBJ는 PNG 텍스처와 함께 가져오세요.
OBJ는 부품 그룹을 보존하지만 피벗·빈 노드·Pressure 변형을 포함하지 않습니다.

부품과 게임 연결
  Cola_Bottle_PSX : 전체 병의 루트. 바닥 중심 피벗, 단위 미터, 위 +Y, 정면 +Z.
  Bottle_Shell   : 반투명 플라스틱 병. 입구가 열려 있고 입구 림과 안쪽 목이 있습니다.
  Cola_Liquid    : 짙은 갈색 콜라 내용물. 윗면과 약간의 빈 공간을 표현합니다.
  Label          : 붉은 픽셀 라벨. 부풀림에 따라 병 몸통을 따라갑니다.
  Cap_Pivot      : 뚜껑을 돌리거나 날리는 기준점. 기본 위치 (0, 0.193, 0)m.
  Cap            : Cap_Pivot의 자식 메시. 로컬 원점은 뚜껑 하단 중심입니다.
  Tamper_Ring    : 뚜껑을 분리한 뒤 목에 남는 봉인 링.
  Burst_Origin   : 병 입구 중심 (0, 0.201, 0)m. 분출 방향은 로컬 +Y입니다.

흔들어서 터지는 연출
  GLB에는 Bottle_Shell, Cola_Liquid, Label에 각각 Pressure 변형이 있습니다.
  세 메시의 Pressure 값을 함께 0에서 1로 올리면 몸통과 라벨이 부풀어 오릅니다.
  뚜껑과 목, 바닥은 고정되며 액체 표면은 조금 낮아집니다.
  FBX에는 기본형과 부푼 형태를 별도 모델로 제공합니다. FBX에 BlendShape는 없습니다.
  FBX 두 상태의 각 부품은 같은 정점 구성과 UV를 사용합니다.
  터지는 순간 Cap_Pivot을 이동하거나 루트에서 분리하고 물리를 적용할 수 있습니다.
  분출 파티클은 Burst_Origin에 연결하세요.
  이 패키지는 모델 에셋입니다. 흔들기 입력, 파열 시뮬레이션, 액체 분출 파티클,
  물리 스크립트 또는 실행 가능한 폭발 애니메이션은 포함하지 않습니다.
  부푼 형태는 게임 연출용이며 실제 압력/액체 부피를 물리적으로 계산한 결과가 아닙니다.

재질 설정
  픽셀 느낌: PNG 필터를 Point 또는 Nearest로 사용하세요.
  라벨/뚜껑/내용물은 불투명 재질입니다.
  PET_Plastic은 Alpha 약 0.15, Metallic 0, Roughness 약 0.56으로 만듭니다.
  GLB에는 Alpha Blend와 양면 설정이 포함되어 있습니다.
  FBX에서 병이 불투명하면 PET_Plastic 재질을 Transparent로 바꾸고 양면 표시를 켜세요.
  투명 병 껍질은 깊이 쓰기와 그림자 투사를 끄는 구성이 미리보기에 사용되었습니다.
  투명 정렬 방식은 엔진마다 다르므로 실제 장면에서 확인하세요.
  기본 크기는 높이 약 21.1cm, 몸통 지름 약 6.5cm입니다.

사양 및 검증
  삼각형 합계 1,044개: 껍질 516 / 내용물 312 / 라벨 24 / 뚜껑 96 / 봉인 링 96.
  메시 5개, 재질 4개, 256×256 PNG 텍스처 한 장.
  GLB 두 파일: Khronos glTF Validator 오류 0, 경고 0.
  FBX 두 파일: Three.js FBXLoader로 모델, UV, 법선, 투명도, 텍스처 및 피벗 확인.
  내보낸 FBX와 GLB를 텍스처와 함께 렌더링하여 확인했습니다.
  Unity/Unreal 실제 프로젝트에서의 가져오기는 별도로 실행하지 않았습니다.

스타일 참고
  https://jashi-psx.itch.io/simple-character-psx-asset
  https://elbolilloduro.itch.io/pizzeria-psx
  참조 패키지의 파일은 사용하거나 포함하지 않았습니다.
