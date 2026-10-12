#!/usr/bin/env bash
# Builds both distributables into dist/:
#   straightwire-<version>-rh8_0-any.yak   for Rhino's Package Manager
#   StraightWire-<version>.zip             for manual install
set -euo pipefail
cd "$(dirname "$0")"

VERSION="${VERSION:-0.3.0}"
YAK="${YAK:-/Applications/Rhino 8.app/Contents/Resources/bin/yak}"

echo "==> test"
dotnet test tests/StraightWire.Core.Tests -c Release --nologo -v q

echo "==> build"
rm -rf src/StraightWire/bin src/StraightWire/obj
dotnet build src/StraightWire -c Release --nologo -v q -p:Version="$VERSION"

rm -rf dist && mkdir -p dist/payload dist/zip
cp src/StraightWire/bin/Release/StraightWire.gha dist/payload/
cp src/StraightWire/bin/Release/StraightWire.gha dist/zip/
cp INSTALL.txt LICENSE dist/zip/
cp LICENSE dist/payload/

cat > dist/payload/manifest.yml <<YML
---
name: StraightWire
version: $VERSION
authors:
  - Yolanda Xing
url: https://github.com/YolandaXing210/straight-wire
description: >
  Draws Grasshopper wires as straight, circuit-board style traces. Wires run at right
  angles or at 45 degrees, each in its own lane and around components, and wires from
  the same output share a trunk. Switch styles for all wires or just the selected ones
  from the Straight Wire menu or the switch in the canvas corner. Styles are saved in
  the .gh file, so anyone with Straight Wire sees the same wires. Wires can be reshaped by
  hand, and Tidy lays a definition out in columns along the data flow with straight wires.
keywords:
  - grasshopper
  - canvas
  - wire
  - layout
  - tidy
YML

echo "==> yak"
( cd dist/payload && "$YAK" build --platform any >/dev/null && mv ./*.yak .. )

echo "==> zip"
( cd dist/zip && zip -q -r "../StraightWire-$VERSION.zip" . )

rm -rf dist/payload dist/zip
ls -la dist
