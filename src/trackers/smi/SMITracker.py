from trackers.Tracker import Tracker
import ctypes as ct
from typing import Callable, Any, Coroutine
import response

try:
    from . import iViewXAPI
except Exception:
    raise Exception("Failed to load iViewXAPI")


class SMITracker(Tracker):
    _model: str = "smi"

    def __init__(self, data_callback: Callable[[Any], Coroutine[Any, Any, None]]):
        self.api = iViewXAPI.iViewXAPI
        self.data_callback = data_callback
        self.paused = True
        self.sample_func = ct.WINFUNCTYPE(ct.c_int, iViewXAPI.CSample)(
            self.sample_callback
        )

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

    def start(self) -> None:
        self.paused = False

    def stop(self) -> None:
        self.paused = True

    async def calibrate(self) -> None:
        res = self.api.iV_Calibrate()

        success, message = iViewXAPI.handle_return_code(res)

        if success:
            await self.data_callback(response.response("calibrated", {}))
        else:
            await self.data_callback(response.error_response(message))

    async def sample_callback(self, sample: iViewXAPI.CSample) -> int:
        if self.paused:
            return 0

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

        return 0
