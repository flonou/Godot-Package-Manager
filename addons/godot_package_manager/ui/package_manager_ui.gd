@tool
extends Control

class_name PackageManagerUI

# Strict rule for this file: anything coming from C# calls, ProjectSettings, or loose dictionary access
# must be explicitly typed. Never use "var x := ..." when the value is dynamic or not statically known.

const PROJECT_SPLIT_RATIO_KEY := "addons/godot_package_manager/ui/split_ratio"
const PROJECT_SOURCES_SPLIT_RATIO_KEY := "addons/godot_package_manager/ui/sources_split_ratio"

var core: PackageManagerCore
var main_split: HSplitContainer
var sources_split: VSplitContainer
var sources_tree: Tree
var npm_registry_tree: Tree
var package_tree: Tree
var versions_tree: Tree
var dependencies_tree: Tree
var description: RichTextLabel
var npm_registries_status: Label
var sources_status: Label
var package_status: Label
var selected_package_title: Label
var selected_package_name: Label
var selected_package_version: Label
var selected_package_status: Label
var selected_version: Label
var install_button: Button
var remove_button: Button
var edit_npm_registry_button: Button
var refresh_button: Button
var add_source_button: Button
var edit_source_button: Button
var remove_source_button: Button
var source_dialog: Window
var source_type_menu: PopupMenu
var source_name_input: LineEdit
var source_url_input: LineEdit
var source_scopes_input: LineEdit
var source_validation_label: Label
var source_edit_index: int = -1
var source_is_editing: bool = false
var current_selected_source_type: String = "git"
var current_selected_npm_registry: NpmRegistryEntry = null
var current_selected_version: PackageVersionEntry = null
var current_selected_package: PackageSummary = null
var available_packages: Array

func _init() -> void:
	core = PackageManagerCore.GetInstance()

func _ready() -> void:
	_bind_scene_nodes()
	_restore_split_ratio()
	_full_refresh()
	visibility_changed.connect(_on_visibility_changed)

func _bind_scene_nodes() -> void:
	name = "PackageManagerUI"
	custom_minimum_size = Vector2(980, 540)

	main_split = get_node("RootMargin/MainSplit") as HSplitContainer
	sources_split = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit") as VSplitContainer
	edit_npm_registry_button = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/NpmRegistryContainer/NpmRegistryToolbar/EditButton") as Button
	refresh_button = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/NpmRegistryContainer/NpmRegistryToolbar/RefreshButton") as Button

	add_source_button = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/OtherSourcesContainer/SourceToolbar/AddButton") as Button
	edit_source_button = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/OtherSourcesContainer/SourceToolbar/EditButton") as Button
	remove_source_button = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/OtherSourcesContainer/SourceToolbar/RemoveButton") as Button
	
	npm_registry_tree = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/NpmRegistryContainer/NpmRegistryTree") as Tree
	npm_registries_status = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/NpmRegistryContainer/RegistriesStatus") as Label
	sources_tree = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/OtherSourcesContainer/SourcesTree") as Tree
	sources_status = get_node("RootMargin/MainSplit/SourcesPanel/SourcesSplit/OtherSourcesContainer/SourcesStatus") as Label

	package_tree = get_node("RootMargin/MainSplit/PackagesPanel/PackageTree") as Tree
	package_status = get_node("RootMargin/MainSplit/PackagesPanel/PackageStatus") as Label
	selected_package_title = get_node("RootMargin/MainSplit/DetailsPanel/TitlePanel/PackageTitle") as Label
	selected_package_name = get_node("RootMargin/MainSplit/DetailsPanel/NamePanel/PackageName") as Label
	selected_package_version = get_node("RootMargin/MainSplit/DetailsPanel/VersionPanel/PackageVersion") as Label
	selected_package_status = get_node("RootMargin/MainSplit/DetailsPanel/StatusPanel/PackageStatus") as Label
	versions_tree = get_node("RootMargin/MainSplit/DetailsPanel/PackageTabs/Versions") as Tree
	dependencies_tree = get_node("RootMargin/MainSplit/DetailsPanel/PackageTabs/Dependencies") as Tree
	description = get_node("RootMargin/MainSplit/DetailsPanel/PackageTabs/Description") as RichTextLabel
	selected_version = get_node("RootMargin/MainSplit/DetailsPanel/ActionToolbar/Version/VersionDisplayPanel/Version") as Label
	install_button = get_node("RootMargin/MainSplit/DetailsPanel/ActionToolbar/Version/Buttons/InstallButton") as Button
	remove_button = get_node("RootMargin/MainSplit/DetailsPanel/ActionToolbar/Version/Buttons/RemoveButton") as Button

	_apply_tree_headers()

	edit_npm_registry_button.pressed.connect(_on_edit_npm_registries_pressed)
	refresh_button.pressed.connect(_on_refresh_pressed)
	add_source_button.pressed.connect(_on_add_source_pressed)
	edit_source_button.pressed.connect(_on_edit_selected_source)
	remove_source_button.pressed.connect(_on_remove_selected_source)
	npm_registry_tree.item_selected.connect(_on_npm_registry_selected)
	package_tree.item_selected.connect(_on_package_selected)
	versions_tree.item_selected.connect(_on_version_selected)
	install_button.pressed.connect(_on_install_selected_version)
	remove_button.pressed.connect(_on_remove_current_package)
	main_split.dragged.connect(_on_split_dragged)
	sources_split.dragged.connect(_on_sources_split_dragged)
	install_button.disabled  = true
	remove_button.disabled  = true

