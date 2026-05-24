# A1 Screen Shade

A1 Screen Shade is a small Windows tray app that can place a black overlay across every connected screen and lower display brightness to the minimum supported level. Click anywhere on the overlay to exit shade mode and restore the previous brightness.

## Usage

- Start the app from `ScreenShade.App`.
- Press `Ctrl+Alt+B` to toggle shade mode.
- Press `Ctrl+Alt+T` to open the quick timed shade menu, enter a delay, and start the countdown.
- Double-click the tray icon to open the management page.
- Choose which displays should be covered.
- Set `延时启动(秒)` if the overlay should start after a countdown.
- Change both hotkeys in the management page if the defaults conflict with another app.
- Or use the tray icon menu and choose `启动遮罩`.
- Click anywhere on the black screen or press any key to leave shade mode.
- Enable `鼠标移动时退出遮罩` in the management page if you also want cursor movement to dismiss the overlay.

Brightness control is best effort: Windows exposes brightness controls differently for internal and external monitors. The overlay always appears even if a monitor refuses hardware brightness changes.

## Install

Release packages include a single EXE, a portable ZIP, and an MSI installer. The MSI installer creates Start Menu and desktop shortcuts.
