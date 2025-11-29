using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Transform leftSpawn;
    [SerializeField] private Transform rightSpawn;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    private float timer = 100f;
    [SerializeField] private TMP_Text timerText;

    public static GameManager Instance;

    public GameObject myPlayer;
    public GameObject enemyPlayer;

    private void Awake()
    {
        Instance = this;
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

        myPlayer = Instantiate(playerPrefab, mySpawn.position, Quaternion.identity);

        var myHurtBox = myPlayer.GetComponentInChildren<HurtBox>();
        if (myHurtBox != null)
            myHurtBox.Initialize(SocketClient.Instance.myUserId);

        Invoke(nameof(EnablePlayerNetwork), 0.3f);


        enemyPlayer = Instantiate(enemyPrefab, enemySpawn.position, Quaternion.identity);

        var enemyHurtBox = enemyPlayer.GetComponentInChildren<HurtBox>();
        if (enemyHurtBox != null)
            enemyHurtBox.Initialize(SocketClient.Instance.enemyUserId);


        // HP 참고용
        var myHealth = myPlayer.GetComponent<Health>();
        var enemyHealth = enemyPlayer.GetComponent<Health>();

        Invoke(nameof(EnableEnemyNetwork), 0.3f);

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
