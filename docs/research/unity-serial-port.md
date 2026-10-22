# Can Unity read a USB serial port?

Ticket: [#3](https://github.com/garrett-reynolds/hockey-game-test/issues/3). Researched 2026-10-08.

**Question:** How does a Unity 6 game on Windows read a COM port at 115200 baud and receive one
message per shot, such as `(20,60,90)`?

## Short answer

Yes. Use `System.IO.Ports.SerialPort` (Unity's Mono class library) in a Windows Standalone build.
Set **Api Compatibility Level = .NET Framework** (enum `NET_Unity_4_8`). Run a blocking
`ReadLine()` loop on a dedicated background thread with a `ReadTimeout`. Pass parsed shots to the
main thread through a thread-safe queue that `Update()` drains. Do not use the `DataReceived`
event: Unity's Mono never raises it. Use `SerialPort.GetPortNames()` to fill a port picker.
If you want a ready-made package, Ardity does exactly this.

## 1. Api Compatibility Level and player settings

- Unity 6 has two Api Compatibility Levels. **.NET Standard 2.1** is the default. **.NET Framework**
  is ".NET Framework 4.8 ... plus additional APIs in .NET Standard 2.1". It is set in
  *Edit > Project Settings > Player > Other Settings > Configuration > Api Compatibility Level*,
  or from code with `PlayerSettings.SetApiCompatibilityLevel`.
  Source: https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-support.html
- The scripting enum calls these `NET_Standard` ("targets .NET Standard 2.1") and `NET_Unity_4_8`
  (".NET Framework 4.8 + Unity additions").
  Source: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ApiCompatibilityLevel.html
- `SerialPort` is **not part of .NET Standard 2.1**. Microsoft's API browser lists it only for
  .NET Framework 2.0–4.8.1 and the separate platform-extension package (`netstandard-2.0-pp`,
  `net-x-pp`). The "pp" monikers mean a separately shipped package, not the standard surface.
  Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport
- **Required setting: .NET Framework.** Under the default .NET Standard 2.1 profile, expect
  `CS0234: 'Ports' does not exist in the namespace 'System.IO'`.
  This is *inferred* from the two sources above. I found no Unity page that states it directly.
  Ardity, the most widely used Unity serial package, makes the same call: its editor script forces
  `ApiCompatibilityLevel.NET_Unity_4_8` for Standalone on Unity 2019 and later.
  Source: https://github.com/dwilches/Ardity/blob/master/UnityProject/Assets/Ardity/Scripts/Editor/ApiLevelChecker.cs
- **Platform:** Windows/Mac/Linux Standalone and the Editor. It does not work on Android or iOS:
  Unity issue 937818 ("DllNotFoundException MonoPosixHelper ... SerialPort constructor on mobile")
  was closed as Won't Fix. This doesn't matter for a Windows PC build.
  Source: https://issuetracker.unity3d.com/issues/system-dot-dllnotfoundexception-monoposixhelper-is-thrown-when-calling-system-dot-io-dot-ports-dot-serialport-constructor-on-mobile-devices
- **Scripting backend:** *Unverified.* I found no primary source on whether `SerialPort` works under
  IL2CPP as well as Mono. Use the Mono backend for the PC build unless we test IL2CPP on the
  bench.

## 2. Threading: yes, read on a background thread

- **`DataReceived` never fires in Unity.** In Unity's own Mono fork (branch `unity-main`),
  `SerialPort.OnDataReceived` is defined but nothing calls it. I checked `SerialPort.cs`,
  `WinSerialStream.cs`, and `SerialPortStream.cs`, and none of them call it. Upstream Mono has the
  same gap. Forum reports match this ("the DataReceived event does not work in Unity's Mono").
  Sources:
  https://github.com/Unity-Technologies/mono/blob/unity-main/mcs/class/System/System.IO.Ports/SerialPort.cs (lines ~762, ~794),
  https://discussions.unity.com/t/how-to-save-serial-port-data-to-a-variable/149308
- **`ReadLine()` blocks until a full line arrives.** In Unity's Mono, `ReadTimeout` defaults to
  `InfiniteTimeout` (-1), so `ReadLine()` in `Update()` freezes the game between shots.
  Source: Unity Mono `SerialPort.cs` lines 33, 54.
- **Most Unity APIs are main-thread-only.** In release builds Unity does not check for
  off-thread calls, so the result is "random crashes and other unpredictable errors". Unity also
  says background code is not stopped automatically when you exit Play mode.
  Source: https://docs.unity3d.com/Manual/async-awaitable-continuations.html
- **Recommended pattern.** Microsoft's own `SerialPort` sample uses this pattern too: a dedicated
  `Thread` loops on `ReadLine()` with `ReadTimeout = 500` and catches `TimeoutException`.
  Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport (Examples)
  1. In `OnEnable`/`Start`, open the port and start a `Thread` with `IsBackground = true`.
  2. The thread loops while `running`. Each pass calls `ReadLine()`, catches `TimeoutException`,
     parses `(x,y,speed)`, and does `ConcurrentQueue<Shot>.Enqueue(...)`.
  3. `Update()` drains the queue with `TryDequeue` and raises game events on the main thread.
  4. In `OnDisable`/`OnApplicationQuit`, set `running = false`, call `Close()`, then `Join()`.
     Microsoft: "If a SerialPort object becomes blocked during a read operation, do not abort the
     thread. Instead, either close the base stream or dispose of the SerialPort object."
     This step is essential in the Editor. Otherwise the port stays open after Play mode ends, and
     the next Play cannot open it.
  Ardity uses the same design: a worker thread, `Queue.Synchronized` input/output queues, and
  reconnect-on-exception with a sleep.
  Source: https://github.com/dwilches/Ardity/blob/master/UnityProject/Assets/Ardity/Scripts/Runtime/Threads/AbstractSerialThread.cs

### Line ending (not yet confirmed by electronics)

- In Unity's Mono, `SerialPort.NewLine` defaults to `Environment.NewLine`, which is `"\r\n"` on
  Windows. `ReadLine()` is `ReadTo(new_line)`.
  Source: Unity Mono `SerialPort.cs` lines 52, 665–667.
  Microsoft's .NET docs give `"\n"` as the default. The Unity Mono value differs on Windows, so
  **set `NewLine` explicitly**.
- Arduino `Serial.println()` sends `"\r\n"`. *Unverified for this firmware: confirm what the tracking
  electronics send.* The robust choice is `NewLine = "\n"` plus `Trim()` on each line, which
  handles both `\n` and `\r\n`. Alternatively, parse on the `)` terminator.

## 3. Listing COM ports for a picker

- `SerialPort.GetPortNames()` returns `string[]` such as `COM3`. On Windows, Unity's Mono reads
  the value data under `HKLM\HARDWARE\DEVICEMAP\SERIALCOMM` and returns an empty array if the
  key is missing.
  Source: Unity Mono `SerialPort.cs` line 548.
- Microsoft says the order "is not specified". It also warns that stale registry data gives
  incorrect results. Sort the list, and re-query when the player opens the picker, because USB
  devices come and go.
  Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.getportnames
- The list contains names only, no "USB Serial Device" friendly names. Getting friendly names
  needs WMI/SetupAPI, which is extra work and was not researched here. A practical workaround is
  to save the last-used port in `PlayerPrefs`.
- COM10 and above: Unity's `WinSerialStream` prefixes `\\.\` automatically, so plain `"COM12"`
  works. Source: Unity Mono `WinSerialStream.cs` lines 79–80.

## 4. Known problems

- Missing namespace or CS0234 means the Api Compatibility Level is wrong. See section 1.
- `DataReceived` is silent. See section 2.
- `BytesToRead` null-reference reports exist from Unity 5.5 (2016–17). *Unverified whether
  this still applies in Unity 6.* `ReadLine()` on a thread does not use it.
  Source: https://issuetracker.unity3d.com/issues/serialport-bytestoread-returns-null-reference
- Only one process can hold a COM port. Close the Arduino IDE Serial Monitor before you press
  Play.
- Some boards (for example, native-USB Arduinos) only send data once DTR is asserted. If nothing
  arrives, try `DtrEnable = true`. *Unverified for our board.*

## 5. Plugin option

- **Ardity** (https://github.com/dwilches/Ardity). Free. Last push 2026-02-16, not archived.
  It provides a `SerialController` prefab, a background thread with queues, auto-reconnect, and
  a custom-delimiter mode. Licensed CC BY 2.0 according to the README. It wraps the same
  `System.IO.Ports` and needs the same `.NET Framework` setting, so it removes the threading
  boilerplate but changes nothing about the underlying stack.
- I did not evaluate paid Asset Store serial plugins. For a single line-based 115200 stream,
  about 60 lines of our own code or Ardity are both enough.

## Open items

- Confirm the firmware's line terminator: `\n` or `\r\n`.
- Bench-test with the real board in the Editor and in a Windows Standalone build (Mono backend).
  Success = shots appear in the console with no frame hitches, and Play can be stopped and
  started again without a "port in use" error.
