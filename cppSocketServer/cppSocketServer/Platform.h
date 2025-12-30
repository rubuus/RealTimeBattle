#pragma once

class Platform {
public:
	float minX;
	float maxX;
	float Y;

	Platform(float minX, float maxX, float Y):
		minX(minX), maxX(maxX), Y(Y) {
	}
};