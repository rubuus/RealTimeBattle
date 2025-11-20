using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    private TcpClient client;
    private NetworkStream stream;

    public Action OnMatchReady;

    private byte[] buffer = new byte[1024];
    private bool connected = false;
    public bool IsConnected => connected;
    public bool enemyDisconnected = false;

    public int myUserId;
    public int enemyUserId;
    public int roomId;
    public string side;

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

            Send($"LOGIN|{AuthManager.Instance.UserId}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
        }
    }

    public async void Send(string msg)
    {
        if (!connected) return;
        byte[] data = Encoding.UTF8.GetBytes(msg);
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

                string msg = Encoding.UTF8.GetString(buffer, 0, read);
                Debug.Log("[SERVER] " + msg);

                HandlePacket(msg);
            }
            catch
            {
                Debug.Log("Connection lost.");
                connected = false;
                break;
            }
        }
    }

    private void HandlePacket(string msg)
    {
        if (msg.StartsWith("MATCH_FOUND"))
        {
            var parts = msg.Split('|');

            roomId = int.Parse(parts[1]);
            myUserId = int.Parse(parts[2]);
            enemyUserId = int.Parse(parts[3]);
            side = parts[4];

            Debug.Log($"매칭 성공! room={roomId}, myUserId={myUserId}, enemyUserId={enemyUserId}, side={side}");

            // ★★★ UI에게 매칭 성공 알림 ★★★
            MatchingTime instance = FindAnyObjectByType<MatchingTime>();
            if (instance != null)
                instance.SuccessMatching();   
        }
        else if (msg == "ENEMY_EXIT")
        {
            enemyDisconnected = true;
            SceneManager.LoadScene("Result");
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


    private void OnApplicationQuit()
    {
        if (client != null) client.Close();
    }
}
