#include "ApiClient.h"
#include <curl/curl.h>
#include <iostream>

ApiClient::ApiClient()
{
    baseUrl = "http://localhost:5146/";
}

ApiClient& ApiClient::Instance()
{
    static ApiClient instance;
    return instance;
}

// API 서버에 JSON 데이터를 HTTP POST로 전송하는 함수
bool ApiClient::Post(const std::string& url, const std::string& json)
{
    CURL* curl = curl_easy_init();
    if (!curl) return false; // 메모리 부족 or 객체 생성 불가 시, 방어

    // API 서버에 JSON 보낸다고 헤더에 명시
    struct curl_slist* headers = nullptr;
    headers = curl_slist_append(headers, "Content-Type: application/json");

    // Base URL + End Point
    std::string fullUrl = baseUrl + url;

    curl_easy_setopt(curl, CURLOPT_URL, fullUrl.c_str());       // URL 설정
    curl_easy_setopt(curl, CURLOPT_POSTFIELDS, json.c_str());   // POST 요청 바디 설정
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);        // Header 설정
    curl_easy_setopt(curl, CURLOPT_TIMEOUT, 5L);                // 최대 5초 대기

    // HTTP 요청 전송 및 응답 수신
    CURLcode res = curl_easy_perform(curl); 

    // Response 된 HTTP code 확인
    long httpCode = 0;
    curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &httpCode);

    // curl 정리
    curl_slist_free_all(headers);
    curl_easy_cleanup(curl);

    // 네트워크 성공 && HTTP 2xx 응답이면 성공으로 판단
    return (res == CURLE_OK && httpCode >= 200 && httpCode < 300);
}