func _apply_tree_headers() -> void:
	_set_tree_headers(sources_tree, ["Name", "Type", "URL"], [180, 120, 260])
	_set_tree_headers(npm_registry_tree, ["Name", "URL", "Scopes"], [180, 260, 180])
	_set_tree_headers(package_tree, ["Package", "Version", "Status"], [180, 120, 150])
	_set_tree_headers(versions_tree, ["Version", "Godot", "Action"], [130, 120, 120])
	_set_tree_headers(dependencies_tree, ["Package", "Version", "Status"], [180, 120, 150])

func _set_tree_headers(tree: Tree, labels: Array, widths: Array) -> void:
	if tree == null:
		return

	tree.columns = labels.size()
	tree.set_column_titles_visible(true)
	for index in range(labels.size()):
		tree.set_column_title(index, str(labels[index]))
		if index < widths.size():
			var min_width: int = int(widths[index])
			tree.set_column_custom_minimum_width(index, min_width)
			tree.set_column_expand(index, true)

func _set_empty_tree_placeholder(tree: Tree, message: String) -> void:
	if tree == null:
		return

	tree.clear()
	var root_item := tree.create_item()
	var main_text := str(message)
	root_item.set_text(0, main_text)
	for column_index in range(1, tree.columns):
		root_item.set_text(column_index, "")


func _restore_split_ratio() -> void:
	if main_split == null:
		return
	main_split.split_offsets = ProjectSettings.get_setting(PROJECT_SPLIT_RATIO_KEY, [0,240])
	sources_split.split_offsets = ProjectSettings.get_setting(PROJECT_SOURCES_SPLIT_RATIO_KEY, [0])

func _on_visibility_changed() -> void:
	if is_visible_in_tree():
		_full_refresh()

func _full_refresh() -> void:
	print("full refresh")
	_refresh_sources()
	
	core.RefreshAvailablePackagesCache()
	available_packages = core.GetAllAvailablePackages()
	_sort_packages_by_name(available_packages)
	_refresh_npm_registry_tree()

	if current_selected_npm_registry != null:
		_refresh_packages_for_registry(current_selected_npm_registry)
	else:
		_refresh_packages()

func _on_refresh_pressed() -> void:
	_full_refresh()
	if source_validation_label != null:
		source_validation_label.text = "Refreshed package manager data"

func _on_split_dragged(offset: int) -> void:
	ProjectSettings.set_setting(PROJECT_SPLIT_RATIO_KEY, main_split.split_offsets)
	ProjectSettings.set_initial_value(PROJECT_SPLIT_RATIO_KEY, [0,240])
	ProjectSettings.save()

func _on_sources_split_dragged(offset: int) -> void:
	ProjectSettings.set_setting(PROJECT_SOURCES_SPLIT_RATIO_KEY, sources_split.split_offsets)
	ProjectSettings.set_initial_value(PROJECT_SOURCES_SPLIT_RATIO_KEY, [0])
	ProjectSettings.save()

