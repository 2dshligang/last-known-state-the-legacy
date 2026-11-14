"""Generate the editable W03 sources and Unity FBX files in Blender background mode."""
import hashlib
import json
from pathlib import Path
import runpy

import bpy

SOURCE = Path(__file__).resolve().parent
ART = SOURCE.parents[2] / "Assets/_Game/Art/W03/Models"
PALETTE = {
    "Shell": (.51, .49, .43, 1),
    "Metal": (.22, .25, .25, 1),
    "Ochre": (.66, .31, .09, 1),
    "Screen": (.052, .090, .10, 1),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    if not bpy.app.background:
        raise RuntimeError("Run in a dedicated Blender background process.")
    helpers = runpy.run_path(str(SOURCE.parent / "W01/build_assets.py"))
    spec = SOURCE / "mesh-spec.json"
    assets = json.loads(spec.read_text(encoding="utf-8"))["assets"]
    manifest = SOURCE / "mesh-hashes.json"
    outputs = [(SOURCE / (asset["name"] + ".blend"), ART / (asset["name"] + ".fbx"))
               for asset in assets]
    if manifest.exists():
        recorded = json.loads(manifest.read_text(encoding="utf-8"))
        files = [spec, Path(__file__).resolve()] + [p for pair in outputs for p in pair]
        actual = {p.relative_to(SOURCE.parents[2]).as_posix(): sha(p) for p in files}
        if actual != recorded["files"]:
            raise RuntimeError("An input or output differs from the approved mesh manifest.")
        print("W03_MESH_VERIFY_PASS")
        return
    if any(path.exists() for pair in outputs for path in pair):
        raise RuntimeError("Refusing to overwrite an existing Blend source or FBX export.")
    ART.mkdir(parents=True, exist_ok=True)
    for asset, (blend_path, fbx_path) in zip(assets, outputs):
        helpers["reset_scene"]()
        bpy.context.preferences.filepaths.save_version = 0
        materials = {}
        for surface, color in PALETTE.items():
            material = bpy.data.materials.new(surface)
            material.diffuse_color = color
            materials[surface] = material

        for part in asset["parts"]:
            x, y, z = part["center"]
            width, height, depth = part["size"]
            # W01 export maps Blender (x, y, z) to Unity (-x, z, -y).
            geometry = helpers["box"](width, depth, height, -x-width/2, -z-depth/2, y-height/2)
            obj = helpers["mesh_object"](part["name"], geometry)
            obj.data.materials.append(materials[part["surface"]])
            obj.select_set(True)
            assert len(obj.data.uv_layers.active.data) == 24
            bpy.context.view_layer.objects.active = obj
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
        bpy.ops.export_scene.fbx(filepath=str(fbx_path), **helpers["EXPORT"])

    files = [spec, Path(__file__).resolve()] + [p for pair in outputs for p in pair]
    data = {
        "blender": bpy.app.version_string,
        "buildHash": bpy.app.build_hash.decode(),
        "exportSettings": {key: sorted(value) if isinstance(value, set) else value
                           for key, value in helpers["EXPORT"].items()},
        "files": {p.relative_to(SOURCE.parents[2]).as_posix(): sha(p) for p in files},
    }
    manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("W03_MESH_GENERATE_PASS")


if __name__ == "__main__":
    main()
