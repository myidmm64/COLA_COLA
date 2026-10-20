# 깨끗한 사진 기반 저해상도 재질

## 현재 적용

더러운 느낌을 없애고 깨끗한 실제 재질의 미세한 결을 표현한다. 녹, 부식, 때, 손때, 종이 얼룩, 페인트 벗겨짐, 깊은 긁힘은 재질 이미지에서 제거했다. 저해상도 사진 질감과 Point 샘플링은 유지한다.

- `Assets/_COLA_COLA/Resources/COLA_COLA_SurfaceAtlas.png`: 기존 파일을 깨끗한 4열×2행 컬러 아틀라스로 교체했다. GUID는 유지하므로 연결된 압력계·아이템·벨·트레이·콜라 몸통에 함께 적용된다. 게임 임포트는 512×256, 재질당 약 128×128, sRGB/Point/비압축/밉맵이다.
- 위쪽 행: 깨끗한 도장면, 새틴 스테인리스, 새 종이, 마감된 나무. 아래쪽 행: 깨끗한 PET, 고무, 직물, 신선한 빵의 결.
- 아틀라스의 깨끗한 평균색에 맞춰 보정값을 바꿨다. 칸 경계 여백을 8텍셀로 늘려 플라스틱 사진 가장자리가 반복되지 않도록 했다.
- `Assets/_COLA_COLA/Resources/COLA_COLA_BottlePrint.png`: 기존 콜라 UV 이미지를 내장 imagegen으로 편집해 새로 만든 깨끗한 빨간 뚜껑/인쇄 라벨용 텍스처. 게임 임포트는 512×512, sRGB/Point/비압축/밉맵이다.
- 콜라 뚜껑은 사출 플라스틱의 미세한 입자와 링/세로 골을, 라벨은 깨끗한 인쇄지의 섬유와 선명한 빨강/아이보리 인쇄를 사용한다. 기존 COLA / CLASSIC / 500 ML 디자인과 UV 배치를 따르도록 생성했다.
- MainScene이 사용하는 `Blockout/Cola_Red_Plastic_Cap_f658da38.mat`와 `Cola_Printed_Label_e6ee7e98.mat`의 실제 BaseMap/MainTex를 새 이미지로 변경했다. 라벨 사진 보조 기여도 .25, 뚜껑 .35로 새 인쇄/플라스틱 이미지를 중심으로 사용한다. 원본 수입 에셋은 보존한다.
- 콜라 몸통/내용물은 이전 원본 BaseMap과 새로 깨끗해진 사진 재질을 계속 사용한다. 라벨/뚜껑용 새 아틀라스의 나머지 UV 영역은 몸통에 적용하지 않는다.
- 사진 재질을 쓰는 저장 재질은 총 28개. 런타임 아이템/트레이는 공용 재질 설정을 따른다.
- 직원 의상과 이전 회색조 경로도 갈색 얼룩·밑칠·긁힘을 없앴다. `COLA_COLA_PropWear.png`는 기존 재질의 아주 약한 중성 입자(최대 수% 명암) 소스로만 남아 있으므로 아직 사용 중이다.
- `MainSceneTools`도 몸통뿐 아니라 뚜껑/라벨을 공용 설정으로 다시 생성하도록 변경했다.
- 보스/플레이어 팔의 원본 텍스처, 모델 배치, 방 조명과 카메라 효과는 바꾸지 않았다.

## 생성 기록

내장 imagegen 편집을 사용했다. CLI/API 대체 경로는 사용하지 않았다. 선택한 이미지 두 장을 프로젝트에 복사했고, 교체된 재질 아틀라스의 중복/백업 파일은 프로젝트에 만들지 않았다.

재질 아틀라스 편집 대상: 기존 `COLA_COLA_SurfaceAtlas.png`.

최종 프롬프트:

> Edit this game material atlas to remove ALL dirt, aging and damage. Preserve EXACT 4-column by 2-row layout and image aspect ratio, each tile filling precisely the same area. Keep photographic REAL material surface detail and natural subtle color variation, intended to be downsampled to 128px per tile for a low-resolution PS1 game. Clean well-kept pizzeria equipment, not abandoned horror props. TOP ROW: 1 intact clean pale gray enamel with a finely textured painted finish, NO chips, NO rust; 2 clean satin stainless steel, fine uniform brushed grain, NO corrosion, NO tarnish, NO dirty spots, NO gouges; 3 clean warm-white new paper, delicate natural paper fibers, NO stains, NO creases, NO yellowing; 4 clean sealed medium-brown wood, regular fine natural wood grain, NO grime, NO damaged fibers. BOTTOM ROW: 5 clean gray PET plastic, extremely fine molding texture and restrained natural soft surface variation, NO fingerprints, NO scratches, NO water rings, NO dirty droplets; 6 clean charcoal rubber with fine even microtexture, NO dust, NO scratches; 7 clean gray-beige cotton fabric with regular fine weave and NO stains; 8 fresh evenly golden baked bread texture with fine natural pores, NO burnt black bits, NO greasy patches. Photographic diffuse albedo, neutral even lighting, no directional highlight, no cast shadow, no bevel or object silhouette, NO text or borders or gutters. Rich natural surface detail without any dirty speckling. Do not replace the materials with flat single-color vector fills.

콜라 편집 대상: `Assets/2.Assets/01_Cola_Bottle/Textures/cola_bottle_atlas_256.png`. 원본을 덮어쓰지 않고 `COLA_COLA_BottlePrint.png`로 저장했다.

최종 프롬프트:

> Edit this EXACT square cola bottle UV texture atlas. This is an in-game texture asset, NOT a bottle render. Preserve ALL UV island positions, island boundaries, proportions, the square aspect ratio, and all lettering placement from the input. Its source layout is a 256x256 atlas: top-left label approximately x0-128 y0-64; top-middle red cap circle x128-192 y0-64; top-right red cap ribbing x192-256 y0-64; thin gray-green separator y64-80; middle liquid/plastic swatches y80-176; bottom dark brown base region y176-256. Do not move any island or enlarge the label into the cap areas. Replace the surfaces with clean photographic low-resolution material detail suitable for a PS1 game, not dirt. LABEL: new clean deep-red printed paper with delicate paper fibers, crisp warm-ivory ink, preserve the exact words 'COLA', 'CLASSIC', '500 ML', their readable original placement, and the thin cream graphic rules. No new symbols or writing. No staining, fading, tears or worn printing. CAP circle and ribbing: clean saturated red injection-molded plastic with fine regular micrograin, subtle natural molded color variation and clear ring/rib detail. No scratches or aging. BODY swatches: clean dark amber-brown cola with soft natural tonal variation, clean pale gray-green PET plastic, no grime, no fingerprints, no water stains, no floating debris. Preserve the original bottom circular details in place but clean and neatly molded. Retain the retro photographic game quality and slight low-resolution softness of the source. Do not add cast shadows, bright specular hotspots, bevels outside the existing islands, grid lines, borders, captions, padding or a bottle silhouette. Preserve the atlas layout more strictly than any style detail.

## 확인 범위

사용자가 화면 캡처를 원하지 않았고 검증을 별도로 요청하지 않았으므로, 이번 작업에서는 Play 진입·검증 브리지·컴파일 검사·화면 캡처를 실행하지 않았다. 생성된 텍스처 이미지 자체를 확인하고 에셋/재질/코드를 수정했다. 이전 `output/verification/latest/` 보고서는 이번 깨끗한 재질과 새 뚜껑/라벨의 검증 근거가 아니다. 새 프리뷰나 임시 빌드는 만들지 않았다.
