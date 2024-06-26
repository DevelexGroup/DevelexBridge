import asyncio
import websockets
import json

class WebSocketServer:
    def __init__(self, host, port):
        self.connected = set()
        self.host = host
        self.port = port
        self.server = None

    async def start_server(self):
        self.server = await websockets.serve(self.handler, self.host, self.port)
        print(f'WebSocket server started on {self.host}:{self.port}')

    async def handler(self, websocket: websockets.WebSocketServerProtocol):
        """
        Handler for incoming websocket connections.
        This function will run for each connected client.
        It will wait for the client to disconnect and then unregister the client.

        :param websocket: The websocket connection object.
        """
        self.connected.add(websocket)
        try:
            await websocket.wait_closed()
        finally:
            # Unregister websocket connection
            self.connected.remove(websocket)

    async def send_data(self, data: dict):
        """
        Send data to all connected clients.
        If no clients are connected, it won't send anything. (i.e. it won't wait for clients to connect)

        :param data: The data to send to the clients. It should be a dictionary that can be converted to JSON.
        """
        json_data = json.dumps(data)
        print(f'Sending data to {len(self.connected)} clients')
        await asyncio.gather(*(ws.send(json_data) for ws in self.connected if ws.open))

    async def close(self):
        """
        Close all connected websockets and the server.
        """
        for ws in self.connected:
            await ws.close()
        self.server.close()
        await self.server.wait_closed()