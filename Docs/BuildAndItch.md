# JUST SHAKE. 빌드와 itch.io 업로드

## 결과물

- Windows: `Builds/JUST_SHAKE_Windows_x64.zip`
- HTML5/WebGL: `Builds/JUST_SHAKE_itchio.zip`
- 압축 전 원본: `Builds/Windows/`, `Builds/WebGL/`
- 최신 빌드 기록: `output/build/latest/Windows.md`, `WebGL.md`

## itch.io 웹 버전

1. 게임 페이지의 **Kind of project**를 **HTML**로 설정한다.
2. `JUST_SHAKE_itchio.zip`을 업로드한다.
3. 해당 파일의 **This file will be played in the browser**를 체크한다.
4. **Embed in page**, **1280 × 720**으로 설정한다. 게임 자체의 전체화면 버튼을 사용하며, 브라우저 전체화면 해제 시 주변 프로젝트 페이지가 다시 보이도록 한다.
5. 데스크톱 키보드·마우스 게임이므로 모바일 지원으로 표시하지 않는다.

ZIP을 열었을 때 맨 위에 `index.html`과 `Build/`가 있다. 부모 폴더째 한 번 더 압축하지 않는다. 압축 파일을 풀어 `file://`로 여는 방식이 아니라 itch.io의 HTML5 호스팅으로 실행한다.

템플릿은 iframe 크기에 맞춰 캔버스를 표시하며, 우클릭 기본 메뉴를 차단해 아이템 반납 입력을 사용할 수 있도록 한다. 웹에서는 데스크톱 해상도 목록 대신 현재 캔버스 크기를 사용한다. 브라우저 전체화면은 버튼을 눌러 요청하며, 웹 버전의 실행 불가능한 게임 종료 메뉴는 숨긴다.

Gzip + JavaScript 압축 해제 대체 기능을 사용하므로 별도 압축 응답 헤더 설정에 의존하지 않는다. 스레드는 사용하지 않아 SharedArrayBuffer 활성화가 필요하지 않다. 브라우저 정책에 따라 소리는 첫 클릭 이후 재생될 수 있다.

## Windows 버전

같은 페이지에 Windows ZIP을 추가할 수 있다. 이 파일은 브라우저 실행 항목으로 체크하지 않고 Windows 다운로드로 표시한다. 압축을 풀고 `JUST SHAKE.exe`를 실행한다. `JUST SHAKE_Data`, `UnityPlayer.dll` 등을 포함한 폴더 전체를 함께 배포한다.

해상도는 주모니터와 호환되는 실제 지원 모드만 내림차순으로 표시한다. 지원되지 않는 기존 저장 해상도는 목록에 끼워 넣지 않고 지원값으로 보정한다. 새 기본값은 최대 지원 해상도·전체화면·영어이며 이후 선택은 저장된다. Windows 지원 모드 조회는 읽기 전용이며 OS의 해상도를 임의로 변경하지 않는다.

## 범위

빌드 성공은 컴파일·에셋 패키징 결과다. 별도 지시가 없는 한 Unity Play 테스트, 브라우저 게임 실행, itch.io 게시 및 Windows 실행 파일 플레이 검사는 수행하지 않는다.

자료: [itch.io HTML5 업로드 규칙](https://itch.io/docs/creators/html5), [Unity 웹 배포와 압축](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/webgl/building-distribution/deploying), [Windows의 모니터 호환 모드 조회](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw).

## 웹 선명도 수정

WebGL 기본 화질은 PC(렌더 스케일 1.0)로 변경했고 템플릿의 DPR 1.5 제한을 제거했다. 현재 ZIP은 Unity에서 재빌드한 PC 화질(렌더 스케일 1.0)을 사용한다. 템플릿은 화면의 실제 DPR을 따르며 이전 0.8 렌더 스케일 보정은 제거했다. 실행 검증은 수행하지 않았다. itch.io에서는 페이지 내 실행(1280 × 720) 후 게임의 전체화면 버튼을 사용하며 기존 웹 ZIP을 교체해야 한다.

웹 버전은 오른쪽 위 설정 버튼과 왼쪽 아래 능력·업적 버튼을 제공한다. 키보드 ESC·TAB과 같은 게임 기능을 호출하며 Windows 빌드에는 버튼이 추가되지 않는다.
