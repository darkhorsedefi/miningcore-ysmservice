# Notification System Documentation

## Overview
Miningcoreは以下の通知機能をサポートしています：

- ブロック発見通知
- 支払い通知
- ハッシュレート低下通知

各通知は以下の方法で配信されます：

1. 管理者メール通知
2. データベース保存（Web UI表示用）
3. メッセージバス（外部システム連携用）

## Configuration

### グローバル設定

```json
{
    "notifications": {
        "enabled": true,
        "email": {
            "host": "smtp.gmail.com",
            "port": 587,
            "user": "your-email@gmail.com",
            "password": "your-app-password",
            "fromAddress": "your-email@gmail.com",
            "fromName": "Miningcore Pool",
            "enableSsl": true
        },
        "admin": {
            "emailAddress": "admin@yourdomain.com",
            "notifyBlockFound": true,
            "notifyPaymentAbove": 1.0,
            "notifyHashrateDropThreshold": 50.0
        }
    }
}
```

### プール別設定

```json
{
    "pools": [{
        "id": "eth1",
        "notifications": {
            "enabled": true,
            "minimumPaymentAmount": 0.1,
            "hashrateDropThreshold": 25
        }
    }]
}
```

## 通知設定の説明

### Email設定
- `host`: SMTPサーバーのホスト名
- `port`: SMTPポート番号
- `user`: SMTPユーザー名
- `password`: SMTPパスワード
- `fromAddress`: 送信元メールアドレス
- `fromName`: 送信者名
- `enableSsl`: SSL/TLS使用フラグ

### 管理者通知設定
- `emailAddress`: 管理者メールアドレス
- `notifyBlockFound`: ブロック発見時の通知有効化
- `notifyPaymentAbove`: 指定額以上の支払い時に通知
- `notifyHashrateDropThreshold`: ハッシュレート低下閾値（%）

### プール別通知設定
- `enabled`: プール別通知の有効化
- `minimumPaymentAmount`: 最小支払い通知額
- `hashrateDropThreshold`: ハッシュレート低下通知閾値（%）

## データベーステーブル

### notifications
- 全ての通知を保存
- Web UIでの表示に使用

### notification_settings
- ユーザーごとの通知設定を保存
- メールやDiscord通知の設定を管理

### notification_history
- 送信された通知の履歴を保存
- エラー追跡とデバッグに使用

## メンテナンス

### 古い通知の削除
```sql
SELECT cleanup_old_notifications(30); -- 30日以上前の通知を削除
```

### 通知統計の確認
```sql
SELECT * FROM pool.v_notification_stats;
SELECT * FROM pool.v_notification_settings_summary;
```

## トラブルシューティング

### メール通知が届かない場合
1. SMTP設定の確認
2. ファイアウォール設定の確認
3. ログファイルの確認

### 通知が保存されない場合
1. データベース接続の確認
2. テーブルのアクセス権限確認
3. ディスク容量の確認

## セキュリティ考慮事項

1. SMTP認証情報の適切な管理
2. SSL/TLS通信の有効化
3. 通知データベースへのアクセス制限
4. センシティブな情報のログ出力制限