#pragma once

#include <stdint.h>
#include <stddef.h>

#ifdef _WIN32
    #define XE_EXPORT __declspec(dllexport)
#else
    #define XE_EXPORT __attribute__((visibility("default")))
#endif

extern "C" {


XE_EXPORT void __cdecl xehash(const uint8_t* input, uint8_t* output);

}
