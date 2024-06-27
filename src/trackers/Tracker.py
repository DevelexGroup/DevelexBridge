from abc import ABC, abstractmethod


class Tracker(ABC):
    @abstractmethod
    def connect(self):
        pass

    @abstractmethod
    def start(self):
        pass

    @abstractmethod
    def stop(self):
        pass

    @abstractmethod
    def calibrate(self):
        pass

    @abstractmethod
    def disconnect(self):
        pass
