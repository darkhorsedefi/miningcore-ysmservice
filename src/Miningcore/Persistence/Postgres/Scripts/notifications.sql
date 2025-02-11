CREATE SCHEMA IF NOT EXISTS pool;

-- 通知テーブル
CREATE TABLE IF NOT EXISTS notifications
(
    id SERIAL PRIMARY KEY,
    type VARCHAR(20) NOT NULL,
    data JSONB NOT NULL,
    created TIMESTAMP NOT NULL,
    processed BOOLEAN NOT NULL DEFAULT FALSE,
    processed_at TIMESTAMP
);

-- 通知設定テーブル
CREATE TABLE IF NOT EXISTS notification_settings
(
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    pool_id VARCHAR(50) NOT NULL,
    email_enabled BOOLEAN DEFAULT FALSE,
    discord_enabled BOOLEAN DEFAULT FALSE,
    webhook_url TEXT,
    notify_block_found BOOLEAN DEFAULT TRUE,
    notify_payment BOOLEAN DEFAULT TRUE,
    notify_hashrate_drop BOOLEAN DEFAULT TRUE,
    minimum_payment DECIMAL(28, 12) DEFAULT 0.1,
    hashrate_drop_threshold INTEGER DEFAULT 25,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, pool_id)
);

-- 通知履歴テーブル
CREATE TABLE IF NOT EXISTS notification_history
(
    id SERIAL PRIMARY KEY,
    notification_id INT REFERENCES notifications(id),
    user_id INT NOT NULL,
    pool_id VARCHAR(50) NOT NULL,
    type VARCHAR(20) NOT NULL,
    sent_via VARCHAR(20) NOT NULL,
    status VARCHAR(20) NOT NULL,
    error_message TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- インデックス
CREATE INDEX IF NOT EXISTS idx_notifications_type ON notifications(type);
CREATE INDEX IF NOT EXISTS idx_notifications_created ON notifications(created);
CREATE INDEX IF NOT EXISTS idx_notifications_processed ON notifications(processed);
CREATE INDEX IF NOT EXISTS idx_notification_settings_user ON notification_settings(user_id);
CREATE INDEX IF NOT EXISTS idx_notification_settings_pool ON notification_settings(pool_id);
CREATE INDEX IF NOT EXISTS idx_notification_history_user ON notification_history(user_id);
CREATE INDEX IF NOT EXISTS idx_notification_history_pool ON notification_history(pool_id);
CREATE INDEX IF NOT EXISTS idx_notification_history_created ON notification_history(created_at);

-- updated_at更新用のトリガー
CREATE OR REPLACE FUNCTION update_notification_settings_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_update_notification_settings_updated_at
    BEFORE UPDATE ON notification_settings
    FOR EACH ROW
    EXECUTE FUNCTION update_notification_settings_updated_at();

-- 古い通知のクリーンアップ関数
CREATE OR REPLACE FUNCTION cleanup_old_notifications(days INTEGER)
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    -- 古い履歴を削除
    DELETE FROM notification_history
    WHERE created_at < NOW() - (days || ' days')::INTERVAL;
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    
    -- 処理済みの古い通知を削除
    DELETE FROM notifications
    WHERE processed = TRUE
    AND created < NOW() - (days || ' days')::INTERVAL;
    GET DIAGNOSTICS deleted_count = deleted_count + ROW_COUNT;
    
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- メンテナンス用のビュー
CREATE OR REPLACE VIEW v_notification_stats AS
SELECT 
    n.type,
    COUNT(*) as total_count,
    COUNT(*) FILTER (WHERE n.processed) as processed_count,
    COUNT(*) FILTER (WHERE NOT n.processed) as pending_count,
    MIN(n.created) as oldest_notification,
    MAX(n.created) as newest_notification
FROM notifications n
GROUP BY n.type;

CREATE OR REPLACE VIEW v_notification_settings_summary AS
SELECT 
    ns.pool_id,
    COUNT(*) as total_users,
    COUNT(*) FILTER (WHERE ns.email_enabled) as email_enabled_count,
    COUNT(*) FILTER (WHERE ns.discord_enabled) as discord_enabled_count,
    COUNT(*) FILTER (WHERE ns.notify_block_found) as block_notify_count,
    COUNT(*) FILTER (WHERE ns.notify_payment) as payment_notify_count,
    COUNT(*) FILTER (WHERE ns.notify_hashrate_drop) as hashrate_notify_count
FROM notification_settings ns
GROUP BY ns.pool_id;