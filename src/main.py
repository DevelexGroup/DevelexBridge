import asyncio
import jsonschema
import jsonschema.exceptions
import validationSchemas as va
import response

from websocketserver.WebSocketServer import WebSocketServer
from trackers.gazepoint.OpenGazeTracker import OpenGazeTracker
from trackers.smi.SMITracker import SMITracker
from trackers.eyelogic.ELTracker import ELTracker
from trackers.dummy.DummyTracker import DummyTracker
from trackers.Tracker import Tracker
from typing import Any, Optional


tracker: Optional[Tracker] = None


async def handle_no_tracker(websocket_server: WebSocketServer) -> None:
    await data_callback(
        websocket_server, response.error_response("No tracker connected")
    )

    print("No tracker connected")


async def on_connect_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    jsonschema.validate(data, va.CONNECT_SCHEMA)

    match data["tracker"]:
        case "opengaze":
            if tracker is not None:
                await tracker.disconnect()

            tracker = OpenGazeTracker(
                data["keepFixations"],
                "localhost",
                4242,
                lambda data: data_callback(websocket_server, data),
            )

            await tracker.connect()
        case "smi":
            if tracker is not None:
                await tracker.disconnect()

            tracker = SMITracker(
                lambda data: data_callback(websocket_server, data),
                asyncio.get_event_loop(),
            )

            await tracker.connect()
        case "eyelogic":
            if tracker is not None:
                await tracker.disconnect()

            tracker = ELTracker(
                lambda data: data_callback(websocket_server, data),
                asyncio.get_event_loop(),
            )

            await tracker.connect()
        case "dummy":
            if tracker is not None:
                await tracker.disconnect()

            tracker = DummyTracker(
                lambda data: data_callback(websocket_server, data),
                asyncio.get_event_loop(),
                250,
            )

            await tracker.connect()
        case _:
            await data_callback(
                websocket_server, response.error_response("Unsupported tracker type")
            )
            print("Unsupported tracker type")
            return


async def on_start_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    if tracker is None:
        await handle_no_tracker(websocket_server)
        return

    await asyncio.create_task(tracker.start())


async def on_stop_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    if tracker is None:
        await handle_no_tracker(websocket_server)
        return

    await asyncio.create_task(tracker.stop())


async def on_calibrate_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    if tracker is None:
        await handle_no_tracker(websocket_server)
        return

    await asyncio.create_task(tracker.calibrate())


async def on_disconnect_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    if tracker is None:
        await handle_no_tracker(websocket_server)
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
    websocket_server: WebSocketServer, data: dict[Any, Any]
) -> None:
    await websocket_server.send_data(data)


async def received_data_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    print(f"Received data from client: {data}")

    try:
        jsonschema.validate(data, va.BASE_SCHEMA)

        asyncio.create_task(MESSAGE_CALLBACKS[data["type"]](data, websocket_server))
    except jsonschema.exceptions.ValidationError as e:
        await data_callback(websocket_server, response.error_response(e.message))
        print(f"Validation error: {e.message}")
        return


async def main() -> None:
    websocket_server = WebSocketServer("localhost", 13892, received_data_callback)

    print("Starting bridge...")

    await websocket_server.start_server()

    try:
        await asyncio.Future()
    except KeyboardInterrupt:
        await websocket_server.close()


if __name__ == "__main__":
    asyncio.run(main())