func _load_sources_settings() -> Array:
	var raw: Variant = ProjectSettings.get_setting(GodotPackageManager.PROJECT_SOURCES_KEY, {})
	var output: Array = []

	if typeof(raw) == TYPE_DICTIONARY:
		for key in raw.keys():
			var entry: Variant = raw[key]
			if typeof(entry) == TYPE_DICTIONARY:
				output.append(entry)
		return output

	if typeof(raw) != TYPE_ARRAY:
		return []

	for item in raw:
		if typeof(item) == TYPE_DICTIONARY:
			output.append(item)
	return output

func _save_sources_settings(entries: Array) -> void:
	var normalized: Array = []
	for item in entries:
		if typeof(item) != TYPE_DICTIONARY:
			continue
		var name: String = str(item.get("name", ""))
		if name == "":
			continue
		normalized.append(item)

	ProjectSettings.set_setting(GodotPackageManager.PROJECT_SOURCES_KEY, normalized)
	ProjectSettings.add_property_info({
		"name": GodotPackageManager.PROJECT_SOURCES_KEY,
		"type": TYPE_ARRAY,
		"hint": PROPERTY_HINT_NONE,
		"hint_string": ""
	})
	ProjectSettings.set_initial_value(GodotPackageManager.PROJECT_SOURCES_KEY, [])
	ProjectSettings.save()


func _current_godot_version() -> String:
	var version_info := Engine.get_version_info()
	var major := int(version_info.get("major", 4))
	var minor := int(version_info.get("minor", 0))
	var patch := int(version_info.get("patch", 0))
	return "%d.%d.%d" % [major, minor, patch]

func _package_display_name(package: PackageSummary) -> String:
	var package_name: String = package.Name
	var display_name: String = package.DisplayName
	if display_name != "":
		return display_name
	return package_name

func _latest_compatible_version(package: PackageSummary) -> PackageVersionEntry:
	var versions: Dictionary = core.GetPackageVersions(package).Versions
	if versions.size() == 0:
		return null
	var latest: PackageVersionEntry = versions[versions.keys()[0]]
	for version in versions:
		var version_name: String = version
		var godot_required: String = versions[version].GodotVersion
		if _is_godot_compatible(godot_required):
			if _compare_versions(version_name, latest.Version) > 0:
				latest = versions[version]
	return latest

func _is_godot_compatible(required_version: String) -> bool:	
	var current_selected_version := _current_godot_version()
	#print(" comparing version. required: " + required_version + " current godot version: " + current_selected_version)
	var resolver = PackageSemVerResolver.new()
	return resolver.Satisfies(current_selected_version, required_version)

func _compare_versions(left: String, right: String) -> int:
	var resolver = PackageSemVerResolver.new()
	return resolver.Compare(left, right)

func _refresh_sources() -> void:
	sources_tree.clear()
	_apply_tree_headers()
	var entries: Array = _load_sources_settings()
	if entries.is_empty():
		_set_empty_tree_placeholder(sources_tree, "No sources configured")
		sources_status.text = "0 source(s) configured"
		return

	var root := sources_tree.create_item()
	for entry in entries:
		var item := sources_tree.create_item(root)
		item.set_text(0, str(entry.get("name", "Unnamed")))
		var source_type := str(entry.get("type", "git"))
		item.set_text(1, source_type)
		var source_value := str(entry.get("url", ""))
		if source_value == "" and entry.has("path"):
			source_value = str(entry.get("path", ""))
		item.set_text(2, source_value)
	sources_status.text = "%d sources configured" % entries.size()

func _refresh_npm_registry_tree() -> void:
	npm_registry_tree.clear()
	_apply_tree_headers()

	var entries: Array = core.GetScopedRegistries()
	npm_registry_tree.set_column_titles_visible(true)
	if entries.is_empty():
		_set_empty_tree_placeholder(npm_registry_tree, "No registries configured")
		return

	for entry in entries:
		var item := npm_registry_tree.create_item()
		item.set_metadata(0, entry)
		item.set_text(0, entry.Name)
		item.set_text(1, entry.Url)
		item.set_text(2, _scopes_to_text(entry.Scopes))

	if current_selected_npm_registry != null:
		var root_item := npm_registry_tree.get_root()
		if root_item != null && root_item.get_child_count():
			var child := root_item.get_child(0)
			while child != null:
				if child.get_metadata(0) == current_selected_npm_registry:
					npm_registry_tree.select(child)
					_on_npm_registry_selected()
					return
				child = child.get_next()
	npm_registries_status.text = "%d registries configured" % entries.size()

