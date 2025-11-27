using System.Numerics;
using System.Text.Json;

public class Room
{
    public int RoomId;
    public ClientSession Player1;
    public ClientSession Player2;

    public bool P1Ready = false;
    public bool P2Ready = false;
    public bool GameStarted = false;

    public bool P1Ended = false;
    public bool P2Ended = false;

    private bool closed = false;

    public Room(int roomId, ClientSession p1, ClientSession p2)
    {
        RoomId = roomId;
        Player1 = p1;
        Player2 = p2;

        p1._room = this;
        p2._room = this;
    }

    public void CheckMatch(ClientSession sender)
    {
        if (sender == Player1)
            P1Ready = true;

        if (sender == Player2)
            P2Ready = true;

        if (P1Ready && P2Ready)
        {
            GameStarted = true;
            Player1.Send(new { type = "LOAD_BATTLE" });
            Player2.Send(new { type = "LOAD_BATTLE" });
        }   
    }

    public void UpdatePlayerState(ClientSession sender, PlayerMovePacket p)
    {
        // sender가 Player1인지 Player2인지 확인
        if (sender == Player1)
        {
            Player1.lastPos = new Vector2(p.x, p.y);
            Player1.lastState = p.state;
        }
        else
        {
            Player2.lastPos = new Vector2(p.x, p.y);
            Player2.lastState = p.state;
        }
    }

    public void UpdatePlayerHP(HitPacket hit)
    {
        ClientSession target = null;

        if (Player1.UserId == hit.hurtId)
            target = Player1;
        else if (Player2.UserId == hit.hurtId)
            target = Player2;

        if (target == null) return;

        target.currentHp -= hit.damage;

        // DAMAGE 패킷 만들어서 양쪽에게 전송
        var dmg = new DamagePacket
        {
            type = "TAKE_DAMAGE",
            id = target.UserId,
            amount = hit.damage,
            currentHP = target.currentHp
        };

        Player1.Send(dmg);
        Player2.Send(dmg);

        // hp 변화하면 winner, loser 갱신
        ClientSession winner = (target == Player1) ? Player2 : Player1;
        ClientSession loser = target;

        if (loser.currentHp <= 0)
            SendGameResult(winner, loser);
    }

    public void SendGameResult(ClientSession winner, ClientSession loser)
    {
        if (winner.currentHp != loser.currentHp)
        {
            winner.Send(new { type = "GAME_WIN" });
            loser.Send(new { type = "GAME_LOSE" });
        }
        else
        {
            winner.Send(new { type = "GAME_DRAW" });
            loser.Send(new { type = "GAME_DRAW" });
        }
    }

    public void OnGameEnd(ClientSession s)
    {
        if (s == Player1) P1Ended = true;
        if (s == Player2) P2Ended = true;

        // 양쪽 다 정상적으로 끝남
        if (P1Ended && P2Ended)
        {
            GameStarted = false;
            SocketServer.Instance.CloseRoom(RoomId); 
        }
    }

    public void OnPlayerDisconnect(ClientSession s)
    {
        if (closed) return;
        closed = true;

        if (!GameStarted || P1Ended || P2Ended)
        {
            SocketServer.Instance.CloseRoom(RoomId);
            return;
        }

        // 비정상 종료(진짜 튕김)
        ClientSession remaining = (s == Player1) ? Player2 : Player1;
        
        if (remaining != null)
            remaining.Send(new { type = "ENEMY_EXIT" });

        SocketServer.Instance.CloseRoom(RoomId);
    }

    public void CloseRoom()
    {
        if (closed) return;
        closed = true;

        Player1._room = null;
        Player2._room = null;
    }
}
