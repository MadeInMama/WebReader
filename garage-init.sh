#!/bin/sh
set -e

echo "=== Garage S3 Initialization ==="
echo "Using S3_ACCESS_KEY from environment: ${S3_ACCESS_KEY:0:10}..."

# Create temporary config for Garage CLI
cat > /etc/garage.toml <<EOF
db_engine = "sqlite"
metadata_dir = "/var/lib/garage/meta"
data_dir = "/var/lib/garage/data"
[rpc]
rpc_secret = "${GARAGE_RPC_SECRET}"
rpc_bind_addr = "0.0.0.0:${GARAGE_PORT_3_2}"
EOF

# Create bucket if it doesn't exist
echo "Creating bucket: ${S3_BUCKET}"
garage bucket create ${S3_BUCKET} || echo "Bucket already exists"

# Set bucket permissions using pre-defined credentials
echo "Setting bucket permissions for read/write access"
garage bucket allow ${S3_BUCKET} --read --write --key ${S3_ACCESS_KEY} || echo "Permissions already set"

echo "=== Garage S3 initialization completed successfully ==="
