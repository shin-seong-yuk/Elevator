# Steam 개발 빌드 실행

현재 App ID는 개발용 **480**입니다. 정식 Elevator 출시용 App ID가 아니며 Steam에서는 Spacewar로 표시될 수 있습니다.

1. 각 PC에서 서로 다른 Steam 계정으로 로그인합니다.
2. `Builds/Windows` 폴더 전체를 복사하고 `Elevator.exe`를 실행합니다. exe만 복사하면 실행되지 않습니다.
3. MULTIPLAYER에서 친구용 방 또는 공개 방을 만듭니다. 친구용 방에서는 INVITE FRIENDS로 Steam 초대 창을 엽니다. 공개 방은 FIND ROOMS에서 찾습니다.
4. 엘리베이터 안에서 대기합니다. 참가자는 R로 준비/취소하고, 다른 참가자가 모두 준비하면 방장이 R로 시작합니다. 초대 및 퇴장은 ESC 대기실 메뉴에서 선택합니다.

Steam 비밀번호를 게임에 입력하지 않습니다. 이미 로그인된 Steam 클라이언트의 계정을 사용합니다. Steam이 꺼져 있다면 실행·로그인 후 CONNECT STEAM을 누르세요. 싱글 플레이는 Steam 없이도 실행됩니다.

공개 방 검색은 `elevator-steam-5` 버전 태그로 다른 480 테스트 게임과 구분합니다. 친구도 같은 빌드를 실행해야 합니다. 게임 진행 중에는 신규 참가를 막습니다. Steam 오버레이 초대는 Windows 실행 파일에서 확인하세요.

## 방장 이전

- Steam 방에서 방장이 나가거나 프로세스가 종료되면 남은 사람 중 체크포인트를 받은 사람을 우선해 무작위로 새 방장을 선택합니다.
- 약 0.5초마다 상태를 저장·전송합니다. 정상 나가기는 마지막 상태를 전송하고 확인을 기다린 뒤 이전합니다.
- 재연결 화면 동안 씬과 네트워크 권한을 다시 구성합니다. 현재 층·이벤트 진행·캐릭터 위치와 속도·무기와 남은 수명·악력·잡기 연결·벽과 바닥의 파손 상태를 복구합니다.
- 떠난 사람의 캐릭터는 제거합니다. 남은 사람은 자신의 원래 캐릭터로 돌아옵니다. 마지막 생존자 규칙은 유지됩니다.
- 강제 종료 시 마지막 수신 상태까지 조금 되돌아갈 수 있습니다. 연결 장애 감지와 새 방장 접속에는 시간이 필요하므로 프레임 단위로 끊김 없는 전환은 아닙니다.
- LAN / DIRECT IP는 기존 직접 연결 방식이며 자동 방장 이전 대상이 아닙니다.

## 설정과 코드

- `Assets/Resources/SteamConfig.json`: App ID와 호환 빌드 태그.
- 프로젝트 루트 `steam_appid.txt`: Unity 개발 실행용 480.
- Windows 빌드 시 `Builds/Windows/steam_appid.txt`를 설정값으로 생성합니다.
- 정식 App ID 발급 후 설정과 개발용 txt를 교체하고 다시 빌드합니다. 정식 Steam 배포의 실행 설정 및 개발용 txt 제외는 Steamworks 배포 절차에 맞춰 처리해야 합니다.
- `Packages/com.rlabrecque.steamworks.net`: Steamworks.NET 2025.164.1, MIT 라이선스 포함.
- `SteamSession`: 로그인 초기화, 로비, 초대, 방장 선출 및 체크포인트 전송.
- `SteamTransport`: Steam Networking Messages와 NGO 사이의 전송 계층. Steam 송신자, 방 소속, 전환 세대와 접속 nonce를 검사합니다.
- `MigrationSnapshot`: 공통 게임 상태를 값과 안정적인 개체 식별자로 저장·복구합니다. 별도의 싱글용 이벤트 복제는 없습니다.

## 검증 범위

`TestResults/steam-live-smoke.txt`는 실제 로그인된 Steam의 초기화, App ID 480 방 생성, NGO 방장/캐릭터 생성 및 방 나가기 결과입니다.

`TestResults/steam-migration-smoke.txt`는 13종 이벤트의 저장·복구, 무작위 후임 선택, 악력·잡기·무기 수명·파손·진행 시간 복원 결과입니다.

여러 프로세스로 수행하는 LAN 체크포인트 재접속 검사는 NGO 권한 교체와 상태 동기화를 검증합니다. 이것만으로 다른 Steam 계정 간 Relay, 친구 초대 수락, Steam 방장 선출까지 실증한 것으로 보지 않습니다. 해당 항목은 서로 다른 Steam 계정과 PC에서 별도 확인이 필요합니다.
