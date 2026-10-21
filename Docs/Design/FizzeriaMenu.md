# FIZZERIA 메뉴

메인 메뉴는 실제 피자리아 씬을 사선으로 바라본다. 콜라와 상대의 실루엣, 뒤쪽의 녹색 음료 기계가 보이도록 구도를 잡았다. 메뉴에 들어온 동안만 카메라를 천천히 움직이며, 따뜻한 테이블 조명과 적은 수의 먼지 입자를 사용한다. 왼쪽의 부드러운 그림자는 메뉴 글자를 읽을 공간을 만든다. 플레이를 시작하면 카메라·렌더러·조명 상태를 복원한다.

메뉴 배경의 보스는 정지된 게임 시간과 별도로 기본 IDLE을 재생한다. 작은 시선 변화·손가락 움직임·상체 자세 정리를 섞으며, 메뉴에서 숨긴 압력계와 트레이를 살피는 동작은 제외한다. 메뉴를 닫으면 Animator의 시간·컬링 설정과 덧입힌 포즈를 복원한다.

## 제목

로고 이미지를 사용하지 않고 Noto Sans KR로 FIZZERIA를 직접 표시한다. 제목은 큰 아이보리색 텍스트이며, 한국어 메뉴에도 Noto Sans KR을 기본 적용한다.

## 폰트 후보

| 후보 | 용도와 인상 | 출처 |
| --- | --- | --- |
| Noto Serif KR | 비교 후보. 오래된 식당 인쇄물과 차분한 긴장감 | https://github.com/notofonts/noto-cjk/tree/main/Serif |
| Noto Sans KR | 확정 적용. 읽기 쉬운 담백한 인상 | https://github.com/notofonts/noto-cjk/tree/main/Sans |
| Gowun Batang | 더 가늘고 부드러운 인디 분위기 | https://github.com/google/fonts/tree/main/ofl/gowunbatang |

폰트 원본과 OFL 1.1 라이선스는 `Assets/_COLA_COLA/Resources/Fonts/`에 보관한다. 일본어·중국어 간체·번체는 각 언어에 맞는 Noto Sans CJK 파일을 사용한다. 일반 게임 UI는 가독성을 위해 Noto Sans를 사용한다. 메인 메뉴의 한국어 후보만 별도로 비교한다.

## 번역

원본은 `Assets/Localization/COLA_COLA_Translations.xlsx`이며, 런타임은 같은 내용의 `Resources/Localization/Strings.json`을 읽는다. 한국어·영어·일본어·중국어 간체·중국어 번체·프랑스어·독일어·스페인어·브라질 포르투갈어·러시아어 10개 언어를 채웠다. 아이템·능력·업적·메뉴·디버그 표기·효과 문구를 포함한다. `{0}` 등의 치환 값과 수치는 유지한다. 번역은 초안이며 원어민 감수를 수행했다고 주장하지 않는다.

실제 Game 뷰의 최신 폰트 비교 화면과 기록은 `output/verification/latest/menu-design/`에 보관한다.
