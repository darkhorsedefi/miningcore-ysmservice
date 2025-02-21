#pragma once

#include <stdint.h>
#include <stddef.h>

#ifdef _WIN32
    #define XE_EXPORT __declspec(dllexport)
    #define XE_CALL __cdecl
#else
    #define XE_EXPORT __attribute__((visibility("default")))
    #define XE_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

XE_EXPORT void XE_CALL xehash(const uint8_t* input, uint8_t* output);

#ifdef __cplusplus
}
#endif
