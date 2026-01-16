using TMPro;
using UnityEngine;

/*
 * GameManager.cs
 * 
 * 역할 :
 * - 배틀씬 유저 스폰 및 데이터 저장
 * 
 */

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public TMP_Text timerText;
    public GameObject myPlayer;
    public GameObject enemyPlayer;
    public int myPlayerId = 0;
    public int enemyPlayerId = 0;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    private void Awake()
    {
        Instance = this;

        if (SocketClient.Instance.useCppServer)
            _ = SocketClient.Instance.CppSendHeaderOnly(C2S_HeaderType.BATTLE_START);
        else
            _ = SocketClient.Instance.CsharpSend(new BasePacket { type = "BATTLE_START" });

       SpawnPlayers();
    }

    // 서버에 저장된 값으로 유저 스폰
    private void SpawnPlayers()
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