func _scopes_to_text(scopes: Array) -> String:
	var text: String = ""
	for scope in scopes:
		if text != "":
			text += ", "
		text += str(scope)
	return text

func _refresh_packages() -> void:
	package_tree.clear()
	_apply_tree_headers()

	if available_packages.is_empty():
		_set_empty_tree_placeholder(package_tree, "No packages available")
		package_status.text = "0 package(s) compatible"
		return

	var root = package_tree.create_item()
	package_tree.hide_root = true

	for package_data in available_packages:
		var name: String = package_data.Name
		if name == "":
			continue
		var latest_version: PackageVersionEntry = _latest_compatible_version(package_data)
		var status := "Not Installed"
		var installed_version: PackageVersionEntry = core.GetInstalledPackageVersion(name)
		if installed_version != null:
			if installed_version.Version == latest_version.Version:
				status = "Installed"
			else:
				status = "Update Available"
		var item := package_tree.create_item(root)
		item.set_metadata(0, package_data)
		item.set_text(0, _package_display_name(package_data))
		item.set_text(1, latest_version.Version)
		item.set_text(2, status)

	package_status.text = "%d package(s) compatible with Godot %s" % [available_packages.size(), _current_godot_version()]
	if current_selected_package != null:
		_select_package(current_selected_package.Name)

func _on_npm_registry_selected() -> void:
	var selected := npm_registry_tree.get_selected()
	if selected == null:
		return

	current_selected_npm_registry = selected.get_metadata(0)

	if current_selected_npm_registry == null:
		package_status.text = "No registry selected"
		return

	package_status.text = "Packages from %s" % current_selected_npm_registry.Name
	_refresh_packages_for_registry(current_selected_npm_registry)

func _sort_packages_by_name(packages: Array) -> void:
	packages.sort_custom(func(a, b):
		var left_name := _package_display_name(a)
		var right_name := _package_display_name(b)
		return left_name.naturalnocasecmp_to(right_name) < 0
	)

func _refresh_packages_for_registry(npm_registry: NpmRegistryEntry) -> void:
	package_tree.clear()

	var packages_in_registry = core.GetAvailablePackagesFromRegistry(npm_registry.Name)
	_sort_packages_by_name(packages_in_registry)
	var shown_count: int = 0
	var root := package_tree.create_item()
	package_tree.hide_root = true

	for package in packages_in_registry:
		var package_name: String = package.Name
		if package_name == "":
			continue

		var installed_version: PackageVersionEntry = core.GetInstalledPackageVersion(package_name)
		var latest_version: PackageVersionEntry = _latest_compatible_version(package)
		var status := "Not Installed"
		if installed_version != null:
			if installed_version.Version == latest_version.Version:
				status = "Installed"
			else:
				status = "Update Available"

		var item := package_tree.create_item(root)
		item.set_metadata(0, package)
		item.set_text(0, _package_display_name(package))
		item.set_text(1, latest_version.Version)
		item.set_text(2, status)
		shown_count += 1

	if shown_count == 0:
		_set_empty_tree_placeholder(package_tree, "No packages for this registry")
		package_status.text = "0 package(s) from %s" % current_selected_npm_registry.Name
		return

	package_status.text = "%d package(s) from %s" % [shown_count, current_selected_npm_registry.Name]
	var root_item := package_tree.get_root()
	if root_item != null && root_item.get_child_count() > 0:
		var first_item := root_item.get_child(0)
		if first_item != null:
			package_tree.deselect_all()
			package_tree.set_selected(first_item, 0)
			_on_package_selected()

func _on_package_selected() -> void:
	var selected := package_tree.get_selected()
	if selected == null:
		return
	var package: PackageSummary = selected.get_metadata(0)
	var package_name: String = package.Name
	if package_name == "":
		return
	
	_select_package(package_name)

