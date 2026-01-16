using System.Numerics;
using api.DTOs;

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
    private int waitingAckCount = 2;
    private bool closed = false;
    private bool pendingClose = false;
    public float gameTime = 100f;
    private bool startedFirstFrameSent = false;

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

        gameTime = 100f;
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
        if (!player1.battleReady || !player2.battleReady) return;
        if (!startedFirstFrameSent)
        {
            startedFirstFrameSent = true;
            return;
        }

        gameTime -= dt;
        SendTimePacket();

        if (gameTime <= 0f || sPlayer1.currentHP <= 0 || sPlayer2.currentHP <= 0)
        {
            player1.battleReady = false;
            player2.battleReady = false;
            EndGame();
            return;
        }

        sPlayer1.Update(dt);
        sPlayer2.Update(dt);

        CheckDamage();
        SendStatePacket();

        if (pendingClose)
        {
            pendingClose = false;
            SocketServer.Instance.CloseRoom(roomId);
        }
    }

    public void OnInputPacket(ClientSession sender, PlayerInputPacket p)
    {
        if (sender == player1)
            sPlayer1.ApplyInput(p);

        else
            sPlayer2.ApplyInput(p);
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

    public bool Overlap(Hitbox hit, Hurtbox hurt)
    {
        return !(Math.Abs(hit.x - hurt.x) > (hit.halfW + hurt.halfW) ||
                Math.Abs(hit.y - hurt.y) > (hit.halfH + hurt.halfH));
    }

    public void CheckDamage()
    {
        // player1 -> player2 공격
        if (sPlayer1.state == PlayerState.Punch && sPlayer1.punchPressed)
        {
            if(IsInPunchRange(sPlayer1, sPlayer2))
            {
                if (sPlayer1.position.X > sPlayer2.position.X && sPlayer2.dir < 0)
                    sPlayer2.dir = 1;
                else if (sPlayer1.position.X < sPlayer2.position.X && sPlayer2.dir > 0)
                    sPlayer2.dir = -1;
                
                sPlayer2.TakeDamage(10, sPlayer1.dir * 1f);
                SendDamagePacket();
            }
        }
        // player2 -> player1 공격
        else if (sPlayer2.state == PlayerState.Punch && sPlayer2.punchPressed)
        {
            if(IsInPunchRange(sPlayer2, sPlayer1))
            {
                if (sPlayer2.position.X > sPlayer1.position.X && sPlayer1.dir < 0)
                    sPlayer1.dir = 1;
                else if (sPlayer2.position.X < sPlayer1.position.X && sPlayer1.dir > 0)
                    sPlayer1.dir = -1;

                sPlayer1.TakeDamage(10, sPlayer2.dir * 1f);
                SendDamagePacket();
            }
        }
    }

    public void SendStatePacket()
    {
        player1.Send(sPlayer1.StatePacket());
        player1.Send(sPlayer2.StatePacket());
        player2.Send(sPlayer1.StatePacket());
        player2.Send(sPlayer2.StatePacket());
    }

    public void SendDamagePacket()
    {
        player1.Send(sPlayer1.HurtPacket());
        player1.Send(sPlayer2.HurtPacket());
        player2.Send(sPlayer1.HurtPacket());
        player2.Send(sPlayer2.HurtPacket());
    }

    public void SendTimePacket()
    {
        player1.Send(new TimeSyncPacket
        {
           type = "GAME_TIME",
           time = (int)Math.Ceiling(gameTime)
        });

        player2.Send(new TimeSyncPacket
        {
           type = "GAME_TIME",
           time = (int)Math.Ceiling(gameTime)
        });
    }

    public void SendGameResult()
    {
        if (sPlayer1.currentHP > sPlayer2.currentHP)
        {
            player1.Send(new { type = "GAME_WIN" });
            player2.Send(new { type = "GAME_LOSE" });
            _ = SaveRecordAsync(player1, player2);
        }
        else if (sPlayer1.currentHP < sPlayer2.currentHP)
        {
            player1.Send(new { type = "GAME_LOSE" });
            player2.Send(new { type = "GAME_WIN" });
            _ = SaveRecordAsync(player2, player1);
        }
        else
        {
            player1.Send(new { type = "GAME_DRAW" });
            player2.Send(new { type = "GAME_DRAW" });
            _ = SaveRecordAsync(player1, player2);
        }
    }

    public void OnAckReceived(ClientSession s)
    {
        if (s.ackReceived) return;
        s.ackReceived = true;

        waitingAckCount--;

        if (waitingAckCount == 0)
        {
            player1.Send(new { type = "ROOM_CLOSED" });
            player2.Send(new { type = "ROOM_CLOSED" });
            pendingClose = true; 
        }
    }

    public async Task SaveRecordAsync(ClientSession winner, ClientSession loser)
    {
        var req = new SaveRecordRequest
        {
            WinnerId = winner.userId,
            LoserId = loser.userId
        };

        bool success = await ApiClient.Post("battle/save", req);

        if (success)
            Console.WriteLine("전적 저장됨");
        else
            Console.WriteLine("전적 저장 실패");
    }

    private void EndGame()
    {
        if (closed) return;
        closed = true;

        SendGameResult();

        waitingAckCount = 2;
    }

    public void OnPlayerDisconnect(ClientSession s)
    {
        if (closed) return;
        closed = true;

        // 비정상 종료(진짜 튕김)
        ClientSession winner = (s == player1) ? player2 : player1;
        ClientSession loser = (s == player1) ? player1 : player2;
        
        winner?.Send(new { type = "ENEMY_EXIT" });
        winner?.Send(new { type = "ROOM_CLOSED" });
        _ = SaveRecordAsync(winner, loser);
        
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
