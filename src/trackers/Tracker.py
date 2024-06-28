from abc import ABC, abstractmethod


class Tracker(ABC):
    _model: str

    @property
    @abstractmethod
    def model(self) -> str:
        pass

    @abstractmethod
    def connect(self) -> None:
        pass

    @abstractmethod
    def start(self) -> None:
        pass

    @abstractmethod
    def stop(self) -> None:
        pass

    @abstractmethod
    def calibrate(self) -> None:
        pass

    @abstractmethod
    def disconnect(self) -> None:
        pass
