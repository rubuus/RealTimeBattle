using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CppPacketRouter
{
    public static void Route(ushort packetType, ReadOnlySpan<byte> body)
    {
        var type = (S2C_PacketType)packetType;

        switch (type)
        {
            case S2C_PacketType.MATCH_FOUND:
                {
                    var pkt = MemoryMarshal.Read<CppMatchFoundPacket>(body);
                    HandleMatchFound(pkt);
                }
                break;

            case S2C_PacketType.LOAD_BATTLE:
                HandleLoadBattle();
                break;

            case S2C_PacketType.PLAYER_STATE:
                {
                    var pkt = MemoryMarshal.Read<CppPlayerStatePacket>(body);
                    HandleState(pkt);
                }
                break;

            case S2C_PacketType.TAKE_DAMAGE:
                {
                    Debug.Log("Received TAKE_DAMAGE packet");
                    var pkt = MemoryMarshal.Read<CppDamagePacket>(body);
                    HandleTakeDamage(pkt);
                }
                break;

            case S2C_PacketType.GAME_TIME:
                {
                    var pkt = MemoryMarshal.Read<CppTimeSyncPacket>(body);
                    HandleTime(pkt);
                }
                break;

            case S2C_PacketType.GAME_WIN:
                HandleGameWin();
                break;

            case S2C_PacketType.GAME_LOSE:
                HandleGameLose();
                break;

            case S2C_PacketType.GAME_DRAW:
                HandleGameDraw();
                break;

            case S2C_PacketType.ENEMY_EXIT:
                HandleEnemyExit();
                break;

            case S2C_PacketType.ROOM_CLOSED:
                SceneManager.LoadScene("Result");
                break;

            case S2C_PacketType.PONG:
                OnPong();
                break;

            default:
                Console.WriteLine($"[WARN] Unknown packet type: {packetType}");
                break;
        }
    }

    private static void HandleMatchFound(CppMatchFoundPacket pkt)
    {
        SocketClient.Instance.roomId = pkt.roomId;
        SocketClient.Instance.myId = pkt.myId;
        SocketClient.Instance.enemyId = pkt.enemyId;
        SocketClient.Instance.side = (pkt.side == 0) ? "LEFT" : "RIGHT" ;

        MatchButton.Instance.isMatching = false;

        SocketClient.Instance.OnMatchFound();
    }

    private static void HandleLoadBattle()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SceneLoader.Instance.LoadBattle();
    }

    private static void HandleState(CppPlayerStatePacket pkt)
    {
        if (!GameManager.Instance ||
            GameManager.Instance.myPlayer == null ||
            GameManager.Instance.enemyPlayer == null)
        {
            return;
        }

        if (pkt.userId == SocketClient.Instance.myId)
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

    private static void HandleTakeDamage(CppDamagePacket pkt)
    {
        GameObject target = (pkt.hurtId == SocketClient.Instance.myId) ?
            GameManager.Instance.myPlayer : GameManager.Instance.enemyPlayer;

        SocketClient.Instance.UpdateHP(target, pkt.currentHP);
    }

    private static void HandleTime(CppTimeSyncPacket pkt)
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.timerText.text = pkt.time.ToString();
    }

    private static void HandleGameWin()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Win";

        _ = SocketClient.Instance.SendHeaderOnlyAsync(C2S_PacketType.RESULT_ACK);
    }

    private static void HandleGameLose()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Lose";

        _ = SocketClient.Instance.SendHeaderOnlyAsync(C2S_PacketType.RESULT_ACK);
    }

    private static void HandleGameDraw()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Draw";

        _ = SocketClient.Instance.SendHeaderOnlyAsync(C2S_PacketType.RESULT_ACK);
    }

    private static void HandleEnemyExit()
    {
        SocketClient.Instance.enemyDisconnected = true;
    }

    private static void OnPong()
    {
        _ = SocketClient.Instance.SendHeaderOnlyAsync(C2S_PacketType.PING);
    }
}
