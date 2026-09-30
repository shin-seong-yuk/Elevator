# Elevator

Unity 6000.3.19f1 기반 멀티플레이 물리 파티 게임.

- Unity Hub에서 이 저장소 폴더를 프로젝트로 추가합니다.
- `Assets/Elevator/Scenes/Elevator.unity`를 열고 Play를 누릅니다.
- 싱글: 사람 1명 + AI 3/5/7명.
- 멀티: Steam 로그인 후 방 생성/참가. 엘리베이터 대기실에서 참가자는 **R**로 준비/취소, 모두 준비하면 방장이 **R**로 시작합니다.
- 개발용 Steam App ID는 **480**입니다. 정식 배포 시 본인 App ID로 교체해야 합니다.
- Windows 빌드: Unity 메뉴 `Elevator/Build Windows Player`.

[플레이 및 개발 안내](Docs/README.ko.md) · [Steam 연동 안내](Docs/STEAM.ko.md)

`Library`, `Temp`, `Builds`, 로컬 로그는 Git에 포함하지 않습니다. Unity 에셋과 `.meta`, `Packages`, `ProjectSettings`는 함께 버전 관리합니다.
