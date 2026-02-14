"""Generate GitHub social preview image (1280x640).

One-time script. Run from the Veil of Ages repo root:
    python scripts/make_social_preview.py

Output: assets/custom/social-preview.png
Upload manually via GitHub repo Settings > Social preview.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO_ROOT = Path(__file__).resolve().parent.parent
LOGO_PATH = REPO_ROOT / "assets" / "custom" / "logo" / "output_8x.png"
OUTPUT_PATH = REPO_ROOT / "assets" / "custom" / "social-preview.png"

BG_COLOR = (26, 10, 42)  # #1A0A2A — dark purple from logo gradient
TEXT_COLOR = (240, 200, 72)  # gold, matching hourglass
TAGLINE_COLOR = (0, 204, 68)  # green, matching magic energy

WIDTH, HEIGHT = 1280, 640


def main() -> None:
    img = Image.new("RGB", (WIDTH, HEIGHT), BG_COLOR)
    draw = ImageDraw.Draw(img)

    # Place logo centered, upper portion
    logo = Image.open(LOGO_PATH).convert("RGBA")
    logo = logo.resize((256, 256), Image.NEAREST)
    logo_x = (WIDTH - 256) // 2
    logo_y = 100
    img.paste(logo, (logo_x, logo_y), logo)

    # Title text — gothic blackletter for title, elegant serif for tagline
    try:
        title_font = ImageFont.truetype("OLDENGL.TTF", 72)
    except OSError:
        try:
            title_font = ImageFont.truetype("arial.ttf", 64)
        except OSError:
            title_font = ImageFont.load_default()
    try:
        tagline_font = ImageFont.truetype("pala.ttf", 32)
    except OSError:
        try:
            tagline_font = ImageFont.truetype("arial.ttf", 28)
        except OSError:
            tagline_font = ImageFont.load_default()

    title = "Veil of Ages"
    bbox = draw.textbbox((0, 0), title, font=title_font)
    tw = bbox[2] - bbox[0]
    draw.text(((WIDTH - tw) // 2, 390), title, fill=TEXT_COLOR, font=title_font)

    tagline = "Whispers of Kalixoria"
    bbox = draw.textbbox((0, 0), tagline, font=tagline_font)
    tw = bbox[2] - bbox[0]
    draw.text(((WIDTH - tw) // 2, 475), tagline, fill=TAGLINE_COLOR, font=tagline_font)

    img.save(str(OUTPUT_PATH))
    print(f"Saved {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
