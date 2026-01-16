using Newtonsoft.Json;
using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

/* 
 * SocketClient.cs
 * 
 * 역할 : 
 * - TCP 소켓 클라이언트
 * - C++ IOCP 기반 실시간 게임 서버 (Binary 프로토콜) 연결
 * - C# TCP 기반 테스트/프로토타입 서버 (JSON 프로토콜) 연결
 * 
*/

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    public bool connected = false;
    public bool enemyDisconnected = false;

    public bool useCppServer { get; private set; }     // C++ C# 서버 변경 변수
    public int myUserId;
    public int mySessionId;
    public int enemyUserId;
    public int enemySessionId;
    public int roomId;
    public string side;

    public string finalResult = "DRAW";

    private TcpClient client;
    private NetworkStream stream;
    private StringBuilder recvBuffer = new StringBuilder();
    private byte[] buffer = new byte[1024];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start()
    {
        await CppConnect();
    }

    // C# 소켓 서버 연결
    public async Task CsharpConnect()
    {
        try
        {
            // Nagle 알고리즘 끄기
            client = new TcpClient();
            client.NoDelay = true;
            client.Client.NoDelay = true;

            await client.ConnectAsync("127.0.0.1", 5000);  // IP, Port 번호 설정

            stream = client.GetStream();

            connected = true;
            useCppServer = false;

            Debug.Log("Connected to server!");

            _ = CSharpReceiveLoop();

            // API에서 응답받은 userId를 소켓 서버에 알려주는 패킷 전송
            _ = CsharpSend(new LoginPacket
            {
                type = "LOGIN",
                jwt = AuthManager.Instance.AccessToken
            });
        }
        catch (Exception ex)
        {
            Debug.LogError("C# Server Connection failed: " + ex.Message);
        }
    }

    // C++ 소켓 서버 연결
    public async Task CppConnect()
    {
        try
        {
            // Nagle 알고리즘 끄기
            client = new TcpClient();
            client.NoDelay = true;
            client.Client.NoDelay = true;

            await client.ConnectAsync("127.0.0.1", 7777);  // IP, Port 번호 설정

            stream = client.GetStream();

            connected = true;
            useCppServer = true;

            Debug.Log("Connected to server!");

            _ = CppReceiveLoop();

            OnLogin();

        }
        catch (Exception ex)
        {
            Debug.LogError("C++ Server Connection failed: " + ex.Message);
        }
    }

    // C# 전용 패킷 전송 함수
    // Packet(JSON) -> string -> byte
    public async Task CsharpSend(object obj)
    {
        if (!connected) return;

        string json = JsonConvert.SerializeObject(obj);
        byte[] data = Encoding.UTF8.GetBytes(json + "\n");

        await stream.WriteAsync(data, 0, data.Length);
    }

    // C++ 전용 패킷 전송 함수
    public async Task CppSend(ReadOnlyMemory<byte> packet)
    {
        if (!connected) return;

        await stream.WriteAsync(packet);
    }

    // 패킷 헤더만 가공하는 함수
    public async Task CppSendHeaderOnly(C2S_HeaderType type)
    {
        PacketHeader header = new PacketHeader
        {
            type = (ushort)type,
            size = PacketHeader.Size
        };

        // 헤더를 buffer에 Write
        byte[] buffer = new byte[PacketHeader.Size];
        MemoryMarshal.Write(buffer.AsSpan(), ref header);

        await CppSend(buffer);
    }

    // 로그인 완료 후, C++ 서버에 JWT Token 보내는 함수
    public void OnLogin()
    {
        string token = AuthManager.Instance.AccessToken;

        byte[] jwtBytes = Encoding.UTF8.GetBytes(token);

        PacketHeader header = new PacketHeader
        {
            type = (ushort)C2S_HeaderType.LOGIN,
            size = (ushort)(PacketHeader.Size + jwtBytes.Length)
        };

        // 동적으로 패킷 사이즈에 맞게 byte 배열 생성
        byte[] buffer = new byte[header.size];

        // 헤더 크기만큼 먼저 메모리 복사
        MemoryMarshal.Write(buffer.AsSpan(0, PacketHeader.Size), ref header);

        // 헤더 이후 부터 바디 작성
        jwtBytes.CopyTo(buffer.AsSpan(PacketHeader.Size));

        _ = CppSend(buffer);
    }

    // C# Recv 루프
    private async Task CSharpReceiveLoop()
    {
        while (connected)
        {
            try
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);

                // TCP 스트림에서 패킷 헤더를 수신
                // 수신 실패 시 서버 연결 종료로 판단
                if (read <= 0)
                {
                    Debug.Log("Disconnected from server.");
                    connected = false;
                    break;
                }

                string text = Encoding.UTF8.GetString(buffer, 0, read);
                recvBuffer.Append(text);

                ProcessLinePackets(recvBuffer);
            }
            catch (Exception e)
            {
                Debug.LogError("ReceiveLoop Exception: " + e);
                connected = false;
                break;
            }
        }
    }

    // 패킷 파싱 후, 이벤트 넘겨주기
    private void ProcessLinePackets(StringBuilder recvBuffer)
    {
        while (true)
        {
            int idx = recvBuffer.ToString().IndexOf('\n');
            if (idx < 0) break;

            string packet = recvBuffer.ToString(0, idx).Trim();
            recvBuffer.Remove(0, idx + 1);

            CSharpPacketRouter.Route(packet);
        }
    }

    // C++ Recv 루프
    private async Task CppReceiveLoop()
    {
        byte[] headerBuf = new byte[PacketHeader.Size];

        while (connected)
        {
            try
            {
                // TCP 스트림에서 패킷 헤더를 수신
                // 수신 실패 시 서버 연결 종료로 판단
                if (!await ReadExactAsync(headerBuf, PacketHeader.Size))
                {
                    Debug.Log("Server disconnected (header).");
                    connected = false;
                    break;
                }

                PacketHeader header =
                    MemoryMarshal.Read<PacketHeader>(headerBuf);

                // 바디 사이즈 제한 0 ~ 4096
                int bodySize = header.size - PacketHeader.Size;
                if (bodySize < 0 || bodySize > 4096)
                {
                    Debug.LogError($"Invalid bodySize={bodySize}");
                    break;
                }

                byte[] body = null;

                // 바디가 존재하면, 바디 크기만큼 수신 버퍼 생성
                if (bodySize > 0)
                {
                    body = new byte[bodySize];

                    // TCP 스트림에서 바디를 끝까지 읽지 못하면 연결 종료로 처리
                    if (!await ReadExactAsync(body, bodySize))
                    {
                        Debug.Log("Server disconnected (body).");
                        break;
                    }
                }

                // 패킷 넘겨주기
                CppPacketRouter.Route(header.type, body);
            }
            catch (Exception e)
            {
                Debug.LogError("ReceiveLoopCpp Exception: " + e);
                connected = false;
                break;
            }
        }
    }

    // TCP 스트림에서 지정한 바이트 수(size)를 끝까지 읽으면 true 반환
    private async Task<bool> ReadExactAsync(byte[] buffer, int size)
    {
        int received = 0;

        while (received < size)
        {
            int read = await stream.ReadAsync(
                buffer,
                received,
                size - received
            );

            if (read <= 0) return false; // disconnected

            received += read;
        }

        return true;
    }

    // 매칭 성공 시, UI 업데이트
    public void OnMatchFound()
    {
        MatchingTime mt = FindAnyObjectByType<MatchingTime>();

        if (mt != null)
            mt.SuccessMatching();
    }

    // HP 변경 시, UI 업데이트
    public void UpdateHP(GameObject target, int currentHP)
    {
        Health targetHealth = target.GetComponent<Health>();
        targetHealth.currentHp = currentHP;
        targetHealth.UpdateHPBar();
    }

    // 클라이언트에서 Disconnect
    public void Disconnect()
    {
        try
        {
            connected = false;

            if (stream != null)
            {
                stream.Close();
                stream = null;
            }

            if (client != null)
            {
                client.Close();
                client = null;
            }

            Debug.Log("SocketClient disconnected.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Disconnect error: " + ex.Message);
        }
    }

    // 객체 파괴 및 게임 종료 시, Disconnect
    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}
