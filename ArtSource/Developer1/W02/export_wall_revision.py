"""Export a UV-only wall revision to a new FBX outside Assets."""
from pathlib import Path
import runpy
import sys

import bpy


def main():
    if not bpy.app.background:
        raise RuntimeError("Run in a dedicated Blender background process.")
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(args) != 1:
        raise ValueError("Supply one new FBX output path after --.")
    output = Path(args[0]).resolve()
    w01 = Path(__file__).resolve().parent.parent / "W01"
    unity_assets = w01.parents[2] / "Assets"
    if output.suffix.lower() != ".fbx" or output.exists() or output.is_relative_to(unity_assets):
        raise ValueError("Choose a new .fbx path outside Assets.")
    original = runpy.run_path(str(w01 / "build_assets.py"))
    bpy.ops.wm.open_mainfile(filepath=str(w01 / "wall-straight-4m.blend"))
    wall = bpy.data.objects[original["WALL"]]
    original["select_only"](wall)
    for loop in wall.data.uv_layers.active.data:
        loop.uv.x = 0.15 + 0.7 * loop.uv.x
    bpy.ops.export_scene.fbx(filepath=str(output), **original["EXPORT"])
    if not output.is_file():
        raise RuntimeError("Blender did not produce the revision.")
    print("W02_BLENDER_REVISION_PASS: existing source and export settings reused.")


if __name__ == "__main__":
    main()
