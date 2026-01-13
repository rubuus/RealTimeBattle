using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    public TMP_Text timerText;

    public static GameManager Instance;

    public GameObject myPlayer;
    public GameObject enemyPlayer;
    public int myPlayerId = 0;
    public int enemyPlayerId = 0;

    private void Awake()
    {
        Instance = this;

        if (SocketClient.Instance.useCppServer)
            _ = SocketClient.Instance.SendHeaderOnlyAsync(C2S_PacketType.BATTLE_START);

        SpawnPlayers();
    }

    void SpawnPlayers()
    {
        bool left = (SocketClient.Instance.side == "LEFT");

        myPlayer = Instantiate(playerPrefab, Vector2.zero, Quaternion.identity);
        myPlayer.transform.localScale = left ? new Vector3(1, 1, 1) : new Vector3(-1, 1, 1);

        enemyPlayer = Instantiate(enemyPrefab, Vector2.zero, Quaternion.identity);
        enemyPlayer.transform.localScale = left ? new Vector3(-1, 1, 1) : new Vector3(1, 1, 1);

        // HP 참고용
        var myHealth = myPlayer.GetComponent<Health>();
        var enemyHealth = enemyPlayer.GetComponent<Health>();
    }
}
