#include "cn_slow_hash.hpp"
#include "keccak.h"
#include <memory.h>
#include <stdint.h>
#include "xehash.h"

extern "C" {
// Main XeHash function implementation
void xehash(const uint8_t* input, uint8_t* output)
{
    // First pass - Keccak
    //keccak(input, length, output, 32);
    
    // Second pass - CryptoNight v4 variant
    cn_v4_hash_t* ctx = new cn_v4_hash_t();
    ctx->hash(input, sizeof(input), output);
}
}