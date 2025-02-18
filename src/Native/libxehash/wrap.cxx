#include "xehash.hxx"
#include "cn_slow_hash.hpp"

void* new_ctx() {
	cn_v4_hash_t *ctx = new cn_v4_hash_t();
	return ctx;
}

void del_ctx(void* ctx) {
	delete (cn_v4_hash_t*)ctx;
}

void cn_hash(void* ctx, const void* in, size_t len, void* out) {
	((cn_v4_hash_t*)ctx)->hash(in, len, out);
}

int variant_version(void* ctx) {
	return ((cn_v4_hash_t*)ctx)->variant_version();
}
