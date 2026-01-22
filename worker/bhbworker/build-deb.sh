#!/bin/bash
set -euo pipefail

# ---------- Check for root when needed ----------
NEED_ROOT=false
if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  echo "Note: Running as non-root. Package will be built but ownership may need manual fixing."
  NEED_ROOT=true
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

VERSION="1.0.0"
PACKAGE_NAME="bhb-worker"
ARCH="amd64"
WORK_DIR="/tmp/bhb-worker-build-$$"
BUILD_DIR="$WORK_DIR/deb-build"
PUBLISH_DIR="$WORK_DIR/publish"

echo "=========================================="
echo " Building BHB Worker .deb package"
echo "=========================================="

rm -rf "$WORK_DIR"
mkdir -p "$BUILD_DIR" "$PUBLISH_DIR"

echo "[1/6] Preparing clean build environment..."
# Copy needed files only (avoid obj/bin)
cp -f "$SCRIPT_DIR"/*.cs "$WORK_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR"/*.csproj "$WORK_DIR/" 2>/dev/null || true
cp -f "$SCRIPT_DIR/appsettings.json" "$WORK_DIR/" 2>/dev/null || true

mkdir -p "$WORK_DIR/debian"
cp -r "$SCRIPT_DIR/debian/"* "$WORK_DIR/debian/"

cd "$WORK_DIR"

echo "[2/6] Publishing .NET application..."
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$PUBLISH_DIR"

echo "[3/6] Creating package structure..."
mkdir -p "$BUILD_DIR/DEBIAN"
mkdir -p "$BUILD_DIR/opt/bhb-worker"
mkdir -p "$BUILD_DIR/etc/systemd/system"

echo "[4/6] Copying application files..."
cp -f "$PUBLISH_DIR/bhb-worker" "$BUILD_DIR/opt/bhb-worker/bhb-worker"
cp -f "$WORK_DIR/appsettings.json" "$BUILD_DIR/opt/bhb-worker/appsettings.json.default"

# Copy sudoers file for automatic sudo configuration
if [[ -f "$WORK_DIR/debian/bhb-worker.sudoers" ]]; then
  cp -f "$WORK_DIR/debian/bhb-worker.sudoers" "$BUILD_DIR/opt/bhb-worker/bhb-worker.sudoers"
  chmod 644 "$BUILD_DIR/opt/bhb-worker/bhb-worker.sudoers"
fi

echo "[5/6] Copying debian control files..."
cp -f "$WORK_DIR/debian/control" "$BUILD_DIR/DEBIAN/"
cp -f "$WORK_DIR/debian/postinst" "$BUILD_DIR/DEBIAN/"
cp -f "$WORK_DIR/debian/prerm" "$BUILD_DIR/DEBIAN/"
cp -f "$WORK_DIR/debian/postrm" "$BUILD_DIR/DEBIAN/"

# Copy systemd service file (already configured to run as root)
if [[ -f "$WORK_DIR/debian/bhb-worker.service" ]]; then
  cp -f "$WORK_DIR/debian/bhb-worker.service" "$BUILD_DIR/etc/systemd/system/"
else
  echo "ERROR: debian/bhb-worker.service not found"
  exit 1
fi

echo "Setting permissions..."
chmod 755 "$BUILD_DIR/DEBIAN/postinst" "$BUILD_DIR/DEBIAN/prerm" "$BUILD_DIR/DEBIAN/postrm"
chmod 755 "$BUILD_DIR/opt/bhb-worker/bhb-worker"
chmod 644 "$BUILD_DIR/opt/bhb-worker/appsettings.json.default"
chmod 644 "$BUILD_DIR/etc/systemd/system/bhb-worker.service"

# IMPORTANT: Make sure all files are owned by root (if running as root)
if [[ "$NEED_ROOT" == "false" ]]; then
  chown -R root:root "$BUILD_DIR"
fi

echo "[6/6] Building .deb package..."
DEB_FILE="${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
dpkg-deb --build "$BUILD_DIR" "$SCRIPT_DIR/$DEB_FILE"

rm -rf "$WORK_DIR"

echo ""
echo "=========================================="
echo " Build complete!"
echo "=========================================="
echo " Package: $DEB_FILE"
echo ""
echo " Install with:"
echo "   sudo dpkg -i $DEB_FILE"
echo ""
echo " Or with dependencies:"
echo "   sudo apt install ./$DEB_FILE"
echo "=========================================="
