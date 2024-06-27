import asyncio

from websocketserver import WebSocketServer
from trackers.gazepoint import OpenGazeTracker
from typing import Any


tracker = None


async def data_callback(
    websocket_server: WebSocketServer.WebSocketServer, data: dict[Any, Any]
) -> None:
    await websocket_server.send_data(data)


async def received_data_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer.WebSocketServer
) -> None:
    print(f"Received data from client: {data}")

    if "type" in data:
        if data["type"] == "config":
            await handle_received_config_data(data, websocket_server)


async def handle_received_config_data(
    data: dict[Any, Any], websocket_server: WebSocketServer.WebSocketServer
) -> None:
    print(f"Received configuration data from client: {data}")
    global tracker
    if tracker is not None:
        tracker.disconnect()

    data_callback_partial = lambda data: data_callback(websocket_server, data)
    tracker = OpenGazeTracker.OpenGazeTracker(
        True, "localhost", 4242, data_callback_partial
    )

    await asyncio.create_task(tracker.connect())


async def main() -> None:
    websocket_server = WebSocketServer.WebSocketServer(
        "localhost", 13892, received_data_callback
    )

    print("Starting bridge...")

    await websocket_server.start_server()

    try:
        await asyncio.Future()
    except KeyboardInterrupt:
        await websocket_server.close()


if __name__ == "__main__":
    asyncio.run(main())
