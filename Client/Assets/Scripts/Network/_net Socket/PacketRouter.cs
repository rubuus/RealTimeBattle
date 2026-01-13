using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PacketRouter
{
    public static void Route(string msg)
    {
        var basePacket = JsonUtility.FromJson<BasePacket>(msg);

        if (!Enum.TryParse<PacketType>(basePacket.type, out var type))
        {
            Console.WriteLine("[WARN] Unknown packet: " + msg);
            return;
        }

        switch (type)
        {
            case PacketType.MATCH_FOUND:
                HandleMatchFound(msg);
                break;

            case PacketType.LOAD_BATTLE:
                HandleLoadBattle();
                break;

            case PacketType.PLAYER_STATE:
                HandleState(msg);
                break;

            case PacketType.TAKE_DAMAGE:
                HandleTakeDamage(msg);
                break;

            case PacketType.GAME_TIME:
                HandleTime(msg);
                break;

            case PacketType.GAME_WIN:
                HandleGameWin();
                break;

            case PacketType.GAME_LOSE:
                HandleGameLose();
                break;

            case PacketType.GAME_DRAW:
                HandleGameDraw();
                break;

            case PacketType.ENEMY_EXIT:
                HandleEnemyExit();
                break;

            case PacketType.ROOM_CLOSED:
                SceneManager.LoadScene("Result");
                break;

            case PacketType.PING:
                _ = SocketClient.Instance.Send(new BasePacket { type = "PONG" });
                break;
        }
    }

    private static void HandleMatchFound(string msg)
    {
        MatchFoundPacket p = JsonUtility.FromJson<MatchFoundPacket>(msg);

        SocketClient.Instance.roomId = p.roomId;
        SocketClient.Instance.mySessionId = p.mySessionId;
        SocketClient.Instance.enemySessionId = p.enemySessionId;
        SocketClient.Instance.side = p.side;

        MatchButton.Instance.isMatching = false;

        SocketClient.Instance.OnMatchFound();
    }

    private static void HandleLoadBattle()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SceneLoader.Instance.LoadBattle();
    }

    private static void HandleState(string msg)
    {
        if (!GameManager.Instance ||
            GameManager.Instance.myPlayer == null ||
            GameManager.Instance.enemyPlayer == null)
        {
            return;
        }

        PlayerStatePacket p = JsonUtility.FromJson<PlayerStatePacket>(msg);

        if (p.userId == SocketClient.Instance.myUserId)
        {
            // 내 캐릭터 업데이트
            var pc = GameManager.Instance.myPlayer.GetComponent<PlayerController>();
            pc.ApplyServerState(new Vector2(p.x, p.y), p.state, p.dir);
        }
        else
        {
            // 상대 캐릭터 업데이트
            var pc = GameManager.Instance.enemyPlayer.GetComponent<EnemyController>();
            pc.ApplyServerState(new Vector2(p.x, p.y), p.state, p.dir);
        }
    }

    private static void HandleTakeDamage(string msg)
    {
        DamagePacket p = JsonUtility.FromJson<DamagePacket>(msg);

        GameObject target = (p.hurtId == SocketClient.Instance.myUserId) ?
            GameManager.Instance.myPlayer : GameManager.Instance.enemyPlayer;

        SocketClient.Instance.UpdateHP(target, p.currentHP);
    }

    private static void HandleTime(string msg)
    {
        TimeSyncPacket p = JsonUtility.FromJson<TimeSyncPacket>(msg);

        GameManager.Instance.timerText.text = p.time.ToString();
    }

    private static void HandleGameWin()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Win";
        _ = SocketClient.Instance.Send(new BasePacket { type = "RESULT_ACK" });
    }

    private static void HandleGameLose()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Lose";
        _ = SocketClient.Instance.Send(new BasePacket { type = "RESULT_ACK" });
    }

    private static void HandleGameDraw()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Draw";
        _ = SocketClient.Instance.Send(new BasePacket { type = "RESULT_ACK" });
    }

    private static void HandleEnemyExit()
    {
        SocketClient.Instance.enemyDisconnected = true;
    }
}
