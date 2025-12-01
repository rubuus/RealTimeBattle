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

    public struct Hitbox
    {
        public float x;     // 중심
        public float y;     // 중심
        public float halfW; // 플레이어 폭의 절반
        public float halfH; // 플레이어 높이의 절반
    }

    public struct Hurtbox
    {
        public float x;     // 중심
        public float y;     // 중심
        public float halfW; // 플레이어 폭의 절반
        public float halfH; // 플레이어 높이의 절반
    }

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

        CheckDamage();
        SendStatePacketToClient();
    }

    public void OnInputPacket(ClientSession sender, PlayerInputPacket p)
    {
        if (sender == player1)
            sPlayer1.ApplyInput(p);

        else
            sPlayer2.ApplyInput(p);
    }

    public void SendStatePacketToClient()
    {
        player1.Send(sPlayer1.ToPacket());
        player2.Send(sPlayer2.ToPacket());
        player1.Send(sPlayer2.ToPacket());
        player2.Send(sPlayer1.ToPacket());
    }

    public bool IsInPunchRange(ServerPlayer attacker, ServerPlayer target)
    {
        Hitbox hitbox;
        Hurtbox hurtbox;

        hitbox.x = attacker.position.X + (0.3f * attacker.dir);
        hitbox.y = attacker.position.Y;
        hitbox.halfW = 0.3f;
        hitbox.halfH = 0.7f;

        hurtbox.x = target.position.X;
        hurtbox.y = target.position.Y;
        hurtbox.halfW = 0.5f;
        hurtbox.halfH = 1f;

        attacker.punchPressed = false;
        
        return Overlap(hitbox, hurtbox);
    }

    public bool Overlap(Hitbox a, Hurtbox b)
    {
        return !(Math.Abs(a.x - b.x) > (a.halfW + b.halfW) ||
                Math.Abs(a.y - b.y) > (a.halfH + b.halfH));
    }

    public void CheckDamage()
    {
        if (sPlayer1.state == PlayerState.Punch && sPlayer1.punchPressed)
        {
            if(IsInPunchRange(sPlayer1, sPlayer2))
            {
                sPlayer2.TakeDamage(10);
                SendDamagePacketToClient();
            }
        }
        else if (sPlayer2.state == PlayerState.Punch && sPlayer2.punchPressed)
        {
            if(IsInPunchRange(sPlayer2, sPlayer1))
            {
                sPlayer1.TakeDamage(10);
                SendDamagePacketToClient();
            }
        }
    }

    public void SendDamagePacketToClient()
    {
        player1.Send(HurtPacket(sPlayer1.id, sPlayer1.currentHP));
        player1.Send(HurtPacket(sPlayer2.id, sPlayer2.currentHP));
        player2.Send(HurtPacket(sPlayer1.id, sPlayer1.currentHP));
        player2.Send(HurtPacket(sPlayer2.id, sPlayer2.currentHP));
    }

    public DamagePacket HurtPacket(int id, int currentHP)
    {
        return new DamagePacket
        {
            type = "TAKE_DAMAGE",
            hurtId = id,
            currentHP = currentHP
        };
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