func _select_package(package_name: String) -> void:
	current_selected_package = null
	for package in available_packages:
		if package.Name == package_name:
			current_selected_package = package
			break

	if current_selected_package == null:
		current_selected_package = PackageSummary.new()
		selected_package_title.text = "None"
		selected_package_name.text = ""
		selected_package_version.text = ""
		selected_package_status.text = "Package not found"
		versions_tree.clear()
		return


	var installed: PackageVersionEntry = core.GetInstalledPackageVersion(current_selected_package.Name)
	var installed_version: String = "None"
	if installed != null:
		installed_version = installed.Version
	var display_name := _package_display_name(current_selected_package)
	selected_package_title.text = current_selected_package.Name
	selected_package_name.text = display_name
	selected_package_version.text = installed_version
	selected_package_status.text = ""
	description.text = current_selected_package.Description

	_build_version_tree()
	_build_dependency_tree()

func _build_dependency_tree() -> void:
	dependencies_tree.clear()
	
	var package_version: PackageVersionEntry = core.GetInstalledPackageVersion(current_selected_package.Name)
	if package_version == null:
		package_version = current_selected_package.LatestVersionData

	var root := dependencies_tree.create_item()
	dependencies_tree.hide_root = true

	var dependencies = package_version.Dependencies
	for dependency in dependencies:
		var package_name: String = dependency
		var required_version: String = dependencies[package_name]
		var status = "Not Installed"
		var installed_version: PackageVersionEntry = core.GetInstalledPackageVersion(package_name)
		if installed_version != null:
			var version_compare = _compare_versions(installed_version.Version,required_version)
			if version_compare == 0:
				status = "Installed"
			elif version_compare > 0:
				status = installed_version.Version+" Installed"
			elif version_compare < 0:
				status = installed_version.Version+" Need Update"
		var item := dependencies_tree.create_item(root)
		item.set_text(0, package_name)
		item.set_text(1, required_version)
		item.set_text(2, status)	

func _build_version_tree() -> void:
	versions_tree.clear()
	
	var versions: Array = core.GetPackageVersions(current_selected_package).Versions.values()
	
	versions.sort_custom(func(a, b):
		var left: String = a.Version
		var right: String = b.Version
		return _compare_versions(left, right) > 0
	)
	if versions.is_empty():
		_set_empty_tree_placeholder(versions_tree, "No versions available")
		return

	var installed_version = ""
	var installed_package_version: PackageVersionEntry = core.GetInstalledPackageVersion(current_selected_package.Name)
	if installed_package_version != null:
		installed_version = installed_package_version.Version

	var root := versions_tree.create_item()
	versions_tree.hide_root = true

	var button_index: int = 0
	for version in versions:
		var version_name: String = version.Version
		var godot_hint: String = version.GodotVersion
		var is_compatible := _is_godot_compatible(godot_hint)

		if not is_compatible:
			continue
		#var icon := icon_install
		var action := "Install"
		if installed_version == version_name:
			#icon = icon_remove
			action = "Remove"
		elif installed_version != "":
			#icon = icon_update
			action = "Replace"
		if not is_compatible:
			#icon = icon_blocked
			action = "Blocked"
		var item := versions_tree.create_item(root)
		item.set_metadata(0, version)
		item.set_text(0, version_name)
		item.set_text(1, godot_hint)
		#item.add_button(2, icon, button_index , false, action)
		item.set_text(2, action)
		button_index += 1

func _on_version_selected() -> void:
	var selected := versions_tree.get_selected()
	if selected == null:
		return
	current_selected_version = selected.get_metadata(0)
	if current_selected_version == null:
		return
	var version_option := selected.get_text(2)
	if version_option == "":
		return
	_select_version(current_selected_version, version_option)


func _select_version(version: PackageVersionEntry, version_option: String) -> void:
	current_selected_version = version
	selected_version.text = current_selected_version.Version
	install_button.disabled = false
	remove_button.disabled = true
	match version_option:
		"Remove":
			install_button.disabled = true
			remove_button.disabled = false
		"Install":
			install_button.text = "Install"
		"Update":
			install_button.text = "Update"
		"Downgrade":
			install_button.text = "Downgrade"


