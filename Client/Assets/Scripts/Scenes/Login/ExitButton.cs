using UnityEngine;

/*
 * ExitButton.cs
 * 
 * 역할 :
 * - 로그인 씬에서 ExitButton 누를 시, 게임 종료
 */

public class ExitButton : MonoBehaviour
{
    public void ExitGame()
    {
        Application.Quit();
    }
}
