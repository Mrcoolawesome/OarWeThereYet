class_name GameSaves extends Resource

@export var checkpoint_num: int = 0
@export var boat_inventory: Array[Dictionary] = []
@export var world_items: Array[Dictionary] = []

const SAVE_DIR := "user://saves/"

func save(slot: int) -> void:
	DirAccess.make_dir_recursive_absolute(SAVE_DIR)
	ResourceSaver.save(self, _path(slot))

static func load_or_create(slot: int) -> GameSaves:
	var path := _path(slot)
	if ResourceLoader.exists(path):
		var res := ResourceLoader.load(path) as GameSaves
		if res:
			return res
	return GameSaves.new()

static func delete_save(slot: int) -> void:
	var path := _path(slot)
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)

static func has_save(slot: int) -> bool:
	return FileAccess.file_exists(_path(slot))

static func _path(slot: int) -> String:
	return SAVE_DIR + "save_%d.tres" % slot