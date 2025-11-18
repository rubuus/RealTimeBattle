using UnityEngine;

public class GameManager : MonoBehaviour
{
    public Transform leftSpawn;
    public Transform rightSpawn;

    public GameObject playerPrefab;

    void Start()
    {
        SpawnPlayersRandom();
    }

    void SpawnPlayersRandom()
    {
        bool flip = Random.value > 0.5f;

        Transform mySpawn = flip ? rightSpawn : leftSpawn;
        Transform enemySpawn = flip ? leftSpawn : rightSpawn;

        var myPlayer = Instantiate(playerPrefab, mySpawn.position, Quaternion.identity);
        myPlayer.GetComponent<PlayerController>().isLocalPlayer = true;

        var enemyPlayer = Instantiate(playerPrefab, enemySpawn.position, Quaternion.identity);
        enemyPlayer.GetComponent<PlayerController>().isLocalPlayer = false;

        myPlayer.GetComponent<PlayerController>().isLeftSide = (mySpawn == leftSpawn);
        enemyPlayer.GetComponent<PlayerController>().isLeftSide = (enemySpawn == leftSpawn);

    }
}