func _on_install_selected_version() -> void:
	if current_selected_package == null:
		return

	var package_name: String = current_selected_package.Name
	var version_to_install: PackageVersionEntry = current_selected_version
	if version_to_install == null:
		version_to_install = _latest_compatible_version(current_selected_package)
	if version_to_install == null:
		return

	var registry_url := "https://registry.npmjs.org"
	if current_selected_npm_registry != null:
		registry_url = current_selected_npm_registry.Url

	var install_result: PackageInstallationResult = core.InstallPackage(version_to_install)

	if install_result != null and install_result.Ok:
		var installed_path := str(install_result.ProjectDir)
		selected_package_status.text = "Installed %s@%s in %s" % [package_name, version_to_install.Version, installed_path]
	else:
		var error_text := "unknown error"
		if install_result != null:
			error_text = str(install_result.Error)
		selected_package_status.text = "Could not install %s@%s: %s" % [package_name, version_to_install.Version, error_text]
		_refresh_packages()
		_select_package(package_name)
		return

	var success: bool = false
	if version_to_install != null:
		success = core.AddDependency(version_to_install)
	else:
		success = core.AddDependency(package_name, version_to_install.Version)
	if success:
		selected_package_status.text = "Installed %s@%s in the project" % [package_name, version_to_install.Version]
	else:
		selected_package_status.text = "Installed %s@%s in cache, but could not update the manifest" % [package_name, version_to_install.Version]
	_refresh_packages()
	_select_package(package_name)

func _on_remove_current_package() -> void:
	if current_selected_package == null:
		return
	var package_name: String = current_selected_package.Name
	var version_to_remove: PackageVersionEntry = current_selected_version
	if version_to_remove == null:
		version_to_remove = _latest_compatible_version(current_selected_package)
	if version_to_remove == null:
		return

	var removed_from_project: bool = core.RemovePackage(package_name, version_to_remove.Version)
	if removed_from_project:
		selected_package_status.text = "Removed %s@%s from the project" % [package_name, version_to_remove.Version]
	else:
		selected_package_status.text = "Could not remove %s@%s from the project" % [package_name, version_to_remove.Version]
	_refresh_packages()
	_select_package(package_name)

func _on_edit_npm_registries_pressed() -> void:
	print("Editing")
	var win := Window.new()
	win.title = "NPM Registry Configuration"
	win.size = Vector2i(1000, 600)
	
	win.unresizable = true 

	win.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	win.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_EXPAND

	var registry_editor = load("res://addons/godot_package_manager/ui/npm_registry_property_editor.gd").new()
	win.add_child(registry_editor)

	registry_editor.set_anchors_preset(Control.PRESET_FULL_RECT)
	registry_editor.offset_left = 0
	registry_editor.offset_top = 0
	registry_editor.offset_right = 0
	registry_editor.offset_bottom = 0
	
	# Automatically clean up the dialog on close
	registry_editor.editing_finished.connect(func(is_saved: bool):
			win.queue_free()
	)

	win.close_requested.connect(
		func():
			win.queue_free()
	)
	
	EditorInterface.get_base_control().add_child(win)
	win.popup_centered()	

func _on_add_source_pressed() -> void:
	_show_source_type_menu()

func _show_source_type_menu() -> void:
	if source_type_menu == null or not is_instance_valid(source_type_menu):
		source_type_menu = PopupMenu.new()
		source_type_menu.add_item("Git repository")
		source_type_menu.add_item("Local folder")
		source_type_menu.id_pressed.connect(_on_source_type_selected)
		add_child(source_type_menu)

	var popup_position := add_source_button.get_global_transform_with_canvas().origin + Vector2(0, add_source_button.size.y)
	source_type_menu.popup(Rect2i(popup_position, Vector2i(200, 0)))

func _on_source_type_selected(id: int) -> void:
	if id == 0:
		current_selected_source_type = "git"
	elif id == 1:
		current_selected_source_type = "local"
	else:
		current_selected_source_type = "git"

	_open_source_form(current_selected_source_type)

