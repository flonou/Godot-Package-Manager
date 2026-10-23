@tool
extends RefCounted


const PROJECT_WINDOW_SIZE_KEY := "addons/godot_package_manager/ui/window_size"

var ui_window: Window
var package_tree: Tree
var core: PackageManagerCore

func _init() -> void:
	core = PackageManagerCore.GetInstance()

func open_ui(base_control: Control) -> void:
	if ui_window and is_instance_valid(ui_window):
		ui_window.popup_centered()
		return

	ui_window = Window.new()
	ui_window.title = "Godot Package Manager"
	ui_window.initial_position = Window.WINDOW_INITIAL_POSITION_CENTER_MAIN_WINDOW_SCREEN
	ui_window.min_size = Vector2i(760, 420)

	ui_window.size = ProjectSettings.get_setting(PROJECT_WINDOW_SIZE_KEY, Vector2i(760,420))
	ui_window.close_requested.connect(_on_window_close_requested)
	ui_window.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	ui_window.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_EXPAND
	base_control.add_child(ui_window)

	var package_ui_scene := preload("res://addons/godot_package_manager/ui/package_manager_ui.tscn")
	var package_ui := package_ui_scene.instantiate()
	package_ui.name = "PackageManagerUI"
	package_ui.set_anchors_preset(Control.PRESET_FULL_RECT)
	package_ui.offset_left = 0
	package_ui.offset_top = 0
	package_ui.offset_right = 0
	package_ui.offset_bottom = 0
	package_ui.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	package_ui.size_flags_vertical = Control.SIZE_EXPAND_FILL
	ui_window.add_child(package_ui)

	ui_window.popup_centered()

func _on_window_close_requested() -> void:
	if ui_window:
		ui_window.hide()
        
		ProjectSettings.set_setting(PROJECT_WINDOW_SIZE_KEY, ui_window.size)
		ProjectSettings.save()


