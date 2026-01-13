using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    public enum PlayerState : byte
    {
        Idle,
        Run,
        Jump,
        GroundDash,
        AirDash,
        Punch,
        Hurt
    }

    private PlayerState state = PlayerState.Idle;
    private float sendTimer = 0f;
    const float SEND_INTERVAL = 0.05f;

    // --- 입력값 (로컬에서만 사용) ---
    private float moveInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool punchPressed;

    // --- 서버에서 받은 권위 좌표/상태 ---
    private Vector2 networkTargetPos;
    private string networkTargetState;
    private byte networkTargetStateByte;
    private bool isNetworkUpdatePending = false;
    private float networkDir;
    [SerializeField] private float networkSmoothTime = 0.03f;

    private Vector2 smoothVel;

    private Rigidbody2D rigid;
    private Animator anim;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        rigid.bodyType = RigidbodyType2D.Kinematic;
        rigid.linearVelocity = Vector2.zero;
        smoothVel = Vector2.zero;
        networkDir = (SocketClient.Instance.side == "LEFT") ? 1f : -1f;
    }

    void Update()
    {
        ReadInput();

        if (isNetworkUpdatePending)
        {
            ApplyServerStateWithGuard();
            isNetworkUpdatePending = false;
        }

        ApplyNetworkPosition();
        ApplyServerDirection();

        sendTimer += Time.unscaledDeltaTime;

        while (sendTimer >= SEND_INTERVAL)
        {
            sendTimer -= SEND_INTERVAL;

            SendInput();
        }
    }

    void ReadInput()
    {
        if (Input.GetKey(KeyCode.RightArrow))
            moveInput = 1;
        else if (Input.GetKey(KeyCode.LeftArrow))
            moveInput = -1;
        else
            moveInput = 0;

        if(Input.GetKeyDown(KeyCode.C)) jumpPressed = true;
        if (Input.GetKeyDown(KeyCode.X)) dashPressed = true;
        if (Input.GetKeyDown(KeyCode.Z)) punchPressed = true;
    }

    private void SendInput()
    {
        if (SocketClient.Instance.useCppServer)
            SendInput_CppBinary();
        else
            SendInput_CSharpDTO();

        jumpPressed = false;
        dashPressed = false;
        punchPressed = false;
    }

    private void SendInput_CSharpDTO()
    {
        _ = SocketClient.Instance.Send(new PlayerInputPacket()
        {
            type = "INPUT",
            id = SocketClient.Instance.myUserId,
            move = moveInput,
            jump = jumpPressed,
            dash = dashPressed,
            punch = punchPressed
        });
    }

    private void SendInput_CppBinary()
    {
        var bodyStruct = new CppPlayerInputPacket
        {
            id = SocketClient.Instance.myUserId,
            move = moveInput,
            jump = jumpPressed ? (byte)1 : (byte)0,
            dash = dashPressed ? (byte)1 : (byte)0,
            punch = punchPressed ? (byte)1 : (byte)0
        };

        // 🔥 body → bytes (복사 없이)
        ReadOnlySpan<byte> body =
            MemoryMarshal.AsBytes(
                MemoryMarshal.CreateReadOnlySpan(ref bodyStruct, 1)
            );

        PacketHeader header = new PacketHeader
        {
            type = (ushort)C2S_PacketType.INPUT,
            size = (ushort)(PacketHeader.Size + body.Length)
        };

        byte[] buffer = new byte[header.size];

        // 1) 헤더 복사
        MemoryMarshal.Write(buffer.AsSpan(0, PacketHeader.Size), ref header);

        // 2) 바디 복사
        body.CopyTo(buffer.AsSpan(PacketHeader.Size));

        _ = SocketClient.Instance.SendAsync(buffer);
    }

    public void ApplyServerDirection()
    {
        transform.localScale = new Vector2(networkDir, 1);
    }

    public void ApplyServerStateWithGuard()
    {
        if (!TryResolvePlayerState(out var newState))
        {
            Debug.LogWarning(
                $"Invalid state. byte={networkTargetStateByte}, string={networkTargetState}");
            isNetworkUpdatePending = false;
            return;
        }

        if (state == PlayerState.Punch &&
            newState == PlayerState.Punch)
        {
            isNetworkUpdatePending = false;
            return;
        }

        ChangeState(newState);
        isNetworkUpdatePending = false;
    }

    public void ApplyServerState(Vector2 pos, string state, int dir)
    {
        networkTargetPos = pos;
        networkTargetState = state;
        networkDir = dir;
        isNetworkUpdatePending = true;
    }

    public void ApplyServerState(Vector2 pos, byte state, sbyte dir)
    {
        networkTargetPos = pos;
        networkTargetStateByte = state;
        networkDir = dir;
        isNetworkUpdatePending = true;
    }

    public void ApplyNetworkPosition()
    {
        transform.position = Vector2.SmoothDamp(
                    transform.position,
                    networkTargetPos,
                    ref smoothVel,
                    networkSmoothTime
                );
    }

    void ChangeState(PlayerState newState)
    {
        Debug.Log($"ChangeState: {state} -> {newState}");
        // 중복 전환 방지
        if (state == newState)
            return;

        state = newState;

        switch (state)
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
}