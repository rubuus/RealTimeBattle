using System.Numerics;
using System.Text.Json;

public class Room
{
    public int RoomId;
    public ClientSession Player1;
    public ClientSession Player2;

    public bool P1Ended = false;
    public bool P2Ended = false;

    public bool P1Disconnected = false;
    public bool P2Disconnected = false;

    public Room(int roomId, ClientSession p1, ClientSession p2)
    {
        RoomId = roomId;
        Player1 = p1;
        Player2 = p2;

        p1.Room = this;
        p2.Room = this;
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

        if (target.currentHp < 0)
        {
            ClientSession winner = (target == Player1) ? Player2 : Player1;
            ClientSession loser = target;
            
            // 💡 서버가 승패를 결정하고 클라이언트에게 통보
            SendGameResult(winner, loser);
        }

        // DAMAGE 패킷 만들어서 양쪽에게 전송
        var dmg = new DamagePacket
        {
            type = "TAKE_DAMAGE",
            id = target.UserId,
            amount = hit.damage,
            currentHP = target.currentHp
        };

        string json = JsonSerializer.Serialize(dmg);
        Console.WriteLine(json);

        Player1.Send(json);
        Player2.Send(json);
    }

    public void OnGameEnd(ClientSession s)
    {
        if (s == Player1) P1Ended = true;
        if (s == Player2) P2Ended = true;

        // 양쪽 다 정상적으로 끝남
        if (P1Ended && P2Ended)
            SocketServer.Instance.CloseRoom(RoomId); 
    }

    public void SendGameResult(ClientSession winner, ClientSession loser)
    {
        winner.Send("GAME_WIN");
        loser.Send("GAME_LOSE");
    }

    public void OnPlayerDisconnect(ClientSession s)
    {
        if (P1Ended && P2Ended)
        {
            SocketServer.Instance.CloseRoom(RoomId);
            return; 
        }

        // 2. 💔 게임이 진행 중이거나 한쪽만 끝났는데 Disconnect된 상황 (튕김)
        
        // Disconnect된 쪽이 Player1인지 Player2인지 확인
        ClientSession disconnectedPlayer = s;
        ClientSession remainingPlayer = (s == Player1) ? Player2 : Player1;
        
        // 살아있는 상대방에게만 즉시 튕김 신호를 보냅니다.
        // (remainingPlayer가 null이 아닐 경우에만 send)
        if (remainingPlayer != null)
        {
            remainingPlayer.Send("ENEMY_EXIT");
        }

        // 3. 방 삭제
        SocketServer.Instance.CloseRoom(RoomId);
    }



    public void CloseRoom()
    {
        Player1.Room = null;
        Player2.Room = null;
    }
}
