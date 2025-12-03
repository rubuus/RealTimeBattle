using System;
using UnityEngine;

[System.Serializable]
public class BattleRecordResponse
{
    public int Id;
    public string Result;
    public DateTime FinishedTime;
    public int WinnerId;
    public int LoserId;
    public string WinnerNickname;
    public string LoserNickname;
}