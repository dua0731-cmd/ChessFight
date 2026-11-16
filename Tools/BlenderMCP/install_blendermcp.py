# Run with: blender -b --factory-startup --python install_blendermcp.py -- <path to addon.py>
# Installs the BlenderMCP add-on into the user's add-ons folder, enables it and saves preferences.
import sys
import bpy
import addon_utils

src = sys.argv[sys.argv.index("--") + 1]
bpy.ops.preferences.addon_install(filepath=src, overwrite=True)
addon_utils.enable("addon", default_set=True, persistent=True)
bpy.context.preferences.use_preferences_save = True
bpy.ops.wm.save_userpref()
loaded = [m.__name__ for m in addon_utils.modules() if addon_utils.check(m.__name__)[1]]
print("BLENDERMCP_ENABLED", "addon" in loaded, bpy.app.version_string)
