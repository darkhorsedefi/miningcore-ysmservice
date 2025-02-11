# Miningcore ユーザー認証システム

## 概要

Miningcoreのユーザー認証システムは、以下の機能を提供します：

1. ユーザー名ベースのマイニング
2. ワーカー別のアドレス管理
3. パスワードベースのdifficulty設定

## データベース構造

PostgreSQLデータベースに以下のテーブルが作成されます：

- `pool.users`: ユーザーアカウント情報
- `pool.miner_addresses`: ユーザーごとのウォレットアドレス
- `pool.worker_auth`: ワーカー認証情報
- `pool.stratum_info`: Stratum接続情報

## 使用方法

### 1. ユーザー名でのマイニング

```
username.worker
```

- `username`: miningcore-uiで登録したユーザー名
- `worker`: 任意のワーカー名（省略時は"default"）

例：
```
john.rig1
john.rig2
```

### 2. アドレス直接指定でのマイニング

```
address.worker
```

例：
```
0x1234....abcd.rig1
```

### 3. difficulty設定

パスワード欄で以下のパラメータを指定できます：

- `d=値`: 固定difficulty値を設定
- `mx=値`: 最大difficulty値を設定

例：
```
d=5000
d=1000,mx=10000
```

## セットアップ

1. データベース移行
```bash
psql -U miningcore -d miningcore -f miningcore-ui/SQL/postgresql_migration.sql
```

2. miningcore-ui設定
config.phpのデータベース接続情報を更新：
```php
$dsn = "pgsql:host=localhost;port=5432;dbname=miningcore;options='--search_path=pool'";
```

3. miningcore設定
pool設定でカスタム認証を有効化：
```json
{
  "id": "pool1",
  "enabled": true,
  "coin": {
    "type": "ETH"
  },
  "ports": {
    "3032": {
      "difficulty": 1,
      "varDiff": {
        "minDiff": 1,
        "maxDiff": null,
        "targetTime": 15,
        "retargetTime": 90,
        "variancePercent": 30
      },
      "customUsername": {
        "enabled": true,
        "validationQuery": "SELECT EXISTS(SELECT 1 FROM pool.users WHERE username = $1)"
      }
    }
  }
}
```

## 注意事項

1. ユーザー名でマイニングする場合は、事前にminingcore-uiでアカウント登録とアドレス設定が必要です。
2. difficultyをパスワードで指定する場合、poolConfigで設定された最小/最大値の制限が適用されます。
3. ワーカー名は各ユーザーで一意である必要があります。