using System;
using System.Runtime.InteropServices;
using UnityEngine;

/*
 * PlayerController.cs
 * 
 * 역할 :
 * - Player 오브젝트 상태 업데이트 및 위치 보간, 입력 전송
 * 
 */

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

public class PlayerController : MonoBehaviour
{
    private PlayerState state = PlayerState.Idle;
    private float sendTimer = 0f;
    private const float SEND_INTERVAL = 0.05f;

    private float moveInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool punchPressed;

    private Vector2 networkTargetPos;
    private float networkDir;
    private string networkTargetState;
    private byte networkTargetStateByte;
    private bool isNetworkUpdatePending = false;

    private Vector2 smoothVel;

    [SerializeField]
    private float networkSmoothTime = 0.03f;

    private Rigidbody2D rigid;
    private Animator anim;

    // 서버 권위를 위해 rigidbody, velocity 초기화
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

        // 지정한 전송 주기(SEND_INTERVAL)를 유지하기 위해
        // 프레임 지연 시 누적된 시간만큼 입력 패킷을 보충 전송
        sendTimer += Time.unscaledDeltaTime;
        while (sendTimer >= SEND_INTERVAL)
        {
            sendTimer -= SEND_INTERVAL;
            SendInput();
        }
    }

    // 유저 입력 저장
    private void ReadInput()
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

        if (state == PlayerState.Punch &&
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
        if (state == newState) return;

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

    // 패킷 전송 후, 값 초기화
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

    // C# 서버 입력 패킷 전송
    private void SendInput_CSharpDTO()
    {
        _ = SocketClient.Instance.CsharpSend(new PlayerInputPacket()
        {
            type = "INPUT",
            id = SocketClient.Instance.myUserId,
            move = moveInput,
            jump = jumpPressed,
            dash = dashPressed,
            punch = punchPressed
        });
    }

    // C++ 서버 입력 패킷 전송
    private void SendInput_CppBinary()
    {
        // 바디 패킷 생성
        var bodyStruct = new CppPlayerInputPacket
        {
            id = SocketClient.Instance.myUserId,
            move = moveInput,
            jump = jumpPressed ? (byte)1 : (byte)0,
            dash = dashPressed ? (byte)1 : (byte)0,
            punch = punchPressed ? (byte)1 : (byte)0
        };

        // 구조체를 복사 없이 byte 단위로 참조 (Span 기반)
        ReadOnlySpan<byte> body =
            MemoryMarshal.AsBytes(
                MemoryMarshal.CreateReadOnlySpan(ref bodyStruct, 1)
            );

        PacketHeader header = new PacketHeader
        {
            type = (ushort)C2S_HeaderType.INPUT,
            size = (ushort)(PacketHeader.Size + body.Length)
        };

        // 패킷 사이즈에 맞게 byte 배열 생성
        byte[] buffer = new byte[header.size];

        // buffer에 헤더 Write
        MemoryMarshal.Write(buffer.AsSpan(0, PacketHeader.Size), ref header);

        // buffer에 헤더 크기 뒤부터 바디 Write
        body.CopyTo(buffer.AsSpan(PacketHeader.Size));

        _ = SocketClient.Instance.CppSend(buffer);
    }

    // 방향 업데이트
    private void ApplyServerDirection()
    {
        transform.localScale = new Vector2(networkDir, 1);
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

    // 서버가 보내준 위치로 자연스럽게 보간
    private void ApplyNetworkPosition()
    {
        transform.position = Vector2.SmoothDamp(
                    transform.position,
                    networkTargetPos,
                    ref smoothVel,
                    networkSmoothTime
                );
    }
}