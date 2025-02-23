#ifndef XEVAN_H
#define XEVAN_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// Forward declaration
struct work;

// Hash function
void xevan_hash(const char* input, char* output, uint32_t len);

// Optional: For compatibility with existing implementations
void xevan_regenhash(struct work *work);

#ifdef __cplusplus
}
#endif

#endif /* XEVAN_H */