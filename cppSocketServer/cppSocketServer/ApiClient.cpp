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

bool ApiClient::Post(const std::string& url, const std::string& json)
{
    CURL* curl = curl_easy_init();
    if (!curl) return false;

    struct curl_slist* headers = nullptr;
    headers = curl_slist_append(headers, "Content-Type: application/json");

    curl_easy_setopt(curl, CURLOPT_URL, baseUrl + url.c_str());
    curl_easy_setopt(curl, CURLOPT_POSTFIELDS, json.c_str());
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
    curl_easy_setopt(curl, CURLOPT_TIMEOUT, 5L);

    CURLcode res = curl_easy_perform(curl);

    long httpCode = 0;
    curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &httpCode);

    curl_slist_free_all(headers);
    curl_easy_cleanup(curl);

    return (res == CURLE_OK && httpCode >= 200 && httpCode < 300);
}

