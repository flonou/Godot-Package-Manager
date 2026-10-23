extends Control 

signal editing_finished(is_saved: bool)

var package_manager_core: PackageManagerCore
var container: VBoxContainer
var npm_registry_tree: Tree
var add_button: Button
var edit_button: Button
var remove_button: Button
var name_edit: LineEdit
var url_edit: LineEdit
var scopes_edit: LineEdit
var save_button: Button
var cancel_button: Button
var status_label: Label
var current_npm_entry: NpmRegistryEntry

func _ready() -> void:
	package_manager_core = PackageManagerCore.GetInstance()

	var scene := preload("res://addons/godot_package_manager/ui/npm_registry_property_editor.tscn").instantiate()
	container = scene as VBoxContainer
	add_child(container)

	npm_registry_tree = container.get_node("RegistryTree") as Tree
	add_button = container.get_node("Toolbar/AddButton") as Button
	edit_button = container.get_node("Toolbar/EditButton") as Button
	remove_button = container.get_node("Toolbar/RemoveButton") as Button
	name_edit = container.get_node("Form/NameEdit") as LineEdit
	url_edit = container.get_node("Form/URLEdit") as LineEdit
	scopes_edit = container.get_node("Form/ScopesEdit") as LineEdit
	save_button = container.get_node("Form/Buttons/SaveButton") as Button
	cancel_button = container.get_node("Form/Buttons/CancelButton") as Button
	status_label = container.get_node("StatusLabel") as Label

	_connect_signals()
	_refresh_npm_registry_tree()
	_clear_form()

func _connect_signals() -> void:
	add_button.pressed.connect(_on_add_pressed)
	edit_button.pressed.connect(_on_edit_pressed)
	remove_button.pressed.connect(_on_remove_pressed)
	save_button.pressed.connect(_on_save_pressed)
	cancel_button.pressed.connect(_on_cancel_pressed)
	name_edit.text_changed.connect(_on_name_changed)
	url_edit.text_changed.connect(_on_url_changed)
	scopes_edit.text_changed.connect(_on_scopes_changed)
	npm_registry_tree.item_selected.connect(_on_npm_tree_selected)

func _on_npm_tree_selected() -> void:
	var selected := npm_registry_tree.get_selected()
	if selected == null:
		return
	var selected_name := selected.get_text(0)
	if selected_name != "":
		status_label.text = "Selected: %s" % selected_name
	var entry: NpmRegistryEntry = selected.get_metadata(0)
	_update_edit_form(entry)

func _on_add_pressed() -> void:
	var entry: NpmRegistryEntry = NpmRegistryEntry.new()
	package_manager_core.AddScopedRegistry(entry)
	_refresh_npm_registry_tree()
	_select_entry(entry)

func _update_edit_form(entry: NpmRegistryEntry) -> void:
	if entry == null:
		_clear_form()
		return
	current_npm_entry = entry
	name_edit.text = entry.Name
	url_edit.text = entry.Url
	scopes_edit.text = _scopes_to_text(entry.Scopes)
	status_label.text = "Editing %s" % entry.Name
	_check_form()

func _on_edit_pressed() -> void:
	var selected := npm_registry_tree.get_selected()
	if selected == null:
		status_label.text = "Select a repository to edit."
		return

	var entry: NpmRegistryEntry = selected.get_metadata(0)
	_update_edit_form(entry)

func _on_remove_pressed() -> void:
	var selected := npm_registry_tree.get_selected()
	if selected == null:
		status_label.text = "Select a repository to remove."
		return

	var entry: NpmRegistryEntry = selected.get_metadata(0)
	if package_manager_core.RemoveScopedRegistry(entry):
		_refresh_npm_registry_tree()
		_clear_form()
		status_label.text = "Removed %s" % entry.Name
		_set_state_valid(true)
	else:
		status_label.text = "Failed to remove %s" % entry.Name
	

