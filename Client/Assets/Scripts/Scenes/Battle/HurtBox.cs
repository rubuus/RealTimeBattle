using UnityEngine;

public class HurtBox : MonoBehaviour
{
    public int PlayerId {  get; private set; }
    public void Initialize(int id) { PlayerId = id; }
}
