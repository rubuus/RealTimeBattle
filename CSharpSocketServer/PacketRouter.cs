using Newtonsoft.Json;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class PacketRouter
{
    public static void Route(ClientSession s, string msg)
    {
        if (s.IsDisconnected)
            return;

        try
        {
            var basePacket = JsonConvert.DeserializeObject<BasePacket>(msg);
            if (string.IsNullOrEmpty(basePacket?.Type)) return;

            switch (basePacket?.Type)
            {
                case "LOGIN":
                    {
                        var p = JsonConvert.DeserializeObject<LoginPacket>(msg);
                        if (p == null) return;
                        HandleLogin(s, p.AccessToken);
                    }
                    break;

                case "MATCH_START":
                    HandleMatchStart(s);
                    break;
                
                case "BATTLE_READY":
                    HandleBattleReady(s);
                    break;
                
                case "BATTLE_START":
                    HandleBattleStart(s);
                    break;

                case "INPUT":
                    {
                        var p = JsonConvert.DeserializeObject<PlayerInputPacket>(msg);
                        if (p == null) return;
                        HandleInput(s, p);
                    }
                    break;
                
                case "RESULT_ACK":
                    HandleResultAck(s);
                    break;
                
                case "PING":
                    HandlePing(s);
                    break;

                default:
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void HandleLogin(ClientSession s, string token)
    {
        try
        {
            var principal = DecodeAndValidateJwt(token, SocketServer.Instance.SecretKey);

            var sub =
                principal.FindFirst("sub")?.Value ??
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(sub, out var userId))
            {
                Console.WriteLine("[LOGIN] invalid sub claim");
                s.Disconnect("Login Failed");
                return;
            }

            s.UserId = userId;
        }
        catch (SecurityTokenException e)
        {
            s.Disconnect("Not Token");
            Console.WriteLine(e);
        }
    }

    private static ClaimsPrincipal DecodeAndValidateJwt(string token, string secretKey)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secretKey);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "GamePortfolio.Server",

            ValidateAudience = true,
            ValidAudience = "GameClient",

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        return tokenHandler.ValidateToken(token, parameters, out _);
    }

    private static void HandleMatchStart(ClientSession s)
    {
        SocketServer.Instance.AddToMatchList(s);
    }

    private static void HandleBattleReady(ClientSession s)
    {
        if (!SocketServer.Instance.RoomAlive(s))
            return;

        var ev = new RoomEvent
        {
            EventType = RoomEventType.BattleReady,
            SessionId = s.SessionId,
            Payload = null
        };

        s.Room?.EnqueueEvent(ev);
    }

    private static void HandleBattleStart(ClientSession s)
    {
        if (!SocketServer.Instance.RoomAlive(s))
            return;

        var ev = new RoomEvent
        {
            EventType = RoomEventType.BattleStart,
            SessionId = s.SessionId,
            Payload = null
        };

        s.Room?.EnqueueEvent(ev);
    }

    private static void HandleInput(ClientSession s, PlayerInputPacket p)
    {
        if (!SocketServer.Instance.RoomAlive(s))
            return;
            
        var ev = new RoomEvent
        {
            EventType = RoomEventType.PlayerInput,
            SessionId = s.SessionId,
            Payload = p
        };

        s.Room!.EnqueueEvent(ev);
    }

    private static void HandleResultAck(ClientSession s)
    {
        if (!SocketServer.Instance.RoomAlive(s))
            return;

        var ev = new RoomEvent
        {
            EventType = RoomEventType.ResultAck,
            SessionId = s.SessionId,
            Payload = null
        };

        s.Room?.EnqueueEvent(ev);
    }
    
    private static void HandlePing(ClientSession s)
    {
        s.LastPingTime = DateTime.UtcNow;
    }
}