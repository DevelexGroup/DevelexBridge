import asyncio
import threading
import time
from typing import Callable, Any, Coroutine, Optional
from trackers.Tracker import Tracker, TrackerState
from websocketserver.WebSocketServer import WebSocketServer
import response


class OpenGazeTracker(Tracker):
    """
    Class for connecting to an OpenGaze tracker and receiving data from it.

    :param keep_fixation_data: A boolean indicating whether to keep fixation data or not. If this is False, fixation data is not sent to the client. This is useful for reducing the amount of data sent to the client.
    :param tcp_host: The host of the tracker. Should be localhost in most cases.
    :param tcp_port: The port of the tracker.
    :param data_callback: The callback function to call when data or messages are received from the tracker. These are re-sent to the client via the WebSocket server. It is either gaze point data or calibration data or confirmation that the tracker is connected. The callback function should take a single argument, which is a dictionary with the data or message to send to the client. The data structure in case of a gaze point is as follows:
        {
            'xL': The x-coordinate of the gaze point. Left eye.
            'yL': The y-coordinate of the gaze point. Left eye.
            'validityL': A boolean indicating whether the data is valid or not. Left eye.
            'xR': The x-coordinate of the gaze point. Right eye.
            'yR': The y-coordinate of the gaze point. Right eye.
            'validityR': A boolean indicating whether the data is valid or not. Right eye.
            'timestamp': The timestamp of the data in seconds.
            'type': 'point'
            'fixationId': The ID of the fixation. This is only present if the data is a fixation. (!!!)
            'fixationDuration': The duration of the fixation in seconds. This is only present if the data is a fixation. (!!!)
        }
    :param reader: The reader object for reading data from the tracker. (asyncio.StreamReader)
    :param writer: The writer object for writing data to the tracker. (asyncio.StreamWriter)
    """

    __state: TrackerState = TrackerState.DISCONNECTED
    __model: str = "opengaze"
    __reader_thread: Optional[threading.Thread] = None
    reader: Optional[asyncio.StreamReader]
    writer: Optional[asyncio.StreamWriter]

    def __init__(
        self,
        keep_fixation_data: bool,
        tcp_host: str,
        tcp_port: int,
        data_callback: Callable[[Any], Coroutine[Any, Any, None]],
        loop: asyncio.AbstractEventLoop,
    ):
        self._stop_thread = threading.Event()
        self.tcp_host = tcp_host
        self.tcp_port = tcp_port
        self.data_callback = data_callback
        self.reader = None
        self.writer = None
        self.keep_fixation_data = keep_fixation_data
        self.loop = loop
        self.__reader_thread = threading.Thread(target=self.recieve_tracker_data_thread)
        self.__reader_thread.daemon = True
        self.__reader_thread.start()

    def __del__(self):
        if self.__reader_thread is not None:
            self.stop_thread()
            self.__reader_thread.join()

    def stop_thread(self):
        self._stop_thread.set()

    def stopped(self):
        return self._stop_thread.is_set()

    async def connect(self) -> None:
        self.__state = TrackerState.CONNECTING

        try:
            self.reader, self.writer = await asyncio.open_connection(
                self.tcp_host, self.tcp_port
            )

            self.__state = TrackerState.CONNECTED
        except ConnectionRefusedError:
            self.__state = TrackerState.DISCONNECTED
            await self.data_callback(response.error_response("Connection refused"))
            return

        print("Connected to TCP server")

        # send confirmation message to the client
        await self.data_callback(response.response("connected"))

    async def start(self) -> None:
        await self.send_to_tracker('<SET ID="ENABLE_SEND_POG_FIX" STATE="1" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_POG_LEFT" STATE="1" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_POG_RIGHT" STATE="1" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_PUPIL_LEFT" STATE="1" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_PUPIL_RIGHT" STATE="1" />\r\n')
        # await self.send_to_tracker('<SET ID="ENABLE_SEND_TIME" STATE="1" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_DATA" STATE="1" />\r\n')
        await self.data_callback(response.response("started"))
        self.__state = TrackerState.STARTED

    def recieve_tracker_data_thread(self):
        while not self.stopped():
            if self.reader is not None and self.__state == TrackerState.STARTED:
                try:
                    future_data = asyncio.run_coroutine_threadsafe(
                        self.reader.read(1024), self.loop
                    )

                    try:
                        data = future_data.result(timeout=1.0)
                    except Exception as e:
                        continue

                    if not data:
                        continue

                    print(f"Received: {data.decode()!r}")
                    asyncio.run_coroutine_threadsafe(self.decode_data(data), self.loop)
                except Exception as e:
                    continue

    async def stop(self) -> None:
        await self.send_to_tracker('<SET ID="ENABLE_SEND_POG_FIX" STATE="0" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_POG_LEFT" STATE="0" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_POG_RIGHT" STATE="0" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_PUPIL_LEFT" STATE="0" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_PUPIL_RIGHT" STATE="0" />\r\n')
        # await self.send_to_tracker('<SET ID="ENABLE_SEND_TIME" STATE="0" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_DATA" STATE="0" />\r\n')
        self.__state = TrackerState.STOPPED
        await self.data_callback(response.response("stopped"))

    async def calibrate(self) -> None:
        """
        Warning! Open Gaze API documentation incorrectly states that the calibration is
        started by sending <SET ID="CALIBRATE_START" VALUE="1" />. This is incorrect.
        Parameters are STATE, not VALUE. The correct command is <SET ID="CALIBRATE_START" STATE="1" />.
        """
        if self.reader is None:
            return

        await self.send_to_tracker('<SET ID="CALIBRATE_SHOW" STATE="1" />\r\n')
        await self.send_to_tracker('<SET ID="CALIBRATE_START" STATE="1" />\r\n')

        """
        Listen to calibration data until receiving
        <CAL ID="CALIB_RESULT" ... />
        """
        while True:
            data = await self.reader.read(1024)

            if not data:
                break

            print(f"Received: {data.decode()!r}")

            if b'<CAL ID="CALIB_RESULT"' in data:
                await self.decode_data(data)
                break

        await self.send_to_tracker('<SET ID="CALIBRATE_SHOW" STATE="0" />\r\n')
        await self.send_to_tracker('<SET ID="CALIBRATE_START" STATE="0" />\r\n')
        await self.data_callback(response.response("calibrated"))

    async def send_to_tracker(self, command: str) -> None:
        if self.writer is None:
            return

        self.writer.write(command.encode())
        await self.writer.drain()

    async def disconnect(self) -> None:
        if self.writer is None:
            return

        if self.__state == TrackerState.STARTED:
            await self.stop()

        self.writer.close()
        await self.writer.wait_closed()
        self.__state = TrackerState.DISCONNECTED

        if self.__reader_thread is not None:
            self.stop_thread()
            self.__reader_thread.join()

        await self.data_callback(response.response("disconnected"))

    async def decode_data(self, data: bytes) -> None:
        """
        Decode the data received from the tracker.
        This function should be overridden in subclasses.

        Parse OpenGaze API XML data and return a dictionary with the relevant data.
        After parsing to the text, they look like this:
        '<REC FPOGX="0.00000" FPOGY="0.00000" FPOGS="0.00000" FPOGD="0.00000" FPOGID="0" FPOGV="0" LPOGX="0.00000" LPOGY="0.00000" LPOGV="0" RPOGX="0.00000" RPOGY="0.00000" RPOGV="0" />\r\n'

        :param data: The data received from the tracker.
        """
        raw_data = data.decode()
        # Split by newline and remove empty strings (sometimes there are two newlines in a row)
        lines = [line for line in raw_data.split("\n") if line]
        data_dict = {}

        for line in lines:
            if line.startswith("<REC"):
                # Split by space and remove empty strings
                parts = [part for part in line.split(" ") if part]

                # If empty, skip
                if len(parts) < 2:
                    print(f"Skipping invalid or empty line: {line}")
                    continue

                try:
                    # Remove <REC and /> from first and last parts
                    if parts[0].startswith("<REC"):
                        parts.pop(0)
                    if parts[-1].endswith("/>"):
                        parts.pop(-1)

                    for part in parts:
                        # Split by = and remove empty strings
                        key_value = [x for x in part.split("=") if x]
                        if len(key_value) != 2:
                            print(f"Skipping invalid part: {part}")
                            continue
                        key, value = key_value
                        # Remove quotes from value
                        value = value.strip('"')
                        data_dict[key] = value

                    parsed_data = self.parse_rec(data_dict)
                    print(parsed_data)
                    await self.data_callback(parsed_data)
                except IndexError as e:
                    print(f"IndexError: {e} - Line: {line} - Parts: {parts}")
                except Exception as e:
                    print(f"Unexpected error in decode_data: {e}")

    def parse_rec(self, data: dict[Any, Any]) -> dict[str, Any]:
        """
        Parse the data dictionary of "rec" and return a dictionary with the relevant data structure
        for transfer to the client.

        :param data: The data dictionary received from the tracker.
        """

        # timestamp as int, provisionally from time.time()
        # TODO: Implement timestamp from tracker, but that will be difficult, CPU ticks?
        timestamp = (
            time.time() * 1000
        )  # in milliseconds to match the client side timestamp

        base_data = {
            "xL": data.get("LPOGX"),
            "yL": data.get("LPOGY"),
            "validityL": data.get("LPOGV"),
            "xR": data.get("RPOGX"),
            "yR": data.get("RPOGY"),
            "validityR": data.get("RPOGV"),
            "pupilDiameterL": data.get("LPD"),
            "pupilDiameterR": data.get("RPD"),
            "timestamp": timestamp,
            "type": "point",
        }

        # if is fixation via inbuilt fixation filter data
        if data.get("FPOGV") == "1":
            base_data["fixationId"] = data.get("FPOGID")
            base_data["fixationDuration"] = float(str(data.get("FPOGD")))

        return base_data

    @property
    def model(self) -> str:
        return self.__model

    @property
    def state(self) -> TrackerState:
        return self.__state
