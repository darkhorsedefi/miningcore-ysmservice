#pragma once

#include <stdint.h>
#include <stddef.h>

#ifdef _WIN32
#define XE_EXPORT __declspec(dllexport)
#else
#define XE_EXPORT
#endif

#ifdef __cplusplus
extern "C" {
#endif

XE_EXPORT void xehash(const uint8_t* input, uint8_t* output);

#ifdef __cplusplus
}
#endif
