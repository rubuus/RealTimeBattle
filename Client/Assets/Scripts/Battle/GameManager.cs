using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Transform leftSpawn;
    [SerializeField] private Transform rightSpawn;
    [SerializeField] private GameObject playerPrefab;

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
        // 내 위치 결정
        Transform mySpawn = (SocketClient.Instance.side == "LEFT") ? leftSpawn : rightSpawn;

        // 상대 위치는 반대
        Transform enemySpawn = (SocketClient.Instance.side == "LEFT") ? rightSpawn : leftSpawn;

        // 내 플레이어 스폰
        myPlayer = Instantiate(playerPrefab, mySpawn.position, Quaternion.identity);
        var myPC = myPlayer.GetComponent<PlayerController>();
        myPC.isLeftSide = (SocketClient.Instance.side == "LEFT");
        myPC.isLocalPlayer = true;

        int myDir = (SocketClient.Instance.side == "LEFT") ? 1 : -1;
        myPC.transform.localScale = new Vector3(myDir, 1, 1);

        // 상대 플레이어 스폰
        enemyPlayer = Instantiate(playerPrefab, enemySpawn.position, Quaternion.identity);
        var enemyPC = enemyPlayer.GetComponent<PlayerController>();
        enemyPC.isLeftSide = (SocketClient.Instance.side == "RIGHT");
        enemyPC.isLocalPlayer = false;

        int enemyDir = (SocketClient.Instance.side == "LEFT") ? -1 : 1;
        enemyPC.transform.localScale = new Vector3(enemyDir, 1, 1);

        // HP 참고 용
        var myHealth = myPlayer.GetComponent<Health>();
        var enemyHealth = enemyPlayer.GetComponent<Health>();

        Invoke(nameof(EnableEnemyNetwork), 0.3f);
        SocketClient.Instance.Send("BATTLE_READY");
    }

    void EnableEnemyNetwork()
    {
        enemyPlayer.GetComponent<PlayerController>().EnableNetwork();
    }
}
