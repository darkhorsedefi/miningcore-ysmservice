# Miningcore テスト手順

## 1. データベースセットアップ

### PostgreSQLデータベースの作成
```bash
createdb miningcore
psql -U miningcore -d miningcore -f miningcore-ui/SQL/postgresql_migration.sql
```

## 2. 設定変更

### miningcore-ui/api/config.php
```php
$dsn = "pgsql:host=localhost;port=5432;dbname=miningcore;options='--search_path=pool'";
$user = "miningcore";
$pass = "your_password";
```

### pool_config.json
```json
{
  "id": "pool1",
  "enabled": true,
  "coin": {
    "type": "BTC"
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
      }
    }
  }
}
```

## 3. テストケース

### 3.1 ユーザー登録とアドレス設定
1. miningcore-uiで新規ユーザー登録
2. 各コインのアドレスを設定

### 3.2 マイニング接続テスト

#### ユーザー名でのマイニング
```bash
# 基本的な接続
stratum+tcp://pool:3032 -u username.worker1

# difficulty指定
stratum+tcp://pool:3032 -u username.worker1 -p "d=1000"

# difficulty上限指定
stratum+tcp://pool:3032 -u username.worker1 -p "d=1000,mx=5000"
```

#### アドレス直接指定
```bash
stratum+tcp://pool:3032 -u address.worker1
```

### 3.3 特殊ケースのテスト

1. 無効なユーザー名
```bash
stratum+tcp://pool:3032 -u invalid_user.worker1
```
期待値: 接続拒否

2. 無効なアドレス
```bash
stratum+tcp://pool:3032 -u invalid_address.worker1
```
期待値: 接続拒否

3. 最大difficulty超過
```bash
stratum+tcp://pool:3032 -u username.worker1 -p "d=10000,mx=5000"
```
期待値: difficulty=5000で接続成功

## 4. 確認項目

### 4.1 基本機能
- [ ] ユーザー登録が正常に機能するか
- [ ] アドレス設定が保存されるか
- [ ] ユーザー名での接続が可能か
- [ ] アドレス直接指定での接続が可能か

### 4.2 Difficulty設定
- [ ] パスワードでのdifficulty指定が機能するか
- [ ] 最大difficulty制限が機能するか
- [ ] VarDiffとの連携が正常か

### 4.3 エラー処理
- [ ] 無効なユーザー名を適切に処理できるか
- [ ] 無効なアドレスを適切に処理できるか
- [ ] バンニング機能が正常に動作するか

## 5. パフォーマンステスト

1. 同時接続テスト
```bash
# 複数ワーカーでの同時接続
for i in {1..10}; do
    stratum+tcp://pool:3032 -u username.worker$i &
done
```

2. 長時間稼働テスト
- 24時間以上の連続稼働で問題ないか確認
- メモリリークや性能劣化がないか監視

## 6. トラブルシューティング

### ログの確認
```bash
tail -f logs/pool.log
```

### データベース接続の確認
```sql
-- ユーザー認証の確認
SELECT * FROM pool.worker_auth WHERE username = 'test_user';

-- difficulty設定の確認
SELECT * FROM pool.shares ORDER BY created DESC LIMIT 10;