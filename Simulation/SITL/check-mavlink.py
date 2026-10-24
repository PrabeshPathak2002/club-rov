"""Exercise only the isolated ArduSub SYSID 42 simulation; always disarm on exit."""
import json
import time
from pymavlink import mavutil

link = mavutil.mavlink_connection("udpin:127.0.0.1:14551", source_system=255)
heartbeat = link.wait_heartbeat(timeout=15)
if heartbeat is None or heartbeat.get_srcSystem() != 42 or heartbeat.type != mavutil.mavlink.MAV_TYPE_SUBMARINE:
    raise RuntimeError("Expected only SYSID 42 submarine simulator")
link.target_system = 42
link.target_component = 1
result = {"system": 42, "messages": {}, "phases": []}


def run(seconds, x=0, y=0, z=500, r=0):
    end = time.monotonic() + seconds
    outputs = None
    while time.monotonic() < end:
        link.mav.heartbeat_send(mavutil.mavlink.MAV_TYPE_GCS, mavutil.mavlink.MAV_AUTOPILOT_INVALID, 0, 0, 0)
        link.mav.manual_control_send(42, x, y, z, r, 0)
        until = time.monotonic() + 0.05
        while time.monotonic() < until:
            message = link.recv_match(blocking=True, timeout=0.02)
            if message and message.get_srcSystem() == 42:
                kind = message.get_type()
                if kind in ("ATTITUDE", "LOCAL_POSITION_NED", "SCALED_PRESSURE", "SERVO_OUTPUT_RAW", "STATUSTEXT", "AUTOPILOT_VERSION", "COMMAND_ACK"):
                    result["messages"][kind] = message.to_dict()
                if kind == "SERVO_OUTPUT_RAW":
                    outputs = [getattr(message, f"servo{i}_raw") for i in range(1, 7)]
    return outputs


try:
    link.mav.request_data_stream_send(42, 1, mavutil.mavlink.MAV_DATA_STREAM_ALL, 10, 1)
    link.mav.command_long_send(42, 1, mavutil.mavlink.MAV_CMD_REQUEST_MESSAGE, 0, 148, 0, 0, 0, 0, 0, 0)
    run(3)
    version = result["messages"].get("AUTOPILOT_VERSION", {}).get("flight_sw_version", 0)
    if version >> 8 != 0x040702:
        raise RuntimeError("Expected ArduSub 4.7.2")
    link.mav.param_request_read_send(42, 1, b"FRAME_CONFIG", -1)
    frame = link.recv_match(type="PARAM_VALUE", blocking=True, timeout=3)
    if frame is None or frame.param_id != "FRAME_CONFIG" or frame.param_value != 1:
        raise RuntimeError("Expected six-motor VECTORED frame (FRAME_CONFIG=1)")
    link.set_mode("MANUAL")
    run(1)
    link.arducopter_arm()
    run(2)
    hb = link.recv_match(type="HEARTBEAT", blocking=True, timeout=3)
    if hb is None or not hb.base_mode & mavutil.mavlink.MAV_MODE_FLAG_SAFETY_ARMED:
        raise RuntimeError("Simulator did not arm: " + str(result["messages"].get("STATUSTEXT")))
    for name, command in (("neutral", {}), ("forward", {"x": 400}), ("neutral", {}),
                          ("starboard", {"y": 400}), ("neutral", {}), ("yaw right", {"r": 300}),
                          ("neutral", {}), ("up", {"z": 650})):
        outputs = run(2, **command)
        expected = {
            "neutral": [1500] * 6,
            "forward": [1420, 1420, 1580, 1580, 1500, 1500],
            "starboard": [1580, 1420, 1580, 1420, 1500, 1500],
            "yaw right": [1560, 1440, 1440, 1560, 1500, 1500],
            "up": [1500, 1500, 1500, 1500, 1440, 1440],
        }[name]
        if outputs != expected:
            raise RuntimeError(f"Unexpected {name} motor outputs: {outputs}")
        result["phases"].append({"name": name, "outputs": outputs})
        print(name, outputs, flush=True)
finally:
    link.arducopter_disarm()
    run(1)
    result["disarmed_outputs"] = result["messages"].get("SERVO_OUTPUT_RAW")
    with open("mavlink-check.json", "w") as report:
        json.dump(result, report, indent=2)
    link.close()
print("MAVLink control check completed, simulator disarmed", flush=True)
