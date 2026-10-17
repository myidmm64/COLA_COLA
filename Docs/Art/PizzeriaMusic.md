# After Hours — 피자리아 대전 BGM

80 BPM, D를 중심으로 한 낮은 드론과 해결되지 않는 반음, 낡은 차임 멜로디의 48초 루프다. 느린 두 번의 심장 박동과 기계적인 짧은 음을 별도 트랙으로 만들었다. 외부 음원이나 기존 게임 음악을 사용하지 않은 프로젝트용 합성 작곡이다.

- `Assets/_COLA_COLA/Resources/Audio/Pizzeria_Night_Base.wav`: 낮은 공간감과 드문 멜로디.
- `Assets/_COLA_COLA/Resources/Audio/Pizzeria_Night_Tension.wav`: 맥박과 불안한 반복음.
- `Tools/Audio/compose_pizzeria_bgm.py`: 고정 난수·순환 딜레이·주기 파형으로 두 원본 WAV를 다시 생성하는 작곡 소스. NumPy 사용.
- `ColaDuelMusic`: MainScene 대전 시작 시 자동 설치. 두 스테레오 트랙을 같은 DSP 시각에 시작하고 계속 반복한다. 새 콜라·재시작에서 곡을 처음부터 반복 재생하거나 소스를 중복 생성하지 않는다.

평소에는 기본 트랙만 낮게 들린다. 공개 한계 대비 압력이 높아지면 약 1.25초 동안 긴장 트랙이 점진적으로 섞인다. 폭발 직전에는 음악이 낮아지고, 폭발 때는 약 10%로 내려가 폭발음을 위한 공간을 만든다. 암전에는 낮게 유지하고 화면 페이드인에 맞춰 회복한다. 승패 화면에서는 전체 음악을 더 낮춘다. 전역 AudioListener 볼륨이나 효과음의 음량은 건드리지 않는다.

`ColaDuelMusic.musicVolume`으로 전체 BGM을 조절할 수 있다. 기본값은 0.34. 두 WAV는 22,050Hz·스테레오·16bit이며 최댓값 약 0.53으로 여유를 둔다. 모든 음원 파일은 실제 게임용 에셋이고 별도 프리뷰나 임시 녹음은 만들지 않는다.

Play 검증은 실제 AudioSource 재생·샘플 위치·루프 경계·두 트랙 동기화·압력 믹스·폭발 시 음량 하강과 페이드 복구·재시작 시 중복 방지를 검사한다. 결과는 `output/verification/latest/music/Playmode-validation.txt`에 기록한다. 자동 검사는 스피커 청취를 대신하지 않는다.
