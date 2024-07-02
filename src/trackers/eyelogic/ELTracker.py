from trackers.Tracker import Tracker
from typing import Callable, Any, Coroutine, Optional
import response
from .ELApiExtension import (
    handle_connect_return,
    handle_start_return,
    handle_calibrate_return,
)

try:
    import eyelogic.ELApi as EL
except Exception:
    raise Exception("Unable to import ELApi")


class ELTracker(Tracker):
    __model: str = "eyelogic"
    __sample_callback = Optional[EL.GazeSampleCallback]
    __event_callback = Optional[EL.EventCallback]

    def __init__(self, data_callback: Callable[[Any], Coroutine[Any, Any, None]]):
        self.data_callback = data_callback
        self.__sample_callback = None
        self.__event_callback = None
        self.api = EL.ELApi("Develex client")
        self.api.registerEventCallback(self.get_event_callback())
        self.api.registerGazeSampleCallback(self.get_sample_callback())

    async def connect(self) -> None:
        success, message = handle_connect_return(self.api.connect())

        if not success:
            await self.data_callback(response.error_response(message))
            return

        await self.data_callback(response.response("connected"))

    async def disconnect(self) -> None:
        self.api.disconnect()
        self.api.registerGazeSampleCallback(None)
        self.api.registerEventCallback(None)

        await self.data_callback(response.response("disconnected"))

    async def calibrate(self) -> None:
        success, message = handle_calibrate_return(self.api.calibrate())

        if not success:
            await self.data_callback(response.error_response(message))
            return

        await self.data_callback(response.response("calibrated"))

    async def start(self) -> None:
        success, message = handle_start_return(self.api.requestTracking(0))

        if not success:
            await self.data_callback(response.error_response(message))
            return

    async def stop(self) -> None:
        self.api.unrequestTracking()

    @property
    def model(self) -> str:
        return self.__model

    """
        EyeLogic API Callbacks
    """

    def get_sample_callback(self) -> EL.GazeSampleCallback:
        @EL.GazeSampleCallback
        async def gaze_sample_callback(sample: POINTER(EL.ELGazeSample)) -> None:  # type: ignore
            await self.data_callback(
                response.response(
                    "point",
                    {
                        "xL": sample.contents.porLeftX,
                        "yL": sample.contents.porLeftY,
                        "xR": sample.contents.porRawX,
                        "yR": sample.contents.porRawY,
                        "timestamp": sample.contents.time / 1000000,
                    },
                )
            )

        if self.__sample_callback is None:
            self.__sample_callback = EL.GazeSampleCallback(gaze_sample_callback)

        return self.__sample_callback

    def get_event_callback(self) -> EL.EventCallback:
        @EL.EventCallback
        def event_callback(event_ptr: POINTER(EL.ELEvent)) -> None:  # type: ignore
            event = EL.ELEvent(event_ptr)

            if event == EL.ELEvent.CONNECTION_CLOSED:
                self.api.registerGazeSampleCallback(None)
                self.api.registerEventCallback(None)

        if self.__event_callback is None:
            self.__event_callback = EL.EventCallback(event_callback)

        return self.__event_callback
