@tool
class_name GodotPackageManager
extends EditorPlugin

const PROJECT_NPM_REGISTRIES_KEY := "addons/godot_package_manager/npm_registries"
const PROJECT_SOURCES_KEY := "addons/godot_package_manager/sources"

var package_manager: RefCounted
var registry_inspector_plugin: EditorInspectorPlugin

func _enter_tree() -> void:
    package_manager = preload("res://addons/godot_package_manager/ui/package_manager.gd").new()
    registry_inspector_plugin = preload("res://addons/godot_package_manager/ui/npm_registry_inspector_plugin.gd").new()
    add_inspector_plugin(registry_inspector_plugin)
    add_tool_menu_item("Package Manager", _on_package_manager_pressed)

func _exit_tree() -> void:
    remove_tool_menu_item("Package Manager")
    if registry_inspector_plugin != null:
        remove_inspector_plugin(registry_inspector_plugin)
        registry_inspector_plugin = null


func _on_package_manager_pressed() -> void:
    if package_manager:
        package_manager.open_ui(get_editor_interface().get_base_control())
