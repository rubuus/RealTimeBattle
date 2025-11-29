using System.Numerics;
using System.Text.Json;

public class Room
{
    public int roomId;
    public ClientSession player1;
    public ClientSession player2;
    public ServerPlayer sPlayer1;
    public ServerPlayer sPlayer2;

    Vector2 leftSpawn = new Vector2(-7f, -2.3f);
    Vector2 rightSpawn = new Vector2(7f, -2.3f);

    public bool p1Ready = false;
    public bool p2Ready = false;
    public bool gameStarted = false;

    public bool p1Ended = false;
    public bool p2Ended = false;

    private bool closed = false;

    public Room(int id, ClientSession p1, ClientSession p2)
    {
        roomId = id;
        player1 = p1;
        player2 = p2;

        p1._room = this;
        p2._room = this;

        sPlayer1 = new ServerPlayer(userId: p1.userId, side: "LEFT", leftSpawn);
        sPlayer2 = new ServerPlayer(userId: p2.userId, side: "RIGHT", rightSpawn);
    }

    public void CheckMatch(ClientSession sender)
    {
        if (sender == player1)
            p1Ready = true;

        if (sender == player2)
            p2Ready = true;

        if (p1Ready && p2Ready)
        {
            gameStarted = true;
            player1.Send(new { type = "LOAD_BATTLE" });
            player2.Send(new { type = "LOAD_BATTLE" });
        }   
    }

    public void Update(float dt)
    {
        if (!gameStarted) return;
        if (player1 == null || player2 == null) return;
        if (!player1.battleReady || !player2.battleReady) return;

        sPlayer1.Update(dt);
        sPlayer2.Update(dt);

        player1.Send(sPlayer1.ToPacket());
        player2.Send(sPlayer2.ToPacket());
        player1.Send(sPlayer2.ToPacket());
        player2.Send(sPlayer1.ToPacket());
    }

    public void OnInputPacket(ClientSession sender, PlayerInputPacket p)
    {
        if (sender == player1)
            sPlayer1.ApplyInput(p);

        else
            sPlayer2.ApplyInput(p);
    }


    /*public void UpdatePlayerHP(HitPacket hit)
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
    }*/

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
        if (s == player1) p1Ended = true;
        if (s == player2) p2Ended = true;

        // 양쪽 다 정상적으로 끝남
        if (p1Ended && p2Ended)
        {
            gameStarted = false;
            SocketServer.Instance.CloseRoom(roomId); 
        }
    }

    public void OnPlayerDisconnect(ClientSession s)
    {
        if (closed) return;
        closed = true;

        if (!gameStarted || p1Ended || p2Ended)
        {
            SocketServer.Instance.CloseRoom(roomId);
            return;
        }

        // 비정상 종료(진짜 튕김)
        ClientSession remaining = (s == player1) ? player2 : player1;
        
        if (remaining != null)
            remaining.Send(new { type = "ENEMY_EXIT" });

        SocketServer.Instance.CloseRoom(roomId);
    }

    public void CloseRoom()
    {
        if (closed) return;
        closed = true;

        player1._room = null;
        player2._room = null;
    }
}
