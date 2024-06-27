METHODS = ["connect", "start", "stop", "calibrate", "disconnect"]
EYE_TRACKERS = ["opengaze", "smi", "eyelogic"]

BASE_SCHEMA = {
    "type": "object",
    "properties": {
        "type": {
            "type": "string",
            "enum": METHODS,
        },
    },
    "required": ["type"],
}

CONNECT_SCHEMA = {
    "type": "object",
    "properties": {
        "type": {
            "type": "string",
            "enum": METHODS,
        },
        "tracker": {"type": "string", "enum": EYE_TRACKERS},
        "keepFixations": {"type": "boolean"},
    },
    "oneOf": [
        {
            "properties": {"tracker": {"enum": ["smi", "eyelogic"]}},
            "required": ["type", "tracker"],
        },
        {
            "required": ["type", "tracker", "keepFixations"],
        },
    ],
}
