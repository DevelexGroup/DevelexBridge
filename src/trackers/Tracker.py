from abc import ABC, abstractmethod


class Tracker(ABC):
    @property
    @abstractmethod
    def model(self) -> str:
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
