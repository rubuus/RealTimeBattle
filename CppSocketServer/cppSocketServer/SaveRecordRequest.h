#pragma once
#include <nlohmann/json.hpp>

struct SaveRecordRequest
{
	int WinnerId;
	int LoserId;
};

inline void to_json(nlohmann::json& j, const SaveRecordRequest& r)
{
    j = nlohmann::json{
        {"winnerId", r.WinnerId},
        {"loserId",  r.LoserId}
    };
}