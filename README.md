# DS4Windows

Like those other DS4 tools, but sexier.

DS4Windows is an extract-anywhere program that allows you to get the best
DualShock 4 experience on your PC. By emulating an Xbox 360 controller, many
more games are accessible. Other input controllers are also supported including the
DualSense, Switch Pro, and JoyCon controllers (**first party hardware only**).

![DS4Windows Preview](https://raw.githubusercontent.com/Ryochan7/DS4Windows/jay/ds4winwpf_screen_20200412.png)

## About this fork

This fork is based on [schmaldeo/DS4Windows](https://github.com/schmaldeo/DS4Windows), which itself builds on
the work of Ryochan7 and Jays2Kings. It adds the following on top:

- **HidHide session blacklist** — DS4Windows automatically registers itself with HidHide on
  startup so that other applications (e.g. Steam, OpenRGB) cannot see the raw DS4 device
  while it is in use. The exclusion is cleared automatically when DS4Windows exits, with no
  manual HidHide configuration required.

- **OpenRGB lightbar sync** *(beta)* — DS4Windows runs a lightweight OpenRGB SDK v4 server
  on port 6743. Add it in OpenRGB via **Settings → SDK Client** (host `localhost`, port `6743`).
  Each controller slot appears as an individually controllable gamepad device in OpenRGB,
  allowing lightbar colours to be set from OpenRGB profiles, effects, or tools like Artemis RGB.

Features inherited from schmaldeo's fork:
- Switch [debouncing](https://www.ganssle.com/debouncing.pdf)
- Stick drift correction tool
- Pitch and roll simulation for DS3 based on accelerometer value (credit: [sunnyqeen](https://github.com/sunnyqeen))

## Downloads

- **[Releases](https://github.com/anagnorisis2peripeteia/DS4Windows/releases)**
- The latest stable build is on the `master` branch; the OpenRGB feature is on the `beta` branch (`3.10.0-beta.1`).

## Install

Download the latest release from [releases](https://github.com/anagnorisis2peripeteia/DS4Windows/releases)
and place it in your preferred location.

## Requirements

- Windows 10 or newer
- [Microsoft .NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) — [x64](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x64-installer) or [x86](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x86-installer)
- [Visual C++ 2015–2022 Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) — [x64](https://aka.ms/vs/17/release/vc_redist.x64.exe) or [x86](https://aka.ms/vs/17/release/vc_redist.x86.exe)
- [ViGEmBus](https://vigem.org/) driver (DS4Windows will install it for you)
- [HidHide](https://github.com/nefarius/HidHide/releases) *(recommended)* — required for the automatic session blacklist feature
- **Sony** DualShock 4 or other supported controller
- Connection method:
  - Micro USB cable
  - [Sony Wireless Adapter](https://www.amazon.com/gp/product/B01KYVLKG2)
  - Bluetooth 4.0 (via an adapter or built-in). Only the Microsoft BT stack is supported.
    *Disabling 'Enable output data' in the controller profile settings may help with latency
    issues but will disable lightbar and rumble support.*
- Disable **PlayStation Configuration Support** and **Xbox Configuration Support** in Steam

## License

DS4Windows is licensed under the GNU General Public License version 3.
See [COPYING](COPYING) or [https://www.gnu.org/licenses/gpl-3.0.txt](https://www.gnu.org/licenses/gpl-3.0.txt).
