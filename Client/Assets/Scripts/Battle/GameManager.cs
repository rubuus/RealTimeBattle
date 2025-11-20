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

    void Start()
    {
        SpawnPlayersRandom();
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

    void SpawnPlayersRandom()
    {
        // 내 위치 결정
        Transform mySpawn = (SocketClient.Instance.side == "LEFT") ? leftSpawn : rightSpawn;

        // 상대 위치는 반대
        Transform enemySpawn = (SocketClient.Instance.side == "LEFT") ? rightSpawn : leftSpawn;

        // 내 플레이어 스폰
        var myPlayer = Instantiate(playerPrefab, mySpawn.position, Quaternion.identity);
        myPlayer.GetComponent<PlayerController>().isLocalPlayer = true;
        myPlayer.GetComponent<PlayerController>().isLeftSide = (SocketClient.Instance.side == "LEFT");

        // 상대 플레이어 스폰
        var enemyPlayer = Instantiate(playerPrefab, enemySpawn.position, Quaternion.identity);
        enemyPlayer.GetComponent<PlayerController>().isLocalPlayer = false;
        enemyPlayer.GetComponent<PlayerController>().isLeftSide = (SocketClient.Instance.side == "RIGHT");

        // HP 참고 용
        var myHealth = myPlayer.GetComponent<Health>();
        var enemyHealth = enemyPlayer.GetComponent<Health>();
    }
}
