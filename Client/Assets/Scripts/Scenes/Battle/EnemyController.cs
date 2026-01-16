using System;
using UnityEngine;
using static PlayerController;

/*
 * EnemyController.cs
 * 
 * 역할 :
 * - Enemy 오브젝트 상태 업데이트 및 위치 보간
 * 
 */

public class EnemyController : MonoBehaviour
{
    public bool isNetworkUpdatePending = false;

    private PlayerState enemyState = PlayerState.Idle;
    private Vector2 networkTargetPos;
    private float networkDir;
    private string networkTargetState;
    private byte networkTargetStateByte;

    private Vector2 smoothVel;

    [SerializeField]
    private float networkSmoothTime = 0.01f;
    private float SNAP_DIST = 0.7f;

    private Rigidbody2D rigid;
    private Animator anim;

    // 서버 권위를 위해 rigidbody, velocity 초기화
    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rigid.bodyType = RigidbodyType2D.Kinematic;
        rigid.linearVelocity = Vector2.zero;
        smoothVel = Vector2.zero;
        networkDir = (SocketClient.Instance.side == "RIGHT") ? 1f : -1f;
    }

    private void Update()
    {
        if (isNetworkUpdatePending) 
        {
            ApplyServerStateWithGuard();
            isNetworkUpdatePending = false;
        }

        ApplyNetworkPosition();
        ApplyServerDirection();
    }

    // 상태 변화 예외 처리 (중복 방지 및 재전송 패킷 업데이트 방지)
    private void ApplyServerStateWithGuard()
    {
        if (!TryResolvePlayerState(out var newState))
        {
            Debug.LogWarning(
                $"Invalid state. byte={networkTargetStateByte}, string={networkTargetState}");
            isNetworkUpdatePending = false;
            return;
        }

        // Punch 가드 (서버에서 막아주지만 클라에서도 방지용)
        if (enemyState == PlayerState.Punch &&
            newState == PlayerState.Punch)
        {
            isNetworkUpdatePending = false;
            return;
        }

        ChangeState(newState);
        isNetworkUpdatePending = false;
    }

    // 제대로 된 상태 데이터가 전달 됐으면 true
    private bool TryResolvePlayerState(
    out PlayerState resolvedState)
    {
        // C++ 서버 (byte)
        if (SocketClient.Instance.useCppServer)
        {
            if (Enum.IsDefined(typeof(PlayerState), networkTargetStateByte))
            {
                resolvedState = (PlayerState)networkTargetStateByte;
                return true;
            }
        }
        // C# 서버 (string)
        else
        {
            if (!string.IsNullOrEmpty(networkTargetState) &&
                Enum.TryParse<PlayerState>(networkTargetState, out var parsed))
            {
                resolvedState = parsed;
                return true;
            }
        }

        resolvedState = default;
        return false;
    }

    // 상태에 따라 애니메이션 변경
    private void ChangeState(PlayerState newState)
    {
        // 중복 전환 방지
        if (enemyState == newState) return;

        enemyState = newState;

        switch (enemyState)
        {
            case PlayerState.Idle:
                anim.Play("Idle");
                break;

            case PlayerState.Run:
                anim.Play("Run");
                break;

            case PlayerState.Jump:
                anim.Play("Jump");
                break;

            case PlayerState.GroundDash:
                anim.Play("GroundDash");
                break;

            case PlayerState.AirDash:
                anim.Play("AirDash");
                break;

            case PlayerState.Punch:
                anim.Play("Punch");
                break;

            case PlayerState.Hurt:
                anim.Play("Hurt");
                break;
        }
    }

    // C# 서버에서 상태 받아서 저장
    public void ApplyServerState(Vector2 pos, string state, int dir)
    {
        networkTargetPos = pos;
        networkTargetState = state;
        networkDir = dir;
        isNetworkUpdatePending = true;
    }

    // C++ 서버에서 상태 받아서 저장
    public void ApplyServerState(Vector2 pos, byte state, sbyte dir)
    {
        networkTargetPos = pos;
        networkTargetStateByte = state;
        networkDir = dir;
        isNetworkUpdatePending = true;
    }

    // 서버가 보내준 위치로 자연스럽게 보간 (패킷 밀릴 시, 보간 초기화)
    private void ApplyNetworkPosition()
    {
        if (Vector2.Distance(transform.position, networkTargetPos) > SNAP_DIST)
        {
            transform.position = networkTargetPos;
            smoothVel = Vector2.zero;
            anim.speed = 1.0f;
        }
        else
        {
            transform.position = Vector2.SmoothDamp(
                transform.position,
                networkTargetPos,
                ref smoothVel,
                networkSmoothTime
            );
        }
    }

    // 방향 업데이트
    private void ApplyServerDirection()
    {
        transform.localScale = new Vector2(networkDir, 1);
    }
}
