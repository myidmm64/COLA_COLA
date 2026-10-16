# COLA_COLA 테스트 아이템 배치

메인 씬에서 아이템 보유 공간을 판단하기 위한 배치다. 플레이어와 보스에게 같은 로우폴리 소품 6종을 하나씩 배치해 총 12개를 보여준다. 아이템 효과나 최종 아이템 선정은 포함하지 않는다.

| 테스트 모델 | 형태 | 대략적인 크기 |
|---|---|---|
| 테스트 아이템 1 | 붉은 회전 손잡이가 달린 황동 배출 밸브 | 폭 14cm, 높이 11cm |
| 테스트 아이템 2 | 밀봉 가장자리와 흰 마개가 있는 청록색 냉각팩 | 폭 17cm, 높이 7cm |
| 테스트 아이템 3 | 피자 그림과 인쇄선이 있는 접힌 종이 쿠폰 | 폭 15cm, 높이 5cm |
| 테스트 아이템 4 | 양끝을 비튼 빨강·크림색 포장 사탕 | 폭 14cm, 높이 5cm |
| 테스트 아이템 5 | 홈이 있는 크림색 몸체와 부푼 붉은 윗면의 뚜껑 | 지름 8cm, 높이 7cm |
| 테스트 아이템 6 | 종이 라벨과 검은 스포이트 마개가 있는 갈색 병 | 지름 7cm, 높이 17cm |

모두 별도 메시·재질·프리팹을 사용한다. 같은 프리팹을 양쪽이 공유하며 원본 임포트 에셋은 수정하지 않는다. 테스트 소품에는 클릭 Collider나 효과 스크립트를 붙이지 않았다.

## 배치

- 계층: `COLA_COLA_MainScene/04_TabletopItems/PlayerInventory`, `BossInventory`
- 테이블 오른쪽 앞에는 청록 테두리의 플레이어 쟁반, 뒤에는 붉은 테두리의 보스 쟁반을 둔다. 화면에 붙인 슬롯 UI가 아닌 손잡이와 얕은 테두리가 있는 실제 3D 서빙 쟁반이다.
- 각 쟁반에 3열 × 2행으로 놓는다. 낮은 냉각팩·쿠폰·사탕은 앞줄, 밸브·뚜껑·스포이트 병은 뒷줄이다. 플레이어 쪽은 병을 왼쪽으로 옮겨 뒤쪽 보스 아이템이 가려지는 것을 피한다. 작은 각도 차이로 기계적인 정렬을 완화한다.
- 쟁반 바닥의 월드 Y는 `0.833m`, 아이템 접촉면은 `0.845m`다. 쟁반 사이의 깊이 방향 여유는 `27cm`다.
- 기존 아이템 Cube 4개와 보스의 기존 캔·집게 2개를 교체한다. 현재 아이템 대체용 Cube는 없다. `05_CubePlaceholders` 그룹은 비어 있다.
- 카메라·FOV·테이블·중앙 콜라·단말기·압력계·버튼·캐릭터·Idle·기존 조명 배치는 보존한다. 오른쪽 아이템의 실루엣을 읽을 수 있도록 약한 국소 Spot 보조광을 추가했다. 중앙 콜라 주변과 1·2·3 버튼의 조작 경로를 유지한다.

이 배치는 고정된 16:9 카메라에서 여섯 종류의 크기·높이 차이와 밀집도를 판단하기 위한 것이다. 최종 아이템의 크기나 카메라 변경을 확정한 설계는 아니다.

## 제작·확인

- 메뉴: `COLA_COLA > Place Six Test Items Per Side`
- 생성기: `Assets/Editor/COLACOLA/TestItemModelFactory.cs`
- 부분 배치 도구: `Assets/Editor/COLACOLA/TestItemLayoutSceneTools.cs`
- 프리팹: `Assets/_COLA_COLA/Art/Prefabs/TestItems/`
- 메시·재질: `Assets/_COLA_COLA/Art/Models/TestItems/`, `Assets/_COLA_COLA/Art/Materials/TestItems/`
- 기존 씬 백업: `output/item-layout/MainScene-before-items.unity`
- 고정 카메라 렌더: `output/item-layout/MainScene-Items.png`
- 위에서 본 실제 Unity 렌더: `output/item-layout/Tabletop-Overview.png`
- 개수·테이블 접촉·아이템 간격·프레임 범위 검사: `output/item-layout/Item-layout-validation.txt`
- 같은 카메라의 실제 깊이 가림과 단독 렌더 픽셀 비교: `output/item-layout/Rendered-visibility-validation.txt`
- 무관한 Transform 보존: `output/item-layout/Placement-preservation.txt`
- 설정·기존 장치 프리팹·Idle 파일 보존: `output/item-layout/preserved-files-after.json`

적용 도구는 저장본을 다시 열어 렌더하고 기존 `PressureInteractionSceneTools.Verify`로 실제 Play 모드의 강도 선택·콜라 클릭 확정·중복 입력 방지·폭발 후 병 초기화와 Idle 지속을 확인한다. 결과는 `output/pressure-interaction`에 기록한다. 전체 `MainSceneTools.Build`에도 같은 12개 배치를 연결한다.

최종 실제 카메라 가림 검사에서 12개 소품 모두 단독 렌더 대비 91.1~100%의 픽셀이 보였다. 플레이어 6개는 99.9~100%, 보스 6개는 91.1~100%다. 낮은 소품 밑부분의 쟁반 테두리 가림을 포함한 수치이며, 완전히 가려진 소품은 없다. 무관한 Transform 350개의 배치와 보호 파일 25개를 보존했고, 저장본 재열기·재질 참조·스크립트·기존 조작 검증을 통과했다. 최종 실행 로그는 `output/item-layout/unity-final-validation.log`다.
