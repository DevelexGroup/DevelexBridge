import response
from websocketserver.WebSocketServer import WebSocketServer
from typing import Any


async def data_callback(
    websocket_server: WebSocketServer, data: dict[Any, Any]
) -> None:
    await websocket_server.send_data(data)


async def handle_no_tracker(websocket_server: WebSocketServer) -> None:
    await data_callback(
        websocket_server, response.error_response("No tracker connected")
    )

    print("[DEVELEX] No tracker connected")


async def handle_tracker_already_connected(websocket_server: WebSocketServer) -> None:
    await data_callback(
        websocket_server, response.error_response("Tracker already connected")
    )

    print("[DEVELEX] Tracker already connected")


async def handle_tracker_connecting(websocket_server: WebSocketServer) -> None:
    await data_callback(
        websocket_server, response.error_response("Tracker is connecting")
    )

    print("[DEVELEX] Tracker is connecting")


async def handle_tracker_not_started(websocket_server: WebSocketServer) -> None:
    await data_callback(
        websocket_server,
        response.error_response("Tracker is connected, but not started"),
    )

    print("[DEVELEX] Tracker is connected, but not started")
