import asyncio
import time

class OpenGazeTracker:
    """
    Class for connecting to an OpenGaze tracker and receiving data from it.

    :param keep_fixation_data: A boolean indicating whether to keep fixation data or not. If this is False, fixation data is not sent to the client. This is useful for reducing the amount of data sent to the client.
    :param tcp_host: The host of the tracker. Should be localhost in most cases.
    :param tcp_port: The port of the tracker.
    :param data_callback: The callback function to call when data or messages are received from the tracker. These are re-sent to the client via the WebSocket server. It is either gaze point data or calibration data or confirmation that the tracker is connected. The callback function should take a single argument, which is a dictionary with the data or message to send to the client. The data structure in case of a gaze point is as follows:
        {
            'x': The x-coordinate of the gaze point.
            'y': The y-coordinate of the gaze point.
            'timestamp': The timestamp of the data in seconds.
            'deviceValidity': A boolean indicating whether the data is valid or not. If the data is a calibration point, this is always True.
            'type': The type of the data. This is either 'point' for gaze point data or 'calibration' for calibration data.
            'fixationId': The ID of the fixation. This is only present if the data is a fixation.
            'fixationDuration': The duration of the fixation in seconds. This is only present if the data is a fixation.
        }
    :param reader: The reader object for reading data from the tracker. (asyncio.StreamReader)
    :param writer: The writer object for writing data to the tracker. (asyncio.StreamWriter)
    """
    def __init__(self, keep_fixation_data, tcp_host, tcp_port, data_callback):
        self.tcp_host = tcp_host
        self.tcp_port = tcp_port
        self.data_callback = data_callback
        self.reader = None
        self.writer = None
        self.keep_fixation_data = keep_fixation_data

    async def connect(self):
        self.reader, self.writer = await asyncio.open_connection(self.tcp_host, self.tcp_port)
        print('Connected to TCP server')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_POG_FIX" STATE="1" />\r\n')
        # await self.send_to_tracker('<SET ID="ENABLE_SEND_TIME" STATE="1" />\r\n')
        await self.send_to_tracker('<SET ID="ENABLE_SEND_DATA" STATE="1" />\r\n')
        print('Sent commands to tracker')
        while True:
            data = await self.reader.read(1024)
            if not data:
                break
            print(f"Received: {data.decode()!r}")
            self.decode_data(data)


    async def send_to_tracker(self, command):
        self.writer.write(command.encode())
        await self.writer.drain()

    async def disconnect(self):
        self.writer.close()
        await self.writer.wait_closed()

    def decode_data(self, data: bytes):
        """
        Decode the data received from the tracker.
        This function should be overridden in subclasses.

        Parse OpenGaze API XML data and return a dictionary with the relevant data.
        After parsing to the text, they look like this:
        <REC FPOGX="0.29043" FPOGY="0.65331" FPOGS="23457.68164" FPOGD="0.19727" FPOGID="12072" FPOGV="1" />\r\n

        :param data: The data received from the tracker.
        """
        raw_data = data.decode()
        # Split by newline and remove empty strings (sometimes there are two newlines in a row)
        lines = [line for line in raw_data.split('\n') if line]
        data_dict = {}

        for line in lines:
            if line.startswith('<REC'):
                # Split by space and remove empty strings
                parts = [part for part in line.split(' ') if part]

                # If empty, skip
                if not parts:
                    continue

                # Remove <REC and /> from first and last parts
                parts.pop(0)
                parts.pop(-1)

                for part in parts:
                    # Split by = and remove empty strings
                    key, value = [x for x in part.split('=') if x]
                    # Remove quotes from value
                    value = value.strip('"')
                    data_dict[key] = value

                parsed_data = self.parse_rec(data_dict)
                print(parsed_data)
                self.data_callback(parsed_data)

    def parse_rec(self, data: dict) -> dict:
        """
        Parse the data dictionary of "rec" and return a dictionary with the relevant data structure
        for transfer to the client.

        :param data: The data dictionary received from the tracker.
        """

        # timestamp as int, provisionally from time.time()
        # TODO: Implement timestamp from tracker, but that will be difficult, CPU ticks?
        timestamp = time.time()

        base_data = {
            'x': data.get('FPOGX'),
            'y': data.get('FPOGY'),
            'timestamp': timestamp,
            'deviceValidity': True, # TODO: Implement this via Best POG, BPOGV
            'type': 'point',
        }

        # if is fixation via inbuilt fixation filter data
        if data.get('FPOGV') == '1':
            base_data['fixationId'] = data.get('FPOGID')
            base_data['fixationDuration'] = float(data.get('FPOGD'))

        return base_data