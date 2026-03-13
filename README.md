# RealTimeBattle

IOCP-based C++ real-time 1:1 battle server  
A game server portfolio project integrated with a  
Unity client and an ASP.NET Core API server (C#).

## Overview

In addition to the main server implementation,  
this project also includes a **C# TCP-based Socket Server**  
implemented for comparison and learning purposes.

The C# server was developed by first implementing a  
functional prototype, and later refactoring it into  
a C++ IOCP server.

## Packet Processing (C# Server)

In the C# server, the following packet processing method  
is intentionally used.

- Packet Object → JSON → Byte (Send)
- Byte → JSON → Packet Object (Recv)

JSON-based serialization/deserialization in a real-time  
socket server environment is inefficient in terms of performance,  
however this approach was intentionally adopted for:

- Comparing packet processing methods between the C# server and the C++ IOCP server
- Understanding serialization costs and structural differences
- Learning implementation of a C# network server

## System Architecture (IOCP)

- Unity Client (C#)
- C++ IOCP Game Server
- ASP.NET Core API Server (JWT Token Verify)
- MySQL (User Data, Battle Record)

## How to Run

### 1. C++ IOCP Server

- Visual Studio 2022
- x64 / Release
- Port configuration required before execution (default: 7777)

### 2. ASP.NET Core API Server

- .NET 9
- Default execution addresses:
  - http://localhost:5146
  - https://localhost:7170
- Run with: dotnet run

### 3. Unity Client

In the `Start()` function of `SocketClient.cs`

- Either the C++ server (`CppConnect`) or the C# server (`CSharpConnect`) can be selected

Run after setting the server address and port.

### 4. Database

- MySQL (Pomelo Provider)
- DB connection information can be configured in `appsettings.json`
- Default configuration is based on a local development environment
- If the DB is not prepared, some features (login / battle record storage) may be limited

### 5. Bot Test (Load Test)

Pass the bot count as an argument when executing.

Example: {exe path} BotTest.exe 127.0.0.1 7777 {bot count (default 1000)}

## Game Scene Structure

- Boot
- Login
- Lobby
- Matching
- Battle
- Result

## Game Lifecycle

### 1. Client Boot

- Initialize common Singleton objects when the game starts
- Load configuration and move to the Login Scene
- Prepare management objects for API / Audio / Scene / UserData

### 2. Login Scene

- Login and registration available
- After successful login, a JWT Token is issued and the Lobby Scene is entered

### 3. Lobby Scene

- UI composed of various panels such as Record, Notice, Setting, and User
- Attempt connection to the Socket Server
- After JWT Token verification, a game server session is created

### 4. Matching Scene

- Load User / Enemy data and update the screen
- Wait for the matching completion signal from the Socket Server
- Enter the Battle Scene when matching is completed

### 5. Battle Scene

- Spawn positions are set based on the Side value, and Player objects are created
- Game state is synchronized based on packets from the Socket Server
- Move to the Result Scene when the result packet is received

### 6. Result Scene

- Game results are immediately reflected locally
- Afterwards, the Socket Server processes Room Close asynchronously
- The Socket Server sends a battle record request to the API Server

## Notes

- The Unity Client uses camelCase for internal variable naming
- The C# API Server and Socket Server follow .NET conventions
- The C++ IOCP Server follows common C++ server coding practices

