#include "zksnark.h"
#include <libsnark/common/default_types/r1cs_ppzksnark_pp.hpp>
#include <libsnark/zk_proof_systems/ppzksnark/r1cs_ppzksnark/r1cs_ppzksnark.hpp>
#include <libff/algebra/curves/alt_bn128/alt_bn128_pp.hpp>
#include <libsnark/gadgetlib1/protoboard.hpp>
#include <libsnark/gadgetlib1/pb_variable.hpp>
#include <stdexcept>
#include <sstream>
#include <vector>

using namespace libsnark;
using namespace libff;

// バッファーサイズ定数
const size_t PROVING_KEY_SIZE = 1024;
const size_t VERIFICATION_KEY_SIZE = 512;
const size_t PROOF_SIZE = 256;
const size_t HASH_SIZE = 32;

// 初期化関数
void init_zksnark() {
    alt_bn128_pp::init_public_params();
}

// シリアライズヘルパー関数
template<typename T>
std::vector<uint8_t> serialize_object(const T& obj) {
    std::stringstream ss;
    ss << obj;
    std::string str = ss.str();
    return std::vector<uint8_t>(str.begin(), str.end());
}

template<typename T>
T deserialize_object(const uint8_t* data, size_t length) {
    std::string str(reinterpret_cast<const char*>(data), length);
    std::stringstream ss(str);
    T obj;
    ss >> obj;
    return obj;
}

// マイニング用の制約システムを作成
r1cs_constraint_system<Fr<alt_bn128_pp>> create_mining_constraint_system() {
    protoboard<Fr<alt_bn128_pp>> pb;
    
    // 入力変数の割り当て
    pb_variable<Fr<alt_bn128_pp>> x;
    pb_variable<Fr<alt_bn128_pp>> y;
    pb_variable<Fr<alt_bn128_pp>> out;
    
    x.allocate(pb, "x");
    y.allocate(pb, "y");
    out.allocate(pb, "out");
    
    // マイニング制約の定義
    // x * y = out の制約を追加
    pb.add_r1cs_constraint(r1cs_constraint<Fr<alt_bn128_pp>>(x, y, out));
    
    return pb.get_constraint_system();
}

// キーペア生成の完全実装
void zksnark_generate_keypair(uint8_t* proving_key, uint8_t* verification_key) {
    init_zksnark();
    
    // マイニング用の制約システムを作成
    auto constraint_system = create_mining_constraint_system();
    
    // キーペアを生成
    r1cs_ppzksnark_keypair<alt_bn128_pp> keypair = r1cs_ppzksnark_generator<alt_bn128_pp>(constraint_system);
    
    // キーペアをシリアライズ
    auto pk_bytes = serialize_object(keypair.pk);
    auto vk_bytes = serialize_object(keypair.vk);
    
    // 出力バッファにコピー
    memcpy(proving_key, pk_bytes.data(), std::min(pk_bytes.size(), size_t(PROVING_KEY_SIZE)));
    memcpy(verification_key, vk_bytes.data(), std::min(vk_bytes.size(), size_t(VERIFICATION_KEY_SIZE)));
}

// 証明生成の完全実装
void zksnark_generate_proof(const uint8_t* proving_key, const uint8_t* input, uint8_t* proof) {
    init_zksnark();
    
    // proving_keyをデシリアライズ
    auto pk = deserialize_object<r1cs_ppzksnark_proving_key<alt_bn128_pp>>(
        proving_key, PROVING_KEY_SIZE);
    
    // 入力データから値を構築
    protoboard<Fr<alt_bn128_pp>> pb;
    pb_variable<Fr<alt_bn128_pp>> x;
    pb_variable<Fr<alt_bn128_pp>> y;
    x.allocate(pb, "x");
    y.allocate(pb, "y");
    
    // 入力値を設定
    pb.val(x) = Fr<alt_bn128_pp>(input[0]);
    pb.val(y) = Fr<alt_bn128_pp>(input[1]);
    
    // 証明を生成
    const r1cs_ppzksnark_proof<alt_bn128_pp> generated_proof = r1cs_ppzksnark_prover<alt_bn128_pp>(
        pk, pb.primary_input(), pb.auxiliary_input());
    
    // 証明をシリアライズ
    auto proof_bytes = serialize_object(generated_proof);
    memcpy(proof, proof_bytes.data(), std::min(proof_bytes.size(), size_t(PROOF_SIZE)));
}

// 検証の完全実装
bool zksnark_verify(const uint8_t* verification_key, const uint8_t* proof, const uint8_t* input) {
    init_zksnark();
    
    // verification_keyをデシリアライズ
    auto vk = deserialize_object<r1cs_ppzksnark_verification_key<alt_bn128_pp>>(
        verification_key, VERIFICATION_KEY_SIZE);
    
    // proofをデシリアライズ
    auto zkproof = deserialize_object<r1cs_ppzksnark_proof<alt_bn128_pp>>(
        proof, PROOF_SIZE);
    
    // 入力値からprimary_inputを構築
    std::vector<Fr<alt_bn128_pp>> primary_input;
    primary_input.push_back(Fr<alt_bn128_pp>(input[0]));
    primary_input.push_back(Fr<alt_bn128_pp>(input[1]));
    
    // 検証を実行
    return r1cs_ppzksnark_verifier_strong_IC<alt_bn128_pp>(vk, primary_input, zkproof);
}

// マイニング用ハッシュ関数の完全実装
void zksnark_hash(const uint8_t* input, uint8_t* output) {
    init_zksnark();
    
    // 一時バッファの準備
    uint8_t proving_key[PROVING_KEY_SIZE];
    uint8_t verification_key[VERIFICATION_KEY_SIZE];
    uint8_t proof[PROOF_SIZE];
    
    // 1. キーペアを生成
    zksnark_generate_keypair(proving_key, verification_key);
    
    // 2. 証明を生成
    zksnark_generate_proof(proving_key, input, proof);
    
    // 3. 検証
    if (!zksnark_verify(verification_key, proof, input)) {
        throw std::runtime_error("ZKSnark proof verification failed");
    }
    
    // 4. 証明からハッシュ値を生成
    // 簡単なハッシュ関数として、proofの最初の32バイトを使用
    memcpy(output, proof, HASH_SIZE);
}