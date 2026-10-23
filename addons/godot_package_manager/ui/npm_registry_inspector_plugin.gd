@tool
extends EditorInspectorPlugin

func _can_handle(object: Object) -> bool:
    return true

func _parse_property(object: Object, type: Variant.Type, name: String, hint: int, hint_text: String, usage_flags: int, wide: bool) -> bool:
    if name != GodotPackageManager.PROJECT_NPM_REGISTRIES_KEY.get_file() or type != TYPE_ARRAY:
        return false

    var editor := preload("res://addons/godot_package_manager/ui/npm_registry_property_editor.gd").new()
    add_property_editor(name, editor)
    return true

