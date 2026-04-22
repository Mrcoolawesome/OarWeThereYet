extends Node

# enum for choosing what multiplayer version we're on
enum MULTIPLAYER_NETWORK_TYPE {ENET, STEAM}

# default network type is the built-in one
var active_network_type: MULTIPLAYER_NETWORK_TYPE = MULTIPLAYER_NETWORK_TYPE.STEAM;

# Which save slot the player currently wants to save and load
var save_slot: int = 0;

var motivator_speed: int = -1;