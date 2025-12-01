using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Transform leftSpawn;
    [SerializeField] private Transform rightSpawn;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    public TMP_Text timerText;

    public static GameManager Instance;

    public GameObject myPlayer;
    public GameObject enemyPlayer;

    private void Awake()
    {
        Instance = this;
        SpawnPlayers();
    }

    void SpawnPlayers()
    {
        bool left = (SocketClient.Instance.side == "LEFT");
        Transform mySpawn = left ? leftSpawn : rightSpawn;
        Transform enemySpawn = left ? rightSpawn : leftSpawn;

        myPlayer = Instantiate(playerPrefab, mySpawn.position, Quaternion.identity);
        myPlayer.transform.localScale = left ? new Vector3(1, 1, 1) : new Vector3(-1, 1, 1);

        enemyPlayer = Instantiate(enemyPrefab, enemySpawn.position, Quaternion.identity);
        enemyPlayer.transform.localScale = left ? new Vector3(-1, 1, 1) : new Vector3(1, 1, 1);

        Invoke(nameof(EnablePlayerNetwork), 0.3f);
        Invoke(nameof(EnableEnemyNetwork), 0.3f);

        // HP 참고용
        var myHealth = myPlayer.GetComponent<Health>();
        var enemyHealth = enemyPlayer.GetComponent<Health>();

        SocketClient.Instance.Send(new BasePacket { type = "BATTLE_START" });
    }

    void EnablePlayerNetwork()
    {
        myPlayer.GetComponent<PlayerController>().EnableNetwork();
    }

    void EnableEnemyNetwork()
    {
        enemyPlayer.GetComponent<EnemyController>().EnableNetwork();
    }
}