func _select_entry(entry: NpmRegistryEntry) -> void:
	var item_to_select = null
	var root = npm_registry_tree.get_root()
	var current_item: TreeItem = root.get_first_child()
	while current_item != null:
		if current_item.get_metadata(0) == entry:
			item_to_select = current_item
			current_npm_entry = entry
			break
		current_item = current_item.get_next_in_tree()
	npm_registry_tree.set_selected(item_to_select,0)

func _refresh_entry(item: TreeItem) -> void:
	if item == null:
		return
	
	item.set_text(0, name_edit.text)
	item.set_text(1, url_edit.text)
	item.set_text(2, scopes_edit.text)

func _on_save_pressed() -> void:
	package_manager_core.SaveManifestData()

func _set_state_valid(valid: bool) -> void:
	save_button.disabled = !valid
	npm_registry_tree.set_process_input(valid)

func _check_form() -> void:
	var prevent_save = false
	var name = name_edit.text
	var url = url_edit.text
	var scopes = scopes_edit.text
	if name == "":
		status_label.text = "Repository name is required."
		prevent_save = true
	var existing_registry: NpmRegistryEntry= package_manager_core.GetScopedRegistry(name)
	if existing_registry != null && existing_registry != current_npm_entry:
		status_label.text = "Repository name already used."
		prevent_save = true
	if url == "":
		status_label.text = "Repository URL is required."
		prevent_save = true
	if not url.begins_with("http://") and not url.begins_with("https://"):
		status_label.text = "URL must start with http:// or https://"
		prevent_save = true
	_set_state_valid(!prevent_save)
	current_npm_entry.Name = name
	current_npm_entry.Url = url
	current_npm_entry.Scopes = _scopes_from_text(scopes)

func _on_name_changed(new_name: String) -> void:
	_check_form()
	_refresh_entry(npm_registry_tree.get_selected())

func _on_url_changed(new_url: String) -> void:
	_check_form()
	_refresh_entry(npm_registry_tree.get_selected())

func _on_scopes_changed(new_scopes: String) -> void:
	_check_form()
	_refresh_entry(npm_registry_tree.get_selected())

func _on_cancel_pressed() -> void:
	_clear_form()
	package_manager_core.RefreshManifestData()
	status_label.text = "Registry editor cleared."
	editing_finished.emit(false) 

func _clear_form() -> void:
	name_edit.text = ""
	url_edit.text = "https://"
	scopes_edit.text = ""

func _save_entries(entries: Array[NpmRegistryEntry]) -> void:
	var valid_entries: Array[NpmRegistryEntry] = []
	for item in entries:
		if item == null:
			continue
		if item.Name == "" or item.Url == "":
			continue
		valid_entries.append(item)

	package_manager_core.SaveNpmRegistries(valid_entries)


func _refresh_npm_registry_tree() -> void:
	npm_registry_tree.clear()
	npm_registry_tree.columns = 3
	npm_registry_tree.set_column_titles_visible(true)
	npm_registry_tree.set_column_title(0, "Name")
	npm_registry_tree.set_column_title(1, "URL")
	npm_registry_tree.set_column_title(2, "Scopes")
	npm_registry_tree.hide_root = true
	var root = npm_registry_tree.create_item()

	var entries: Array[NpmRegistryEntry] = package_manager_core.GetScopedRegistries()
	for index in range(entries.size()):
		var entry: NpmRegistryEntry = entries[index]
		if entry == null:
			continue
		var item: TreeItem = npm_registry_tree.create_item(root)
		item.set_text(0, entry.Name)
		item.set_text(1, entry.Url)
		item.set_text(2, _scopes_to_text(entry.Scopes))
		item.set_metadata(0, entry)

	status_label.text = "%d registry(s) configured" % entries.size()

func _scopes_to_text(scopes: Array) -> String:
	var text := ""
	for scope in scopes:
		if text != "":
			text += ", "
		text += str(scope)
	return text

func _scopes_from_text(text: String) -> Array:
	var scopes: Array = []
	if text.strip_edges() == "":
		return scopes
	var parts := text.split(",")
	for part in parts:
		var trimmed := part.strip_edges()
		if trimmed != "":
			scopes.append(trimmed)
	return scopes

func update_property() -> void:
	_refresh_npm_registry_tree()
