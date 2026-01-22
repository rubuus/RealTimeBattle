#pragma once

#include <queue>
#include <mutex>

template<typename T>
class ThreadSafeQueue
{
public:
    bool try_pop(T& out)
    {
        std::lock_guard<std::mutex> lock(m);
        if (q.empty())
            return false;

        out = q.front();
        q.pop();
        return true;
    }

    void push(const T& value)
    {
        std::lock_guard<std::mutex> lock(m);
        q.push(value);
    }

    bool empty() const
    {
        std::lock_guard<std::mutex> lock(m);
        return q.empty();
    }

private:
    mutable std::mutex m;
    std::queue<T> q;
};
