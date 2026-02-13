#!/usr/bin/env python3
"""
Simple GUI editor for pixelart.py grid files.
Left-click to paint with selected color, right-click to erase (transparent).

Usage: python pixelart_gui.py [directory]
  directory: folder containing grid.txt and palette.txt (default: current dir)
"""

import sys
import tkinter as tk
from pathlib import Path

GRID_SIZE = 32
CELL_SIZE = 16
TRANSPARENT = "."
CHECKER_LIGHT = "#DCDCDC"
CHECKER_DARK = "#B4B4B4"


def load_palette(path):
    palette = {TRANSPARENT: None}
    if not path.exists():
        return palette
    with open(path) as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            if "=" not in line:
                continue
            alias, color = line.split("=", 1)
            alias = alias.strip()
            color = color.strip()
            if len(alias) == 1 and alias != TRANSPARENT:
                palette[alias] = None if color.lower() == "transparent" else color
    return palette


def load_grid(path):
    rows = []
    with open(path) as f:
        for line in f:
            vals = line.strip().split()
            if len(vals) == GRID_SIZE:
                rows.append(vals)
    if len(rows) != GRID_SIZE:
        print(f"ERROR: grid has {len(rows)} rows, expected {GRID_SIZE}")
        sys.exit(1)
    return rows


def save_grid(path, rows):
    with open(path, "w", newline="\n") as f:
        for row in rows:
            f.write(" ".join(row) + "\n")


def checker_color(r, c):
    return CHECKER_LIGHT if (r // 2 + c // 2) % 2 == 0 else CHECKER_DARK


def cell_display_color(val, palette, r, c):
    if val == TRANSPARENT:
        return checker_color(r, c)
    if val in palette and palette[val] is not None:
        return palette[val]
    if val.startswith("#") and len(val) == 7:
        return val
    return "#FF00FF"  # unknown = magenta


class PixelEditor:
    def __init__(self, root, work_dir):
        self.root = root
        self.work_dir = Path(work_dir)
        self.grid_path = self.work_dir / "grid.txt"
        self.palette_path = self.work_dir / "palette.txt"

        self.palette = load_palette(self.palette_path)
        self.grid = load_grid(self.grid_path)
        self.selected = TRANSPARENT
        self.painting = False

        root.title("Pixel Art Editor")

        # Main layout
        main = tk.Frame(root)
        main.pack(fill=tk.BOTH, expand=True)

        # Palette panel
        palette_frame = tk.Frame(main, padx=5, pady=5)
        palette_frame.pack(side=tk.LEFT, fill=tk.Y)
        tk.Label(palette_frame, text="Palette", font=("Arial", 10, "bold")).pack()

        self.palette_buttons = {}
        # Transparent button
        btn = tk.Button(palette_frame, text=".", width=3, height=1, relief=tk.SUNKEN,
                        bg="#FFFFFF", command=lambda: self.select_color(TRANSPARENT))
        btn.pack(pady=2)
        self.palette_buttons[TRANSPARENT] = btn

        for alias, color in sorted(self.palette.items()):
            if alias == TRANSPARENT:
                continue
            display = color if color else "#FFFFFF"
            btn = tk.Button(palette_frame, text=alias, width=3, height=1,
                            bg=display, command=lambda a=alias: self.select_color(a))
            btn.pack(pady=2)
            self.palette_buttons[alias] = btn

        # Save button
        tk.Button(palette_frame, text="Save", width=6, command=self.save,
                  bg="#90EE90").pack(pady=10)
        # Render button
        tk.Button(palette_frame, text="Render", width=6, command=self.render,
                  bg="#ADD8E6").pack(pady=2)
        # Refresh button
        tk.Button(palette_frame, text="Refresh", width=6, command=self.refresh,
                  bg="#FFD700").pack(pady=2)

        # Canvas
        canvas_size = GRID_SIZE * CELL_SIZE
        self.canvas = tk.Canvas(main, width=canvas_size, height=canvas_size,
                                highlightthickness=0)
        self.canvas.pack(side=tk.LEFT, padx=5, pady=5)

        self.cells = [[None] * GRID_SIZE for _ in range(GRID_SIZE)]
        for r in range(GRID_SIZE):
            for c in range(GRID_SIZE):
                x0 = c * CELL_SIZE
                y0 = r * CELL_SIZE
                color = cell_display_color(self.grid[r][c], self.palette, r, c)
                rect = self.canvas.create_rectangle(x0, y0, x0 + CELL_SIZE, y0 + CELL_SIZE,
                                                     fill=color, outline="#333333", width=0.5)
                self.cells[r][c] = rect

        self.canvas.bind("<Button-1>", self.on_click)
        self.canvas.bind("<B1-Motion>", self.on_drag)
        self.canvas.bind("<Button-3>", self.on_right_click)
        self.canvas.bind("<B3-Motion>", self.on_right_drag)

        self.select_color(TRANSPARENT)

    def select_color(self, alias):
        # Update button relief
        for a, btn in self.palette_buttons.items():
            btn.config(relief=tk.SUNKEN if a == alias else tk.RAISED)
        self.selected = alias

    def cell_at(self, event):
        c = event.x // CELL_SIZE
        r = event.y // CELL_SIZE
        if 0 <= r < GRID_SIZE and 0 <= c < GRID_SIZE:
            return r, c
        return None, None

    def paint(self, r, c, value):
        if r is None:
            return
        self.grid[r][c] = value
        color = cell_display_color(value, self.palette, r, c)
        self.canvas.itemconfig(self.cells[r][c], fill=color)

    def on_click(self, event):
        r, c = self.cell_at(event)
        self.paint(r, c, self.selected)

    def on_drag(self, event):
        r, c = self.cell_at(event)
        self.paint(r, c, self.selected)

    def on_right_click(self, event):
        r, c = self.cell_at(event)
        self.paint(r, c, TRANSPARENT)

    def on_right_drag(self, event):
        r, c = self.cell_at(event)
        self.paint(r, c, TRANSPARENT)

    def save(self):
        save_grid(self.grid_path, self.grid)
        print("Saved grid.txt")

    def refresh(self):
        self.palette = load_palette(self.palette_path)
        self.grid = load_grid(self.grid_path)
        for r in range(GRID_SIZE):
            for c in range(GRID_SIZE):
                color = cell_display_color(self.grid[r][c], self.palette, r, c)
                self.canvas.itemconfig(self.cells[r][c], fill=color)
        print("Refreshed from disk")

    def render(self):
        self.save()
        import subprocess
        script = Path(__file__).parent / "pixelart.py"
        subprocess.run([sys.executable, str(script), "render"], cwd=str(self.work_dir))
        print("Rendered preview.png")


def main():
    work_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    root = tk.Tk()
    PixelEditor(root, work_dir)
    root.mainloop()


if __name__ == "__main__":
    main()
