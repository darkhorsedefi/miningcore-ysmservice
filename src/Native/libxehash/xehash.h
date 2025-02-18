#pragma once

#ifdef _WIN32
#define XE_EXPORT __declspec(dllexport)
#else
#define XE_EXPORT
#endif

#ifdef __cplusplus
extern "C" {
#endif

XE_EXPORT void xehash(const unsigned char* input, unsigned char* output);

#ifdef __cplusplus
}
#endif
