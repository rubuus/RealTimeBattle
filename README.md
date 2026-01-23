# RealTimeBattle

IOCP 기반 C++ 실시간 1:1 대전 서버  
Unity 클라이언트 + C# API 서버와 연동되는 게임 서버 포트폴리오 프로젝트

## System Architecture

- Unity Client (C#)
- C++ IOCP Game Server (번외 : C# TCP 기반 SOCKET Server)
- ASP.NET Core API Server (JWT Token Verify)
- MySQL (User Data, Battle Record)

## How to Run

### 1. C++ IOCP Server

- Visual Studio 2022
- x64 / Release
- 실행 전 포트 설정 필요 (기본 7777)

### 2. ASP.NET Core API Server

- .NET 9
- 기본 실행 주소:
  - http://localhost:5146
  - https://localhost:7170
- `dotnet run` 실행

### 3. Unity Client

- `SocketClient.cs`의 `Start()` 함수에서
  - C++ 서버 (CppConnect) 또는 C# (CSharpConnect) 서버 선택 가능
- 서버 주소 / 포트 설정 후 실행

### 4. Bot Test

- 실행 시 봇 수 인자 전달
- 예:
  {exe 경로} BotTest.exe 127.0.0.1 7777 {봇 개수 (기본 1000)}

## Game Scene Structure

- Boot
- Login
- Lobby
- Matching
- Battle
- Result

## Game Lifecycle

### 1. Client Boot

- 게임 실행 시 공통 Singleton 객체 초기화
- 설정 로드 후 Login Scene으로 전환
- API / Audio / Scene / UserData 관리 객체 준비

### 2. Login Scene

- 로그인 및 회원 가입 가능
- 로그인 성공 시, JWT Token을 발행 및 Lobby Scene 이동

### 3. Lobby Scene

- Record, Notice, Setting, User 등 다양한 Panel로 UI 구성
- Socket Server 접속 시도
- JWT Token 검증 후 게임 서버 세션 생성

### 4. Matching Scene

- User / Enemy 데이터 로드 및 화면 업데이트
- Socket Server 매칭 완료 신호 대기
- 매칭 완료 시 Battle Scene 진입

### 5. Battle Scene

- Side 값 기준으로 스폰 위치 설정 후, Player 객체 생성
- Socket Server 패킷 기준으로 게임 상태 동기화
- 결과 패킷 수신 시 Result Scene 이동

### 6. Result Scene

- 로컬에서 게임 결과 즉시 반영
- 이후 Socket Server에서는 Room Close 비동기 처리
- Socket Server에서 API Server로 전적 기록 Request