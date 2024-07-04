import threading
import json
from websockets.sync.client import connect
from time import sleep


def only_sending() -> None:
    with connect("ws://localhost:13892") as websocket:
        websocket.send(json.dumps({"type": "connect", "tracker": "dummy"}))
        print("Sent: connect")

        sleep(0.5)

        websocket.send(json.dumps({"type": "start"}))
        print("Sent: start")

        sleep(3)

        websocket.send(json.dumps({"type": "stop"}))
        print("Sent: stop")

        sleep(0.2)

        websocket.send(json.dumps({"type": "disconnect"}))
        print("Sent: disconnect")


def only_recieve() -> None:
    with connect("ws://localhost:13892") as websocket:
        while True:
            try:
                message = websocket.recv()
                print(f"Received: {str(message)}")
            except Exception:
                print("Connection closed")
                break


t1 = threading.Thread(target=only_sending)
t2 = threading.Thread(target=only_recieve)

t1.start()
t2.start()

t1.join()
t2.join()
