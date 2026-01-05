#pragma once
#include <vector>
#include <thread>
#include <queue>
#include <functional>
#include <mutex>
#include <condition_variable>
#include <atomic>

class ThreadPool
{
public:
    explicit ThreadPool(size_t threadCount);
    ~ThreadPool();

    // 작업 추가
    void Enqueue(std::function<void()> job);

private:
    void WorkerLoop();

private:
    std::vector<std::thread> workers;
    std::queue<std::function<void()>> jobs;

    std::mutex mtx;
    std::condition_variable cv;
    std::atomic<bool> stop;
};
