using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    private TcpClient client;
    private NetworkStream stream;

    public Action OnMatchReady;

    private byte[] buffer = new byte[1024];
    private StringBuilder recvBuffer = new StringBuilder();

    public bool connected = false;
    public bool enemyDisconnected = false;

    public int myUserId;
    public int enemyUserId;
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
        await Connect();
    }

    public async Task Connect()
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 5000);  // 서버 주소

            stream = client.GetStream();
            connected = true;
            Debug.Log("Connected to server!");

            _ = ReceiveLoop();

            Send(new LoginPacket
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

    public async void Send(object obj)
    {
        if (!connected) return;

        string json = JsonUtility.ToJson(obj);
        byte[] data = Encoding.UTF8.GetBytes(json + "\n");
        await stream.WriteAsync(data, 0, data.Length);
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
