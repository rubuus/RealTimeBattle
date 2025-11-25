using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Transform leftSpawn;
    [SerializeField] private Transform rightSpawn;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    private float timer = 10f;
    [SerializeField] private TMP_Text timerText;

    public static GameManager Instance;

    public GameObject myPlayer;
    public GameObject enemyPlayer;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SpawnPlayers();
    }

    void Update()
    {
        RunTimer();
    }

    void RunTimer()
    {
        timer -= Time.deltaTime;

        if (timerText != null)
            timerText.text = Mathf.Ceil(timer).ToString();

        if (timer <= 0f)
        {
            SceneManager.LoadScene("Result");
        }
    }

    void SpawnPlayers()
    {
        Transform mySpawn = (SocketClient.Instance.side == "LEFT") ? leftSpawn : rightSpawn;
        Transform enemySpawn = (SocketClient.Instance.side == "LEFT") ? rightSpawn : leftSpawn;

        // 내 플레이어는 PlayerPrefab (PlayerController)
        myPlayer = Instantiate(playerPrefab, mySpawn.position, Quaternion.identity);
        var myPC = myPlayer.GetComponent<PlayerController>();
        myPC.isLeftSide = (SocketClient.Instance.side == "LEFT");
        myPC.isLocalPlayer = true;

        int myDir = (SocketClient.Instance.side == "LEFT") ? 1 : -1;
        myPC.transform.localScale = new Vector3(myDir, 1, 1);

        // 상대 플레이어는 EnemyPrefab (EnemyController)
        enemyPlayer = Instantiate(enemyPrefab, enemySpawn.position, Quaternion.identity);

        int enemyDir = (SocketClient.Instance.side == "LEFT") ? -1 : 1;
        enemyPlayer.transform.localScale = new Vector3(enemyDir, 1, 1);

        // HP 참고용
        var myHealth = myPlayer.GetComponent<Health>();
        var enemyHealth = enemyPlayer.GetComponent<Health>();

        Invoke(nameof(EnableEnemyNetwork), 0.3f);
        SocketClient.Instance.Send("BATTLE_READY");
    }

    void EnableEnemyNetwork()
    {
        enemyPlayer.GetComponent<EnemyController>().EnableNetwork();
    }
}
