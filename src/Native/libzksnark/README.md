# libzksnark for Miningcore

このライブラリは、miningcoreにzkSNARK（Zero-Knowledge Succinct Non-Interactive Argument of Knowledge）機能を提供します。

## 必要条件

### Linux

必要なパッケージをインストール：

```bash
sudo apt-get update
sudo apt-get install -y \
    build-essential \
    cmake \
    git \
    libgmp-dev \
    libboost-all-dev \
    libssl-dev \
    pkg-config
```

libsnarkのインストール：

```bash
cd /tmp
git clone https://github.com/scipr-lab/libsnark.git
cd libsnark
git submodule init && git submodule update
mkdir build && cd build
cmake -DCMAKE_INSTALL_PREFIX=/usr/local ..
make -j$(nproc)
sudo make install
```

### Windows

1. 必要なツール
   - Visual Studio 2022以降
   - CMake 3.20以降
   - Git for Windows

2. 依存ライブラリのインストール

   a. GMP (GNU Multiple Precision Arithmetic Library)
   ```bat
   vcpkg install gmp:x64-windows
   ```

   b. Boost
   ```bat
   vcpkg install boost:x64-windows
   ```

   c. libsnark
   ```bat
   git clone https://github.com/scipr-lab/libsnark.git
   cd libsnark
   mkdir build
   cd build
   cmake -G "Visual Studio 17 2022" -A x64 -DCMAKE_TOOLCHAIN_FILE=[vcpkg root]/scripts/buildsystems/vcpkg.cmake ..
   cmake --build . --config Release
   cmake --install . --prefix C:/Dev/libsnark
   ```

3. 環境変数の設定
   ```bat
   set LIBSNARK_ROOT=C:\Dev\libsnark
   set GMP_ROOT=[vcpkg root]\installed\x64-windows
   set BOOST_ROOT=[vcpkg root]\installed\x64-windows
   ```

## ビルド

### Linux

```bash
cd src/Miningcore
./build-libs-linux.sh
```

### Windows

```bat
build-windows.bat
```

## 使用方法

このライブラリは、miningcoreのハッシュアルゴリズムとして実装されています。

```json
{
  "coin": {
    "hash": "zksnark"
  }
}
```

## 技術詳細

- 実装はlibsnarkのR1CS ppzkSNARKを使用
- キーペア生成、証明生成、検証機能を提供
- マルチスレッド対応のキーキャッシング機能
- ネイティブコードはC++17で実装
- C#インターフェースはP/Invokeを使用

## ライセンス

MITライセンス