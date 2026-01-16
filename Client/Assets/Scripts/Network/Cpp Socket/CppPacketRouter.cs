using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * CppPacketRouter.cs
 * 
 * 역할 :
 * - 패킷 데이터 기반으로 이벤트 생성 및 서버에 패킷 전송 (핸들링)
 * 
 */

public static class CppPacketRouter
{
    public static void Route(ushort packetType, ReadOnlySpan<byte> body)
    {
        var type = (S2C_HeaderType)packetType;

        switch (type)
        {
            case S2C_HeaderType.MATCH_FOUND:
                {
                    var pkt = MemoryMarshal.Read<CppMatchFoundPacket>(body);
                    HandleMatchFound(pkt);
                }
                break;

            case S2C_HeaderType.LOAD_BATTLE:
                HandleLoadBattle();
                break;

            case S2C_HeaderType.PLAYER_STATE:
                {
                    var pkt = MemoryMarshal.Read<CppPlayerStatePacket>(body);
                    HandleState(pkt);
                }
                break;

            case S2C_HeaderType.TAKE_DAMAGE:
                {
                    var pkt = MemoryMarshal.Read<CppDamagePacket>(body);
                    HandleTakeDamage(pkt);
                }
                break;

            case S2C_HeaderType.GAME_TIME:
                {
                    var pkt = MemoryMarshal.Read<CppTimeSyncPacket>(body);
                    HandleTime(pkt);
                }
                break;

            case S2C_HeaderType.GAME_WIN:
                HandleGameWin();
                break;

            case S2C_HeaderType.GAME_LOSE:
                HandleGameLose();
                break;

            case S2C_HeaderType.GAME_DRAW:
                HandleGameDraw();
                break;

            case S2C_HeaderType.ENEMY_EXIT:
                HandleEnemyExit();
                break;

            case S2C_HeaderType.ROOM_CLOSED:
                SceneManager.LoadScene("Result");
                break;

            case S2C_HeaderType.PONG:
                OnPong();
                break;

            default:
                Debug.Log($"[WARN] Unknown packet type: {packetType}");
                break;
        }
    }

    // 매칭 성사 시, 룸 관련 변수 초기화
    private static void HandleMatchFound(CppMatchFoundPacket pkt)
    {
        SocketClient.Instance.roomId = pkt.roomId;
        SocketClient.Instance.myUserId = pkt.myUserId;
        SocketClient.Instance.mySessionId = pkt.mySessionId;
        SocketClient.Instance.enemySessionId = pkt.enemySessionId;
        SocketClient.Instance.enemyUserId = pkt.enemyUserId;
        SocketClient.Instance.side = (pkt.side == 0) ? "LEFT" : "RIGHT" ;

        MatchButton.Instance.isMatching = false;

        SocketClient.Instance.OnMatchFound();
    }

    // 배틀씬 로드
    private static void HandleLoadBattle()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SceneLoader.Instance.LoadBattle();
        _ = SocketClient.Instance.CppSendHeaderOnly(C2S_HeaderType.BATTLE_READY);
    }

    // 상태 업데이트
    private static void HandleState(CppPlayerStatePacket pkt)
    {
        if (!GameManager.Instance ||
            GameManager.Instance.myPlayer == null ||
            GameManager.Instance.enemyPlayer == null)
        {
            return;
        }

        if (pkt.playerId == SocketClient.Instance.myUserId)
        {
            // 내 캐릭터 업데이트
            var pc = GameManager.Instance.myPlayer.GetComponent<PlayerController>();
            pc.ApplyServerState(new Vector2(pkt.x, pkt.y), pkt.state, pkt.dir);
        }
        else
        {
            // 상대 캐릭터 업데이트
            var pc = GameManager.Instance.enemyPlayer.GetComponent<EnemyController>();
            pc.ApplyServerState(new Vector2(pkt.x, pkt.y), pkt.state, pkt.dir);
        }
    }

    // Damage 발생 시, UI 업데이트
    private static void HandleTakeDamage(CppDamagePacket pkt)
    {
        GameObject target = (pkt.hurtId == SocketClient.Instance.myUserId) ?
            GameManager.Instance.myPlayer : GameManager.Instance.enemyPlayer;

        SocketClient.Instance.UpdateHP(target, pkt.currentHP);
    }

    // 현재 시간으로 UI 업데이트
    private static void HandleTime(CppTimeSyncPacket pkt)
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.timerText.text = pkt.time.ToString();
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameWin()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Win";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;

        _ = SocketClient.Instance.CppSendHeaderOnly(C2S_HeaderType.RESULT_ACK);
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameLose()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Lose";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;

        _ = SocketClient.Instance.CppSendHeaderOnly(C2S_HeaderType.RESULT_ACK);
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameDraw()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Draw";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;

        _ = SocketClient.Instance.CppSendHeaderOnly(C2S_HeaderType.RESULT_ACK);
    }

    // 상대 종료 상태 업데이트
    private static void HandleEnemyExit()
    {
        SocketClient.Instance.enemyDisconnected = true;
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;
    }

    // 핑 보내기
    private static void OnPong()
    {
        _ = SocketClient.Instance.CppSendHeaderOnly(C2S_HeaderType.PING);
    }
}
