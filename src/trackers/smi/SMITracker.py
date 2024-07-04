import asyncio
from trackers.Tracker import Tracker
import ctypes as ct
from typing import Callable, Any, Coroutine
import response

try:
    from . import iViewXAPI
except Exception:
    raise Exception("Failed to load iViewXAPI")


class SMITracker(Tracker):
    __model: str = "smi"

    def __init__(
        self,
        data_callback: Callable[[Any], Coroutine[Any, Any, None]],
        loop: asyncio.AbstractEventLoop,
    ):
        self.api = iViewXAPI.iViewXAPI
        self.data_callback = data_callback
        self.paused = True
        self.sample_func = ct.WINFUNCTYPE(ct.c_int, iViewXAPI.CSample)(
            self.sample_callback
        )
        self.loop = loop

    async def connect(self) -> None:
        res = self.api.iV_SetLogger(ct.c_int(1), ct.c_char_p(b"tracker.log"))
        res = self.api.iV_Connect(
            ct.c_char_p(b"127.0.0.1"),
            ct.c_int(4444),
            ct.c_char_p(b"127.0.0.1"),
            ct.c_int(5555),
        )

        success, message = iViewXAPI.handle_return_code(res)

        if not success:
            await self.data_callback(response.error_response(message))
            return

        res = self.api.iV_SetSampleCallback(self.sample_func)
        success, message = iViewXAPI.handle_return_code(res)

        if success:
            await self.data_callback(response.response("connected", {}))
        else:
            await self.data_callback(response.error_response(message))

    async def disconnect(self) -> None:
        res = self.api.iV_Disconnect()

        success, message = iViewXAPI.handle_return_code(res)

        if success:
            await self.data_callback(response.response("disconnected", {}))
        else:
            await self.data_callback(response.error_response(message))

    async def start(self) -> None:
        self.paused = False

    async def stop(self) -> None:
        self.paused = True

    async def calibrate(self) -> None:
        res = self.api.iV_Calibrate()

        success, message = iViewXAPI.handle_return_code(res)

        if success:
            await self.data_callback(response.response("calibrated", {}))
        else:
            await self.data_callback(response.error_response(message))

    def sample_callback(self, sample: iViewXAPI.CSample) -> int:
        if self.paused:
            return 0

        asyncio.run_coroutine_threadsafe(
            self.send_callback_data(sample),
            self.loop,
        )

        return 0

    async def send_callback_data(self, sample: iViewXAPI.CSample) -> None:
        await self.data_callback(
            response.response(
                "point",
                {
                    "xL": sample.leftEye.gazeX,
                    "yL": sample.leftEye.gazeY,
                    "xR": sample.rightEye.gazeX,
                    "yR": sample.rightEye.gazeY,
                    "timestamp": sample.timestamp / 1000000,
                },
            )
        )

    @property
    def model(self) -> str:
        return self.__model
