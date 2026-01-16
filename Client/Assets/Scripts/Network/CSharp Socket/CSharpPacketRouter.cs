using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * CSharpPacketRouter.cs
 * 
 * 역할 :
 * - - 패킷 데이터 기반으로 이벤트 생성 및 서버에 패킷 전송 (핸들링)
 * 
 */

public static class CSharpPacketRouter
{
    public static void Route(string msg)
    {
        var basePacket = JsonUtility.FromJson<BasePacket>(msg);

        if (!Enum.TryParse<S2C_PacketType>(basePacket.type, out var type))
        {
            Console.WriteLine("[WARN] Unknown packet: " + msg);
            return;
        }

        switch (type)
        {
            case S2C_PacketType.MATCH_FOUND:
                HandleMatchFound(msg);
                break;

            case S2C_PacketType.LOAD_BATTLE:
                HandleLoadBattle();
                break;

            case S2C_PacketType.PLAYER_STATE:
                HandleState(msg);
                break;

            case S2C_PacketType.TAKE_DAMAGE:
                HandleTakeDamage(msg);
                break;

            case S2C_PacketType.GAME_TIME:
                HandleTime(msg);
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
        }
    }

    // 매칭 성사 시, 룸 관련 변수 초기화
    private static void HandleMatchFound(string msg)
    {
        MatchFoundPacket p = JsonUtility.FromJson<MatchFoundPacket>(msg);

        SocketClient.Instance.roomId = p.roomId;
        SocketClient.Instance.myUserId = p.myUserId;
        SocketClient.Instance.mySessionId = p.mySessionId;
        SocketClient.Instance.enemySessionId = p.enemySessionId;
        SocketClient.Instance.enemyUserId = p.enemyUserId;
        SocketClient.Instance.side = p.side;

        MatchButton.Instance.isMatching = false;

        SocketClient.Instance.OnMatchFound();
    }

    // 배틀씬 로드
    private static void HandleLoadBattle()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SceneLoader.Instance.LoadBattle();
    }

    // 상태 업데이트
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

    // Damage 발생 시, UI 업데이트
    private static void HandleTakeDamage(string msg)
    {
        DamagePacket p = JsonUtility.FromJson<DamagePacket>(msg);

        GameObject target = (p.hurtId == SocketClient.Instance.myUserId) ?
            GameManager.Instance.myPlayer : GameManager.Instance.enemyPlayer;

        SocketClient.Instance.UpdateHP(target, p.currentHP);
    }

    // 현재 시간으로 UI 업데이트
    private static void HandleTime(string msg)
    {
        TimeSyncPacket p = JsonUtility.FromJson<TimeSyncPacket>(msg);

        GameManager.Instance.timerText.text = p.time.ToString();
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameWin()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Win";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;

        _ = SocketClient.Instance.CsharpSend(new BasePacket { type = "RESULT_ACK" });
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameLose()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Lose";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;

        _ = SocketClient.Instance.CsharpSend(new BasePacket { type = "RESULT_ACK" });
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameDraw()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Draw";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;

        _ = SocketClient.Instance.CsharpSend(new BasePacket { type = "RESULT_ACK" });
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
        _ = SocketClient.Instance.CsharpSend(new BasePacket { type = "PING" });
    }
}
