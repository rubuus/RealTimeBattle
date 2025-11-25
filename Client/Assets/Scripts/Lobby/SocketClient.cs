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
    private StringBuilder recvBuffer = new StringBuilder();

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
        byte[] data = Encoding.UTF8.GetBytes(msg + "\n");
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

                    Debug.Log("[SERVER PACKET] " + packet);
                    HandlePacket(packet);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("ReceiveLoop Exception: " + e);
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

            MatchButton.Instance.isMatching = false;

            // UI에게 매칭 성공 알림
            MatchingTime instance = FindAnyObjectByType<MatchingTime>();

            if (instance != null)
                instance.SuccessMatching();
        }
        else if (msg.Contains("\"type\":\"PLAYER_MOVE\""))
        {
            if (SceneManager.GetActiveScene().name != "Battle")
                return;

            // GameManager 준비 안 됐으면 버리기
            if (GameManager.Instance == null || GameManager.Instance.enemyPlayer == null)
                return;

            PlayerMovePacket p = JsonUtility.FromJson<PlayerMovePacket>(msg);

            // 내 패킷이면 무시
            if (p.id == myUserId)
                return;

            if (p.id != myUserId)
            {
                var enemy = GameManager.Instance.enemyPlayer; 

                if (enemy != null)
                {
                    var pc = enemy.GetComponent<EnemyController>();

                    pc.EnemyStateUpdate(new Vector2(p.x, p.y), p.state);
                }
            }
        }
        else if (msg == "GAME_END")
        {
            enemyDisconnected = false;
            Disconnect();
            SceneManager.LoadScene("Result");
        }
        else if (msg == "ENEMY_EXIT")
        {
            enemyDisconnected = true;
            Disconnect();
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
