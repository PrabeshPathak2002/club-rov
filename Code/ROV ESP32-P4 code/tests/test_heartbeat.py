import ctypes
import os
import pathlib
import subprocess

stage = pathlib.Path(__file__).resolve().parent.parent / "main"
output = stage.parent / "build-host"
output.mkdir(exist_ok=True)
lib = output / ('heartbeat_test.dll' if os.name == 'nt' else 'heartbeat_test.so')
subprocess.run(['gcc','-shared','-std=c11','-Wall','-Wextra','-Werror',str(stage/'rov_heartbeat.c'),'-o',str(lib)],check=True)
fn = ctypes.CDLL(str(lib)).rov_heartbeat_disarmed
fn.argtypes = [ctypes.c_void_p,ctypes.c_size_t,ctypes.POINTER(ctypes.c_bool)]
fn.restype = ctypes.c_bool

def crc(data):
    n=65535
    for b in data:
        t = b ^ (n & 255)
        t ^= (t << 4) & 255
        n = ((n >> 8) ^ (t << 8) ^ (t << 3) ^ (t >> 4)) & 65535
    return n.to_bytes(2,'little')

def frame(v2=False, armed=False, signed=False, system=1, component=1, kind=12, autopilot=3):
    payload=bytes([0,0,0,0,kind,autopilot,128 if armed else 0,3,3])
    header=bytes([253,9,int(signed),0,8,system,component,0,0,0]) if v2 else bytes([254,9,8,system,component,0])
    return header+payload+crc(header[1:]+payload+bytes([50]))+(bytes(13) if signed else b'')

count=0
def check(data, valid, disarmed=True):
    global count
    result=ctypes.c_bool(False)
    b=ctypes.create_string_buffer(data)
    assert bool(fn(b,len(data),ctypes.byref(result))) == valid
    if valid: assert result.value == disarmed
    count+=1

for v2 in [False,True]:
    for armed in [False,True]:
        f=frame(v2,armed)
        check(f,True,not armed)
        for end in range(len(f)): check(f[:end],False)
        for i in range(len(f)):
            bad=bytearray(f);bad[i]^=1;check(bytes(bad),False)
    check(frame(v2,system=42),True)
    check(frame(v2,component=2),False)
    check(frame(v2,kind=2),False)
    check(frame(v2,autopilot=8),False)
    check(frame(v2)+b'\0',False)
check(frame(True,signed=True),True)
check(frame(True,armed=True,signed=True),True,False)
print(f'PASS: {count} heartbeat cases (CRC, armed state, v1/v2, malformed frames, controller identity).')
