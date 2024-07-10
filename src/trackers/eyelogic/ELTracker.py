from trackers.Tracker import Tracker, TrackerState
from typing import Callable, Any, Coroutine, Optional
import response
from .ELApiExtension import (
    handle_connect_return,
    handle_start_return,
    handle_calibrate_return,
)
import asyncio

try:
    import trackers.eyelogic.api.ELApi as EL
except Exception:
    raise Exception("Unable to import ELApi")


class ELTracker(Tracker):
    __state: TrackerState = TrackerState.DISCONNECTED
    __model: str = "eyelogic"
    __sample_callback = Optional[EL.GazeSampleCallback]
    __event_callback = Optional[EL.EventCallback]

    def __init__(
        self,
        data_callback: Callable[[Any], Coroutine[Any, Any, None]],
        loop: asyncio.AbstractEventLoop,
    ):
        self.data_callback = data_callback
        self.__sample_callback = None
        self.__event_callback = None
        self.api = EL.ELApi("Develex")
        self.api.registerEventCallback(self.get_event_callback())
        self.api.registerGazeSampleCallback(self.get_sample_callback())
        self.loop = loop

    async def connect(self) -> None:
        self.__state = TrackerState.CONNECTING
        success, message = handle_connect_return(self.api.connect())

        if not success:
            self.__state = TrackerState.DISCONNECTED
            await self.data_callback(response.error_response(message))
            return

        self.__state = TrackerState.CONNECTED
        await self.data_callback(response.response("connected"))

    async def disconnect(self) -> None:
        self.api.disconnect()
        self.api.registerGazeSampleCallback(None)
        self.api.registerEventCallback(None)

        self.__state = TrackerState.DISCONNECTED

        await self.data_callback(response.response("disconnected"))

    async def calibrate(self) -> None:
        success, message = handle_calibrate_return(self.api.calibrate(0))

        if not success:
            await self.data_callback(response.error_response(message))
            return

        await self.data_callback(response.response("calibrated"))

    async def start(self) -> None:
        success, message = handle_start_return(self.api.requestTracking(0))

        if not success:
            await self.data_callback(response.error_response(message))
            return

        self.__state = TrackerState.STARTED
        await self.data_callback("started")

    async def stop(self) -> None:
        self.api.unrequestTracking()

        self.__state = TrackerState.STOPPED
        await self.data_callback("stopped")

    @property
    def model(self) -> str:
        return self.__model

    @property
    def state(self) -> TrackerState:
        return self.__state

    """
        EyeLogic API Callbacks
    """

    def get_sample_callback(self) -> EL.GazeSampleCallback:
        @EL.GazeSampleCallback
        def gaze_sample_callback(sample: EL.POINTER(EL.ELGazeSample)) -> None:  # type: ignore
            asyncio.run_coroutine_threadsafe(
                self.send_callback_data(sample),
                self.loop,
            )

        if self.__sample_callback is None:
            self.__sample_callback = EL.GazeSampleCallback(gaze_sample_callback)

        return self.__sample_callback

    async def send_callback_data(self, sample: EL.POINTER(EL.ELGazeSample)) -> None:  # type: ignore
        await self.data_callback(
            response.response(
                "point",
                {
                    "xL": sample.contents.porLeftX,
                    "yL": sample.contents.porLeftY,
                    "xR": sample.contents.porRawX,
                    "yR": sample.contents.porRawY,
                    "timestamp": sample.contents.timestampMicroSec / 1000000,
                },
            )
        )

    def get_event_callback(self) -> EL.EventCallback:
        @EL.EventCallback
        def event_callback(event: EL.ELEvent) -> None:  # type: ignore
            print("event")
            if event == EL.ELEvent.CONNECTION_CLOSED:
                self.api.registerGazeSampleCallback(None)
                self.api.registerEventCallback(None)

        if self.__event_callback is None:
            self.__event_callback = EL.EventCallback(event_callback)

        return self.__event_callback
