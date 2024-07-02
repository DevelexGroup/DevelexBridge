import asyncio
import jsonschema
import jsonschema.exceptions
import validationSchemas as va

from websocketserver import WebSocketServer
from trackers.gazepoint.OpenGazeTracker import OpenGazeTracker
from trackers.smi.SMITracker import SMITracker
from trackers.eyelogic.ELTracker import ELTracker
from typing import Any


tracker = None


async def on_connect_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer.WebSocketServer
) -> None:
    global tracker

    jsonschema.validate(data, va.CONNECT_SCHEMA)

    match data["tracker"]:
        case "opengaze":
            if tracker is not None:
                tracker.disconnect()

            tracker = OpenGazeTracker(
                data["keepFixations"],
                "localhost",
                4242,
                lambda data: data_callback(websocket_server, data),
            )

            await tracker.connect()
        case "smi":
            tracker = SMITracker(lambda data: data_callback(websocket_server, data))

            await tracker.connect()
        case "eyelogic":
            tracker = ELTracker(lambda data: data_callback(websocket_server, data))

            await tracker.connect()
        case _:
            print("Unsupported tracker type")
            return


async def on_start_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer.WebSocketServer
) -> None:
    global tracker

    if tracker is None:
        print("No tracker connected")
        return

    await asyncio.create_task(tracker.start())


async def on_stop_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer.WebSocketServer
) -> None:
    global tracker

    if tracker is None:
        print("No tracker connected")
        return

    await asyncio.create_task(tracker.stop())

async def on_calibrate_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer.WebSocketServer
) -> None:
    global tracker

    if tracker is None:
        print("No tracker connected")
        return

    await asyncio.create_task(tracker.calibrate())


async def on_disconnect_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer.WebSocketServer
) -> None:
    global tracker

    if tracker is None:
        print("No tracker connected")
        return

    await asyncio.create_task(tracker.disconnect())


# TODO: maybe with decorators?
MESSAGE_CALLBACKS = {
    "connect": on_connect_callback,
    "start": on_start_callback,
    "stop": on_stop_callback,
    "calibrate": on_calibrate_callback,
    "disconnect": on_disconnect_callback,
}


async def data_callback(
    websocket_server: WebSocketServer.WebSocketServer, data: dict[Any, Any]
) -> None:
    await websocket_server.send_data(data)


async def received_data_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer.WebSocketServer
) -> None:
    print(f"Received data from client: {data}")

    try:
        jsonschema.validate(data, va.BASE_SCHEMA)

        asyncio.create_task(MESSAGE_CALLBACKS[data["type"]](data, websocket_server))
    except jsonschema.exceptions.ValidationError as e:
        print(f"Validation error: {e.message}")
        return


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