func _on_edit_selected_source() -> void:
	var selected := sources_tree.get_selected()
	if selected == null:
		return

	var source_name := selected.get_text(0)
	var entries: Array = _load_sources_settings()
	for index in range(entries.size()):
		var entry: Dictionary = entries[index]
		if str(entry.get("name", "")) == source_name:
			source_is_editing = true
			source_edit_index = index
			current_selected_source_type = str(entry.get("type", "git"))
			if current_selected_source_type != "git" and current_selected_source_type != "local":
				current_selected_source_type = "git"
			if source_dialog == null or not is_instance_valid(source_dialog):
				_create_source_dialog()
			_populate_source_dialog(entry)
			source_dialog.title = "Edit package source"
			source_dialog.popup_centered()
			return

func _create_source_dialog() -> void:
	source_dialog = Window.new()
	source_dialog.title = "Add package source"
	source_dialog.initial_position = Window.WINDOW_INITIAL_POSITION_CENTER_MAIN_WINDOW_SCREEN
	source_dialog.size = Vector2i(440, 260)
	source_dialog.min_size = Vector2i(360, 220)
	source_dialog.exclusive = false
	source_dialog.transient = true
	source_dialog.close_requested.connect(func() -> void:
		source_dialog.hide()
	)

	var root := MarginContainer.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_theme_constant_override("margin_left", 12)
	root.add_theme_constant_override("margin_top", 12)
	root.add_theme_constant_override("margin_right", 12)
	root.add_theme_constant_override("margin_bottom", 12)
	source_dialog.add_child(root)

	var form := VBoxContainer.new()
	form.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	form.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(form)

	var name_label := Label.new()
	name_label.text = "Name"
	form.add_child(name_label)
	source_name_input = LineEdit.new()
	source_name_input.placeholder_text = "My package source"
	form.add_child(source_name_input)

	var url_label := Label.new()
	url_label.text = "Repository URL"
	form.add_child(url_label)
	source_url_input = LineEdit.new()
	source_url_input.placeholder_text = "https://github.com/user/repository.git"
	form.add_child(source_url_input)

	source_scopes_input = LineEdit.new()
	source_scopes_input.visible = false
	source_scopes_input.placeholder_text = ""
	form.add_child(source_scopes_input)

	source_validation_label = Label.new()
	source_validation_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	source_validation_label.modulate = Color(1.0, 0.8, 0.6, 1.0)
	form.add_child(source_validation_label)

	var actions := HBoxContainer.new()
	var save_button := Button.new()
	save_button.text = "Save"
	var cancel_button := Button.new()
	cancel_button.text = "Cancel"
	actions.add_child(save_button)
	actions.add_child(cancel_button)
	form.add_child(actions)

	save_button.pressed.connect(_save_source_dialog)
	cancel_button.pressed.connect(func() -> void:
		source_dialog.hide()
	)

	var root_tree := get_tree()
	if root_tree != null and root_tree.root != null:
		if source_dialog.get_parent() != root_tree.root:
			if source_dialog.get_parent() != null:
				source_dialog.reparent(root_tree.root)
			else:
				root_tree.root.add_child(source_dialog)
	else:
		add_child(source_dialog)
	_reset_source_dialog_fields()
	_configure_source_form_for_type(current_selected_source_type)

func _open_source_form(source_type: String) -> void:
	source_is_editing = false
	source_edit_index = -1
	if source_dialog == null or not is_instance_valid(source_dialog):
		_create_source_dialog()
	else:
		_reset_source_dialog_fields()
	current_selected_source_type = source_type
	_configure_source_form_for_type(current_selected_source_type)
	source_dialog.title = "Add package source"
	source_dialog.popup_centered()

func _configure_source_form_for_type(source_type: String) -> void:
	if source_dialog == null or source_url_input == null:
		return

	if source_type == "local":
		source_url_input.placeholder_text = "res://addons/my_package" 
		if source_url_input.get_parent() != null:
			var label_node := source_url_input.get_parent().get_child(2) as Label
			if label_node != null:
				label_node.text = "Folder path"
	else:
		source_url_input.placeholder_text = "https://github.com/user/repository.git"
		if source_url_input.get_parent() != null:
			var label_node := source_url_input.get_parent().get_child(2) as Label
			if label_node != null:
				label_node.text = "Repository URL"

func _reset_source_dialog_fields() -> void:
	if source_name_input != null:
		source_name_input.text = ""
	if source_url_input != null:
		source_url_input.text = ""
	if source_scopes_input != null:
		source_scopes_input.text = ""
	if source_validation_label != null:
		source_validation_label.text = ""

