{
    "$schema": "https://raw.githubusercontent.com/oliverw/miningcore/master/src/Miningcore/config.schema.json",
    "logging": {
        "level": "info",
        "enableConsoleLog": true,
        "enableConsoleColors": true,
        "logFile": "",
        "apiLogFile": "",
        "logBaseDirectory": "",
        "perPoolLogFile": false
    },
    "banning": {
        "manager": "Integrated",
        "banOnJunkReceive": true,
        "banOnInvalidShares": true
    },
    "persistence": {
        "postgres": {
            "host": "127.0.0.1",
            "port": 5432,
            "user": "miningcore",
            "password": "password",
            "database": "miningcore"
        }
    },
    "paymentProcessing": {
        "enabled": true,
        "interval": 100,
        "shareRecoveryFile": "recovered-shares.txt"
    },
    "api": {
        "enabled": true,
        "listenAddress": "*",
        "port": 4000
    },
    "clusterName": "pool1",
    "pools": [
        {
            "id": "pool1",
            "enabled": true,
            "coin": "yourCoin",
            "addresses": {
                "template": "{%address%}",
                "validateAddress": true
            },
            "ports": {
                "3032": {
                    "name": "Stratum Port",
                    "listenAddress": "*",
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
                        "pattern": "^[a-zA-Z0-9_-]+$",
                        "validationQuery": "SELECT EXISTS(SELECT 1 FROM pool.users WHERE username = $1)"
                    }
                }
            },
            "daemons": [
                {
                    "host": "127.0.0.1",
                    "port": 8332,
                    "user": "user",
                    "password": "password"
                }
            ]
        }
    ]
}