import asyncio
import time

class OpenGazeTracker:
    """
    Class for connecting to an OpenGaze tracker and receiving data from it.
    """
    def __init__(self, tcp_host, tcp_port, data_callback):
        self.tcp_host = tcp_host
        self.tcp_port = tcp_port
        self.data_callback = data_callback
        self.reader = None
        self.writer = None
        self.keep_fixation_data = True # TODO: Implement this
        self.base_timestamp = time.time()

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