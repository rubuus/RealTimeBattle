using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    private TcpClient client;
    private NetworkStream stream;

    private byte[] buffer = new byte[1024];
    private StringBuilder recvBuffer = new StringBuilder();

    public bool connected = false;
    public bool enemyDisconnected = false;
    public bool useCppServer = false;

    public int myId;
    public int enemyId;
    public int roomId;
    public string side;

    public string finalResult = "DRAW";

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

    public async Task Connect()
    {
        try
        {
            client = new TcpClient();
            client.NoDelay = true;
            client.Client.NoDelay = true;
            await client.ConnectAsync("127.0.0.1", 5000);  // 서버 주소

            stream = client.GetStream();
            connected = true;
            useCppServer = false;
            Debug.Log("Connected to server!");

            _ = ReceiveLoop();

            _= Send(new LoginPacket
            {
                type = "LOGIN",
                userId = AuthManager.Instance.UserId
            });
        }
        catch (Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
        }
    }

    public async Task CppConnect()
    {
        try
        {
            client = new TcpClient();
            client.NoDelay = true;
            client.Client.NoDelay = true;
            await client.ConnectAsync("127.0.0.1", 7777);  // 서버 주소

            stream = client.GetStream();
            connected = true;
            useCppServer = true;
            Debug.Log("Connected to server!");

            _ = ReceiveLoopCpp();

            OnLogin();

        }
        catch (Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
        }
    }

    public async Task Send(object obj)
    {
        if (!connected) return;

        string json = JsonUtility.ToJson(obj);
        byte[] data = Encoding.UTF8.GetBytes(json + "\n");
        await stream.WriteAsync(data, 0, data.Length);
    }

    public async Task SendHeaderOnlyAsync(C2S_PacketType type)
    {
        PacketHeader header = new PacketHeader
        {
            type = (ushort)type,
            size = PacketHeader.Size
        };

        byte[] buffer = new byte[PacketHeader.Size];
        MemoryMarshal.Write(buffer.AsSpan(), ref header);

        await SendAsync(buffer);
    }

    public async Task SendAsync(ReadOnlyMemory<byte> packet)
    {
        if (!connected) return;

        await stream.WriteAsync(packet);
    }

    public void OnLogin()
    {
        CppLoginPacket loginPacket = new CppLoginPacket
        {
            userId = AuthManager.Instance.UserId
        };

        ReadOnlySpan<byte> body =
            MemoryMarshal.AsBytes(
                MemoryMarshal.CreateReadOnlySpan(ref loginPacket, 1)
            );

        PacketHeader header = new PacketHeader
        {
            type = (ushort)C2S_PacketType.LOGIN,
            size = (ushort)(PacketHeader.Size + body.Length)
        };

        byte[] buffer = new byte[header.size];

        // header
        MemoryMarshal.Write(buffer.AsSpan(0, PacketHeader.Size), ref header);

        // body
        body.CopyTo(buffer.AsSpan(PacketHeader.Size));

        _ = SendAsync(buffer);
    }


    private async Task ReceiveLoop()
    {
        while (connected)
        {
            try
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    Debug.Log("Disconnected from server.");
                    connected = false;
                    break;
                }

                string text = Encoding.UTF8.GetString(buffer, 0, read);
                recvBuffer.Append(text);

                // 줄 단위로 패킷 분리
                while (true)
                {
                    int idx = recvBuffer.ToString().IndexOf('\n');
                    if (idx < 0) break;

                    string packet = recvBuffer.ToString(0, idx).Trim();
                    recvBuffer.Remove(0, idx + 1);

                    PacketRouter.Route(packet);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("ReceiveLoop Exception: " + e);
                connected = false;
                break;
            }
        }
    }

    private async Task ReceiveLoopCpp()
    {
        byte[] headerBuf = new byte[PacketHeader.Size];

        while (connected)
        {
            try
            {
                if (!await ReadExactAsync(headerBuf, PacketHeader.Size))
                {
                    Debug.Log("Server disconnected (header).");
                    break;
                }

                PacketHeader header =
                    MemoryMarshal.Read<PacketHeader>(headerBuf);

                int bodySize = header.size - PacketHeader.Size;
                if (bodySize < 0 || bodySize > 4096)
                {
                    Debug.LogError($"Invalid bodySize={bodySize}");
                    break;
                }

                byte[] body = null;
                if (bodySize > 0)
                {
                    body = new byte[bodySize];
                    if (!await ReadExactAsync(body, bodySize))
                    {
                        Debug.Log("Server disconnected (body).");
                        break;
                    }
                }

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

            if (read <= 0)
                return false; // disconnected

            received += read;
        }

        return true;
    }

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

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void OnMatchFound()
    {
        MatchingTime instance = FindAnyObjectByType<MatchingTime>();

        if (instance != null)
            instance.SuccessMatching();
    }

    public void UpdateHP(GameObject target, int currentHP)
    {
        Health targetHealth = target.GetComponent<Health>();
        targetHealth.currentHp = currentHP;
        targetHealth.UpdateHPBar();
    }
}
