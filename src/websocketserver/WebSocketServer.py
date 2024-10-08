import asyncio
import websockets
import json
from typing import Optional, Set, Any, Callable, Awaitable


class WebSocketServer:
    connected: Set[websockets.WebSocketServerProtocol]
    host: str
    port: int
    server: Optional[websockets.WebSocketServer]

    def __init__(
        self,
        host: str,
        port: int,
        received_data_callback: Callable[
            [dict[Any, Any], "WebSocketServer"], Awaitable[None]
        ],
    ):
        self.connected = set()
        self.host = host
        self.port = port
        self.server = None
        self.received_data_callback = received_data_callback  # Callback function to call when data is received from the client. It should take two arguments: the data received (dict) and the WebSocketServer object.

    async def start_server(self) -> None:
        self.server = await websockets.serve(self.handler, self.host, self.port)

        print(f"[DEVELEX] WebSocket server started on {self.host}:{self.port}")

    async def handler(self, websocket: websockets.WebSocketServerProtocol) -> None:
        self.connected.add(websocket)

        try:
            await self.process_incoming_messages(websocket)
        finally:
            # Unregister websocket connection
            self.connected.remove(websocket)

    async def process_incoming_messages(
        self, websocket: websockets.WebSocketServerProtocol
    ) -> None:
        while True:
            try:
                data = await websocket.recv()

                try:
                    json_data = json.loads(data)
                    if isinstance(json_data, dict):
                        await self.received_data_callback(json_data, self)

                        if "type" in json_data and json_data["type"] == "disconnect":
                            self.connected.remove(websocket)
                    else:
                        print(
                            f"[DEVELEX] Received invalid data from client: {str(data)}"
                        )
                except json.JSONDecodeError:
                    print(f"[DEVELEX] Received invalid data from client: {str(data)}")
            except websockets.ConnectionClosed:
                break

    async def send_data(self, data: dict[Any, Any]) -> None:
        json_data = json.dumps(data)

        print(f"[DEVELEX] Sending data ({json_data}) to {len(self.connected)} clients")

        await asyncio.gather(*(ws.send(json_data) for ws in self.connected if ws.open))

    async def close(self) -> None:
        for ws in self.connected:
            await ws.close()

        if self.server is None:
            return

        self.server.close()
        await self.server.wait_closed()
