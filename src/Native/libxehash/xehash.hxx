#ifndef CN_GPU_H
#define CN_GPU_H

#include <stddef.h>

// __cplusplus tells the compiler that inside code is compiled with the c++ compiler
#ifdef __cplusplus
// extern "C" tells C++ compiler exports the symbols without a name manging.
extern "C" {
  #endif
  void* new_ctx();
  void del_ctx(void* ctx);
  int variant_version(void* ctx);
  void cn_hash(void* ctx, const void* in, size_t len, void* out);
  #ifdef __cplusplus
}
#endif
#endif