func _populate_source_dialog(entry: Dictionary) -> void:
	_reset_source_dialog_fields()
	current_selected_source_type = str(entry.get("type", "git"))
	if current_selected_source_type != "git" and current_selected_source_type != "local":
		current_selected_source_type = "git"
	_configure_source_form_for_type(current_selected_source_type)
	if source_name_input != null:
		source_name_input.text = str(entry.get("name", ""))
	if source_url_input != null:
		source_url_input.text = str(entry.get("url", entry.get("path", "")))
	if source_scopes_input != null:
		var scopes: Array = entry.get("scopes", [])
		var scopes_text := ""
		for scope in scopes:
			if scopes_text != "":
				scopes_text += ", "
			scopes_text += str(scope)
		source_scopes_input.text = scopes_text

func _validate_registry_entry(entry: Dictionary) -> String:
	var name := str(entry.get("name", "")).strip_edges()
	var type_value := str(entry.get("type", "git"))
	var value := str(entry.get("url", entry.get("path", ""))).strip_edges()

	if name == "":
		return "Source name is required."
	if value == "":
		if type_value == "local":
			return "Local folder path is required."
		return "Repository URL is required."

	if type_value == "git":
		var regex := RegEx.new()
		var compile_error := regex.compile("^https?://.+|^git@.+:.+$|^ssh://.+$")
		if compile_error != OK or not regex.search(value):
			return "Repository URL must be a valid git URL."
	elif type_value == "local":
		if value.begins_with("res://") == false and value.begins_with("user://") == false and value.find("://") != -1:
			return "Use a project path like res://... or a local filesystem path."

	return ""

func _save_source_dialog() -> void:
	var entries: Array = _load_sources_settings()
	var source_value := source_url_input.text.strip_edges()
	var type_value := current_selected_source_type
	if type_value != "git" and type_value != "local":
		type_value = "git"

	var new_entry: Dictionary = {
		"name": source_name_input.text.strip_edges(),
		"type": type_value,
		"auth": {},
		"scopes": []
	}

	if type_value == "git":
		new_entry["url"] = source_value
		new_entry["path"] = ""
	else:
		new_entry["path"] = source_value
		new_entry["url"] = ""

	var validation_error := _validate_registry_entry(new_entry)
	if validation_error != "":
		if source_validation_label != null:
			source_validation_label.text = validation_error
		return

	if source_is_editing and source_edit_index >= 0 and source_edit_index < entries.size():
		entries[source_edit_index] = new_entry
	else:
		entries.append(new_entry)

	_save_sources_settings(entries)
	_refresh_sources()
	if source_dialog != null:
		source_dialog.hide()

func _read_manifest_file(path: String) -> Dictionary:
	var manifest: Dictionary = {}
	var file_path := ProjectSettings.globalize_path(path)
	if not FileAccess.file_exists(file_path):
		return manifest

	var file := FileAccess.open(file_path, FileAccess.READ)
	if file == null:
		return manifest

	var content := file.get_as_text()
	file.close()
	if content.strip_edges() == "":
		return manifest

	var json := JSON.new()
	var parse_error := json.parse(content)
	if parse_error != OK:
		return manifest

	var parsed := json.data
	if typeof(parsed) == TYPE_DICTIONARY:
		manifest = parsed
	return manifest

func _write_manifest_file(path: String, manifest: Dictionary) -> bool:
	var file_path := ProjectSettings.globalize_path(path)
	var directory := file_path.get_base_dir()
	if not DirAccess.dir_exists_absolute(directory):
		DirAccess.make_dir_recursive_absolute(directory)

	var json := JSON.new()
	var text := json.stringify(manifest, "\t")
	var file := FileAccess.open(file_path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(text)
	file.close()
	return true

func _on_remove_selected_source() -> void:
	var selected := sources_tree.get_selected()
	if selected == null:
		return
	var source_name := selected.get_text(0)
	var entries: Array = _load_sources_settings()
	var filtered: Array = []
	for entry in entries:
		if str(entry.get("name", "")) != source_name:
			filtered.append(entry)
	_save_sources_settings(filtered)
	_refresh_sources()
