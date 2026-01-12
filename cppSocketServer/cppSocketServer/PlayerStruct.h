#pragma once
#include <cstdint>

enum class PlayerState : uint8_t
{
    Idle = 0,
    Run = 1,
    Jump = 2,
    GroundDash = 3,
    AirDash = 4,
    Punch = 5,
    Hurt = 6
};

// pair 대신 Vector 구조체를 정의해 의미 명시
struct Vector2 {
    float x;
    float y;

    Vector2() : x(0), y(0) {}
    Vector2(float x, float y) : x(x), y(y) {}

    Vector2 operator+(const Vector2& r) const {
        return { x + r.x, y + r.y };
    }

    Vector2 operator*(float s) const {
        return { x * s, y * s };
    }
};

struct Dash
{
    float duration;
    float speed;
    float timer;
    float cooldown;
    float cooldownTimer;
};

struct Punch
{
    float duration;
    float timer;
    float cooldown;
    float cooldownTimer;
};