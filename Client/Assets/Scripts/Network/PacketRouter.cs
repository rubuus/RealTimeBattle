using System;
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
        Debug.Log(basePacket);
        switch (type)
        {
            case PacketType.MATCH_FOUND:
                HandleMatchFound(msg);
                break;

            case PacketType.LOAD_BATTLE:
                HandleLoadBattle();
                break;

            case PacketType.PLAYER_MOVE:
                HandleMove(msg);
                break;

            case PacketType.TAKE_DAMAGE:
                HandleTakeDamage(msg);
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
        }
    }

    private static void HandleMatchFound(string msg)
    {
        MatchFoundPacket p = JsonUtility.FromJson<MatchFoundPacket>(msg);

        SocketClient.Instance.roomId = p.roomId;
        SocketClient.Instance.myUserId = p.myUserId;
        SocketClient.Instance.enemyUserId = p.enemyUserId;
        SocketClient.Instance.side = p.side;

        MatchButton.Instance.isMatching = false;

        SocketClient.Instance.OnMatchFound();
    }

    private static void HandleLoadBattle()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SceneLoader.Instance.LoadBattle();
    }

    private static void HandleMove(string msg)
    {
        PlayerMovePacket p = JsonUtility.FromJson<PlayerMovePacket>(msg);

        if (p.id == SocketClient.Instance.myUserId)
            return;

        if (p.id != SocketClient.Instance.myUserId)
        {
            var enemy = GameManager.Instance.enemyPlayer;

            if (enemy != null)
            {
                var pc = enemy.GetComponent<EnemyController>();

                pc.EnemyStateUpdate(new Vector2(p.x, p.y), p.state);
            }
        }
    }

    private static void HandleTakeDamage(string msg)
    {
        DamagePacket p = JsonUtility.FromJson<DamagePacket>(msg);

        GameObject target = (p.id == SocketClient.Instance.myUserId) ?
            GameManager.Instance.myPlayer : GameManager.Instance.enemyPlayer;

        SocketClient.Instance.UpdateHP(target, p.currentHP);
    }

    private static void HandleGameWin()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Win";
        SceneManager.LoadScene("Result");
    }

    private static void HandleGameLose()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Lose";
        SceneManager.LoadScene("Result");
    }

    private static void HandleGameDraw()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Draw";
        SceneManager.LoadScene("Result");
    }

    private static void HandleEnemyExit()
    {
        SocketClient.Instance.enemyDisconnected = true;
        SceneManager.LoadScene("Result");
    }
}
