from abc import ABC, abstractmethod
from enum import Enum


class TrackerState(Enum):
    DISCONNECTED = 0
    CONNECTING = 1
    CONNECTED = 2
    STARTED = 3
    STOPPED = 4


class Tracker(ABC):
    @property
    @abstractmethod
    def model(self) -> str:
        pass

    @property
    @abstractmethod
    def state(self) -> TrackerState:
        pass

    @abstractmethod
    async def connect(self) -> None:
        pass

    @abstractmethod
    async def start(self) -> None:
        pass

    @abstractmethod
    async def stop(self) -> None:
        pass

    @abstractmethod
    async def calibrate(self) -> None:
        pass

    @abstractmethod
    async def disconnect(self) -> None:
        pass
