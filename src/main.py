import asyncio
import jsonschema
import jsonschema.exceptions
import schemas as va
import response
import output_handlers as oh

from websocketserver.WebSocketServer import WebSocketServer
from trackers.gazepoint.OpenGazeTracker import OpenGazeTracker
from trackers.smi.SMITracker import SMITracker
from trackers.eyelogic.ELTracker import ELTracker
from trackers.dummy.DummyTracker import DummyTracker
from trackers.Tracker import Tracker, TrackerState
from typing import Any, Optional


tracker: Optional[Tracker] = None
VERSION = "1.0.2"


async def on_connect_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    jsonschema.validate(data, va.CONNECT_SCHEMA)

    if tracker is not None:
        if tracker.state == TrackerState.CONNECTING:
            await oh.handle_tracker_connecting(websocket_server)
            return
        elif (
            tracker.state != TrackerState.DISCONNECTED
            and tracker.state != TrackerState.CONNECTING
        ):
            await oh.handle_tracker_already_connected(websocket_server)
            return

    match data["tracker"]:
        case "opengaze":
            tracker = OpenGazeTracker(
                data["keepFixations"],
                "localhost",
                4242,
                lambda data: oh.data_callback(websocket_server, data),
                asyncio.get_event_loop(),
            )

            await tracker.connect()
        case "smi":
            tracker = SMITracker(
                lambda data: oh.data_callback(websocket_server, data),
                asyncio.get_event_loop(),
            )

            await tracker.connect()
        case "eyelogic":
            tracker = ELTracker(
                lambda data: oh.data_callback(websocket_server, data),
                asyncio.get_event_loop(),
            )

            await tracker.connect()
        case "dummy":
            tracker = DummyTracker(
                lambda data: oh.data_callback(websocket_server, data),
                asyncio.get_event_loop(),
                250,
            )

            await tracker.connect()
        case _:
            await oh.data_callback(
                websocket_server, response.error_response("Unsupported tracker type")
            )
            print("[DEVELEX] Unsupported tracker type")
            return


async def on_start_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    if tracker is None or tracker.state == TrackerState.DISCONNECTED:
        await oh.handle_no_tracker(websocket_server)
        return

    if tracker.state == TrackerState.CONNECTING:
        await oh.handle_tracker_connecting(websocket_server)
        return

    await asyncio.create_task(tracker.start())


async def on_stop_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    if tracker is None or tracker.state == TrackerState.DISCONNECTED:
        await oh.handle_no_tracker(websocket_server)
        return

    if tracker.state == TrackerState.CONNECTING:
        await oh.handle_tracker_connecting(websocket_server)
        return

    if tracker.state != TrackerState.STARTED:
        await oh.handle_tracker_not_started(websocket_server)
        return

    await asyncio.create_task(tracker.stop())


async def on_calibrate_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    if tracker is None or tracker.state == TrackerState.DISCONNECTED:
        await oh.handle_no_tracker(websocket_server)
        return

    if tracker.state == TrackerState.CONNECTING:
        await oh.handle_tracker_connecting(websocket_server)
        return

    await asyncio.create_task(tracker.calibrate())


async def on_disconnect_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    global tracker

    if tracker is None or tracker.state == TrackerState.DISCONNECTED:
        await oh.handle_no_tracker(websocket_server)
        return

    if tracker.state == TrackerState.CONNECTING:
        await oh.handle_tracker_connecting(websocket_server)
        return

    await asyncio.create_task(tracker.disconnect())

    if tracker.state == TrackerState.DISCONNECTED:
        tracker = None


# TODO: maybe with decorators?
MESSAGE_CALLBACKS = {
    "connect": on_connect_callback,
    "start": on_start_callback,
    "stop": on_stop_callback,
    "calibrate": on_calibrate_callback,
    "disconnect": on_disconnect_callback,
}


async def received_data_callback(
    data: dict[Any, Any], websocket_server: WebSocketServer
) -> None:
    print(f"[DEVELEX] Received data from client: {data}")

    try:
        jsonschema.validate(data, va.BASE_SCHEMA)

        await asyncio.create_task(
            MESSAGE_CALLBACKS[data["type"]](data, websocket_server)
        )
    except jsonschema.exceptions.ValidationError as e:
        await oh.data_callback(websocket_server, response.error_response(e.message))
        print(f"Validation error: {e.message}")
        return


async def main() -> None:
    websocket_server = WebSocketServer("localhost", 13892, received_data_callback)

    print(f"[DEVELEX] Starting bridge... version: {VERSION}")

    await websocket_server.start_server()

    try:
        await asyncio.Future()
    except KeyboardInterrupt:
        await websocket_server.close()


if __name__ == "__main__":
    asyncio.run(main())
