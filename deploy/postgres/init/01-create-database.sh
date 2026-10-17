#!/bin/sh
# 首次初始化数据目录时执行：创建业务库和三个数据库账号，见 docs/conventions/database.md §2.2
# 密码来自 deploy/.env；表、视图和授权由 EF Core 迁移负责，这里不建表
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
    -v db="$SUANPAN_DB" \
    -v migrator_password="$SUANPAN_MIGRATOR_PASSWORD" \
    -v app_password="$SUANPAN_APP_PASSWORD" \
    -v portal_password="$SUANPAN_PORTAL_PASSWORD" <<'EOSQL'
CREATE ROLE suanpan_migrator LOGIN PASSWORD :'migrator_password';
CREATE ROLE suanpan_app LOGIN PASSWORD :'app_password';
CREATE ROLE suanpan_portal LOGIN PASSWORD :'portal_password';

-- 迁移账号是库的所有者，因此拥有 public schema 的建表权限，也能创建 hangfire schema
CREATE DATABASE :"db" OWNER suanpan_migrator;

-- 只有这三个账号能连接业务库
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db" TO suanpan_app, suanpan_portal;
EOSQL
