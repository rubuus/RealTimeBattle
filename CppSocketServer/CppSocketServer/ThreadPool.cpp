#include "ThreadPool.h"

// Worker Thread 생성
ThreadPool::ThreadPool(size_t threadCount)
    : stop(false)
{
    for (size_t i = 0; i < threadCount; ++i)
        workers.emplace_back(&ThreadPool::WorkerLoop, this);
}

ThreadPool::~ThreadPool()
{
	// Stop Flag 설정
    {
        std::lock_guard<std::mutex> lock(mtx);
        stop = true;
    }

    // 대기 중인 모든 Worker Thread 깨우기
    cv.notify_all();

	// Thread 종료 대기
    for (auto& t : workers)
        if (t.joinable())
            t.join();
}

// Worker Thread에 잡 할당
bool ThreadPool::Enqueue(std::function<void()> job)
{
    {
        std::lock_guard<std::mutex> lock(mtx);
        if (stop) return false;
        jobs.push(std::move(job));
    }

    // 대기 중인 Thread 하나 깨우기
    cv.notify_one();

    return true;
}

void ThreadPool::WorkerLoop()
{
    while (true)
    {
        std::function<void()> job;

        // 락 잡고 조건 확인
        {
            std::unique_lock<std::mutex> lock(mtx);

            // 작업이 들어오거나 종료 신호가 올 때까지 대기
            cv.wait(lock, [this] {
                return stop || !jobs.empty();
                });

			// 종료 조건 확인
            if (stop && jobs.empty())
                return;

            job = std::move(jobs.front());
            jobs.pop();
        }

		// 잡 실행
        job();
    }
}