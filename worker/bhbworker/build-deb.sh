#!/bin/bash
set -e

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

# Clean previous build
rm -rf "$WORK_DIR"
mkdir -p "$BUILD_DIR"
mkdir -p "$PUBLISH_DIR"

# Copy source to temp directory to avoid existing obj conflicts
echo "[1/6] Preparing clean build environment..."
cp "$SCRIPT_DIR"/*.cs "$WORK_DIR/"
cp "$SCRIPT_DIR"/*.csproj "$WORK_DIR/"
cp "$SCRIPT_DIR/appsettings.json" "$WORK_DIR/"
mkdir -p "$WORK_DIR/debian"
cp -r "$SCRIPT_DIR/debian/"* "$WORK_DIR/debian/"

cd "$WORK_DIR"

# Publish the .NET application
echo "[2/6] Publishing .NET application..."
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o "$PUBLISH_DIR"

# Create package directory structure
echo "[3/6] Creating package structure..."
mkdir -p "$BUILD_DIR/DEBIAN"
mkdir -p "$BUILD_DIR/opt/bhb-worker"
mkdir -p "$BUILD_DIR/etc/systemd/system"

# Copy application files
echo "[4/6] Copying application files..."
cp "$PUBLISH_DIR/bhb-worker" "$BUILD_DIR/opt/bhb-worker/"
cp "$WORK_DIR/appsettings.json" "$BUILD_DIR/opt/bhb-worker/appsettings.json.default"

# Copy debian control files
echo "[5/6] Copying debian control files..."
cp "$WORK_DIR/debian/control" "$BUILD_DIR/DEBIAN/"
cp "$WORK_DIR/debian/postinst" "$BUILD_DIR/DEBIAN/"
cp "$WORK_DIR/debian/prerm" "$BUILD_DIR/DEBIAN/"
cp "$WORK_DIR/debian/postrm" "$BUILD_DIR/DEBIAN/"

# Copy systemd service file
cp "$WORK_DIR/debian/bhb-worker.service" "$BUILD_DIR/etc/systemd/system/"

# Set permissions
chmod 755 "$BUILD_DIR/DEBIAN/postinst"
chmod 755 "$BUILD_DIR/DEBIAN/prerm"
chmod 755 "$BUILD_DIR/DEBIAN/postrm"
chmod 755 "$BUILD_DIR/opt/bhb-worker/bhb-worker"

# Build the package
echo "[6/6] Building .deb package..."
DEB_FILE="${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
dpkg-deb --build "$BUILD_DIR" "$SCRIPT_DIR/$DEB_FILE"

# Cleanup build directory
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
