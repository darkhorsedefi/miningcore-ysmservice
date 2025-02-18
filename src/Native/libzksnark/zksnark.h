#pragma once

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>

// ZKSNARKキーペア生成関数
void zksnark_generate_keypair(uint8_t* proving_key, uint8_t* verification_key);

// 証明生成関数
void zksnark_generate_proof(const uint8_t* proving_key, const uint8_t* input, uint8_t* proof);

// 検証関数
bool zksnark_verify(const uint8_t* verification_key, const uint8_t* proof, const uint8_t* input);

// ハッシュ関数インターフェース（miningcoreのハッシュアルゴリズムとして使用）
void zksnark_hash(const uint8_t* input, uint8_t* output);

#ifdef __cplusplus
}
#endif