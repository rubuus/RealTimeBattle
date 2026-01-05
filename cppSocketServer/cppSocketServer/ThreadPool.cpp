#include "ThreadPool.h"

ThreadPool::ThreadPool(size_t threadCount)
    : stop(false)
{
    for (size_t i = 0; i < threadCount; ++i)
    {
        workers.emplace_back(&ThreadPool::WorkerLoop, this);
    }
}

ThreadPool::~ThreadPool()
{
    stop = true;
    cv.notify_all();

    for (auto& t : workers)
    {
        if (t.joinable())
            t.join();
    }
}

void ThreadPool::Enqueue(std::function<void()> job)
{
    {
        std::lock_guard<std::mutex> lock(mtx);
        jobs.push(std::move(job));
    }
    cv.notify_one();
}

void ThreadPool::WorkerLoop()
{
    while (true)
    {
        std::function<void()> job;

        {
            std::unique_lock<std::mutex> lock(mtx);
            cv.wait(lock, [this] {
                return stop || !jobs.empty();
                });

            if (stop && jobs.empty())
                return;

            job = std::move(jobs.front());
            jobs.pop();
        }

        job(); // 실제 작업 실행
    }
}
