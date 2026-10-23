# Godot Package Manager

Prototype of a Unity-like package manager for Godot.

## Goal
Provide a Godot editor plugin capable of:
- registering custom npm-like registries,
- installing packages from npm, git, or local folders,
- resolving dependencies and versions,
- maintaining a lockfile

## Structure
- `plugin.cfg`: Godot plugin metadata.
- `godot_package_manager.gd`: plugin entry point.
- `PackageManagerCore.cs`: main API entry for package management

