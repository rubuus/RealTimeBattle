#pragma once
#include <string>

class ApiClient
{
public:
    static ApiClient& Instance();

    bool Post(const std::string& url, const std::string& jsonBody);

private:
    ApiClient();
    std::string baseUrl;
};
