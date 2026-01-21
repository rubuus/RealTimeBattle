using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

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
        var basePacket = JsonConvert.DeserializeObject<BasePacket>(msg);

        switch (basePacket.Type)
        {
            case "MATCH_FOUND":
                HandleMatchFound(msg);
                break;

            case "LOAD_BATTLE":
                HandleLoadBattle();
                break;

            case "PLAYER_STATE":
                HandleState(msg);
                break;

            case "TAKE_DAMAGE":
                HandleTakeDamage(msg);
                break;

            case "GAME_TIME":
                HandleTime(msg);
                break;

            case "GAME_WIN":
                HandleGameWin();
                break;

            case "GAME_LOSE":
                HandleGameLose();
                break;

            case "GAME_DRAW":
                HandleGameDraw();
                break;

            case "ENEMY_EXIT":
                HandleEnemyExit();
                break;

            case "ROOM_CLOSED":
                SceneManager.LoadScene("Result");
                break;

            case "PONG":
                OnPong();
                break;
        }
    }

    // 매칭 성사 시, 룸 관련 변수 초기화
    private static void HandleMatchFound(string msg)
    {
        MatchFoundPacket p = JsonConvert.DeserializeObject<MatchFoundPacket>(msg);

        SocketClient.Instance.roomId = p.RoomId;
        SocketClient.Instance.myUserId = p.MyUserId;
        SocketClient.Instance.mySessionId = p.MySessionId;
        SocketClient.Instance.enemySessionId = p.EnemySessionId;
        SocketClient.Instance.enemyUserId = p.EnemyUserId;
        SocketClient.Instance.side = p.Side;

        MatchButton.Instance.isMatching = false;

        SocketClient.Instance.OnMatchFound();
    }

    // 배틀씬 로드
    private static void HandleLoadBattle()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SceneLoader.Instance.StartCoroutine(SceneLoader.Instance.LoadBattle());
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

        PlayerStatePacket p = JsonConvert.DeserializeObject<PlayerStatePacket>(msg);

        if (p.UserId == SocketClient.Instance.myUserId)
        {
            // 내 캐릭터 업데이트
            var pc = GameManager.Instance.myPlayer.GetComponent<PlayerController>();
            pc.ApplyServerState(new Vector2(p.X, p.Y), p.State, p.Dir);
        }
        else
        {
            // 상대 캐릭터 업데이트
            var pc = GameManager.Instance.enemyPlayer.GetComponent<EnemyController>();
            pc.ApplyServerState(new Vector2(p.X, p.Y), p.State, p.Dir);
        }
    }

    // Damage 발생 시, UI 업데이트
    private static void HandleTakeDamage(string msg)
    {
        DamagePacket p = JsonConvert.DeserializeObject<DamagePacket>(msg);

        GameObject target = (p.HurtId == SocketClient.Instance.myUserId) ?
            GameManager.Instance.myPlayer : GameManager.Instance.enemyPlayer;

        SocketClient.Instance.UpdateHP(target, p.CurrentHp);
    }

    // 현재 시간으로 UI 업데이트
    private static void HandleTime(string msg)
    {
        TimeSyncPacket p = JsonConvert.DeserializeObject<TimeSyncPacket>(msg);

        GameManager.Instance.timerText.text = p.Time.ToString();
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameWin()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Win";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;
        SocketClient.Instance.side = string.Empty;

        _ = SocketClient.Instance.CSharpSend(new BasePacket { Type = "RESULT_ACK" });
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameLose()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Lose";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;
        SocketClient.Instance.side = string.Empty;

        _ = SocketClient.Instance.CSharpSend(new BasePacket { Type = "RESULT_ACK" });
    }

    // Result 씬에 결과 반영 먼저 한 후, 배틀 끝났음을 서버에 알림
    private static void HandleGameDraw()
    {
        SocketClient.Instance.enemyDisconnected = false;
        SocketClient.Instance.finalResult = "Draw";
        SocketClient.Instance.enemyUserId = -1;
        SocketClient.Instance.enemySessionId = -1;
        SocketClient.Instance.side = string.Empty;

        _ = SocketClient.Instance.CSharpSend(new BasePacket { Type = "RESULT_ACK" });
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
        _ = SocketClient.Instance.CSharpSend(new BasePacket { Type = "PING" });
    }
}
