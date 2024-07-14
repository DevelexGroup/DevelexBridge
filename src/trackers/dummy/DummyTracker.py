import threading
import response
from trackers.Tracker import Tracker, TrackerState
import asyncio
from random import randrange
from time import sleep
from typing import Optional


class DummyTracker(Tracker):
    __state: TrackerState = TrackerState.DISCONNECTED
    __model: str = "dummy"
    __sample_callback = None
    __sample_thread: Optional[threading.Thread] = None
    __sample_thread_running = False
    __messages_sent = 0

    def __init__(self, data_callback, loop, freq=30):
        self.data_callback = data_callback
        self.paused = True
        self.loop = loop
        self.freq = freq
        self.__sample_thread_running = True
        self.__sample_thread = threading.Thread(target=self.sample_feeder)
        self.__sample_thread.daemon = True
        self.__sample_thread.start()

    def __del__(self):
        if self.__sample_thread is not None:
            self.__sample_thread_running = False
            self.__sample_thread.join()

    @property
    def model(self) -> str:
        return self.__model

    @property
    def state(self) -> TrackerState:
        return self.__state

    async def connect(self) -> None:
        self.get_sample_callback()
        self.__state = TrackerState.CONNECTED
        await self.data_callback(response.response("connected"))

    async def disconnect(self) -> None:
        if self.__sample_thread is not None:
            self.__sample_thread_running = False
            self.__sample_thread.join()

        self.__state = TrackerState.DISCONNECTED

        await self.data_callback(response.response("disconnected"))

    async def calibrate(self) -> None:
        await self.data_callback("calibrated")

    async def start(self) -> None:
        self.paused = False
        self.__state = TrackerState.STARTED
        await self.data_callback("started")

    async def stop(self) -> None:
        self.paused = True
        self.__state = TrackerState.STOPPED
        await self.data_callback("stopped")

    def get_sample_callback(self):
        def gaze_sample_callback(sample: dict[str, float]) -> None:
            asyncio.run_coroutine_threadsafe(
                self.send_callback_data(sample),
                self.loop,
            )

        if self.__sample_callback is None:
            self.__sample_callback = gaze_sample_callback

        return self.__sample_callback

    def sample_feeder(self) -> None:
        while self.__sample_thread_running:
            if not self.paused and self.__sample_callback is not None:
                sleep(1 / self.freq)

                self.__sample_callback(
                    {
                        "xL": randrange(0, 100) + 0.2,
                        "yL": randrange(0, 100) + 0.3,
                        "xR": randrange(0, 100) + 0.4,
                        "yR": randrange(0, 100) + 0.6,
                        "timestamp": self.__messages_sent,
                    }
                )

                self.__messages_sent += 1

    async def send_callback_data(self, sample: dict[str, float]):
        await self.data_callback(
            response.response(
                "point",
                {
                    "xL": sample["xL"],
                    "yL": sample["yL"],
                    "xR": sample["xR"],
                    "yR": sample["yR"],
                    "timestamp": sample["timestamp"],
                },
            )
        )
