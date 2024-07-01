from typing import Any


def response(_type: str, data: dict[Any, Any] = {}) -> dict[Any, Any]:
    return {**data, "type": _type}


def error_response(message: str) -> dict[Any, Any]:
    return response("error", {"message": message})
