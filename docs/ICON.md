# Volume + app icon

The icon uses the full **Volume +** wordmark on a rounded dark tile. Its palette follows the app: `#111823` for the background, light gray for **Volume**, and `#D3F585` for **+**. The artwork was created specifically for this project's rename using the built-in **imagegen** tool. It is included under the project's MIT license, alongside the original app assets.

- Source artwork: `src/SesPlus/Assets/volumeplus-icon.png`.
- Windows icon: `src/SesPlus/Assets/volumeplus.ico` (transparent 32-bit frames at 16, 24, 32, 48, 64, 128, and 256 pixels).
- The EXE and window captions use the full icon tile. The in-app header retains the original lime equalizer-bar badge alongside a cropped **Volume +** wordmark from the same PNG.

After replacing the square source PNG, run `powershell -ExecutionPolicy Bypass -File scripts/create-icon.ps1` on Windows, then build the app. The script only resizes and packages the original artwork; it does not generate a new design.

## Design specification

Use the exact wordmark **Volume +** in one horizontal line, with a capital V and lowercase remaining letters. Keep the word light gray, the plus lime green, and the tile dark blue-black. Use a bold, clean sans-serif style and preserve transparent corners outside the rounded tile. The artwork should have no extra text, border, gradient, watermark, or mockup framing.

## Generation prompt

The built-in imagegen tool edited the previous project icon with `transparent_background: true` and this prompt:

> Edit target: the supplied Ses+ app icon. Rebrand this same icon for the exact new application name 'Volume +'. Replace 'Ses+' with the exact word 'Volume' followed by a small clear space and a plus sign '+', in a single horizontal line. Spell it V-o-l-u-m-e, capital V followed by lowercase olume. Keep the rounded bold contemporary sans-serif style and robust thick strokes, make the longer wordmark occupy about 86% of the tile width, optically centered horizontally and vertically. The Volume text is pale cool light gray #F0F3F8, the plus sign is lime green #D3F585. Retain the existing dark blue-black rounded square tile in #111823 and transparent outer corners. Keep the design flat, minimal, front facing, clean edges, no border, no bevel, no shadows, no extra icons, no sound bars inside this tile, no other text, no mockup, no watermark. Preserve the same square 1280x1280 composition if possible. Place the full wordmark in the middle band, fully inside x=80..1200 and y=480..800 so it can also be displayed as a cropped wordmark in the application header. Transparent background outside the rounded tile; no opaque outer canvas.

The actual header view uses the crop `60,480,1140,320` to retain the full leading V and spacing. The PNG itself is preserved without cropping.
