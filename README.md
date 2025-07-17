# ClipTypr

![example](https://github.com/user-attachments/assets/b745d66c-4894-4065-bcaa-fc933328e35b)

## Important Note
This program will likely be flagged as Virus, due to the native Windows functions used to provide these functionalities.
- If you really don't trust me, check the source code and/or compile it yourself :)

#### Download the lastest version: https://github.com/BlyZeDev/ClipTypr/releases/latest

# Usage
On program startup this icon will appear in the Notification Icons area. *These little icons in the bottom right corner on the Taskbar*

![icon](https://github.com/BlyZeDev/ClipTypr/blob/master/ClipTypr/icon.ico)

If you click the icon this context menu will open.

![traymenu](https://github.com/user-attachments/assets/6f318a94-f2c7-487c-b4e9-cf0db0c08592)

## Write from Clipboard
- Write Text from Clipboard
  - Starts a timer. After the timer elapsed the text from the clipboard is typed into the focused window.

#### Please open a Text Editor like Notepad++[^notepad++] or the Windows Notepad and make sure to focus it, while the timer is running.

- Write Image from Clipboard
  - Starts a timer. After the timer elapsed the image from the clipboard is typed as text into the focused window.
  - **After transferring, the image has to be saved as .ps1 and executed with Powershell.**
- Write File from Clipboard
  - Starts a timer. After the timer elapsed the file from the clipboard is typed as text into the focused window.
  - **After transferring, the file has to be saved as .ps1 and executed with Powershell.**

## Clipboard Store
- Add Entry
  - Adds the current content from your clipboard as entry.
  - The maximum amount of entries currently is **10**.
- Entry - *Preview of the clipboard content*
  - Contains a saved clipboard entry.
  - Clicking on it will override your current clipboard with the content of this entry.
- Clear Entries
  - Clears all currently saved entries.

## Show Logs
- Opens a Console window that displays log messages. To close this window **Show Logs** has to be unticked.
- Don't try to close the window itself. It is only closable by unticking **Show Logs** or exiting the application

## Settings
- Open Application Folder
  - Opens the application folder.
  - Here you will find the Plugins folder and the appsettings.
- Edit Configuration
  - Opens the configuration file of the app.
  - These settings can be customized at runtime.
- Run As Admin
  - Restarts the application with Administrator privileges.
- Autostart
  - Puts the application into the Startup menu if ticked.
  - If unticked the program is removed.

## ClipTypr - Version X.X.X
  - The current version number, you are running.

## Exit
  - Completely closes the application.
  - Equivalent to Task Manager -> Kill.

[^notepad++]: https://notepad-plus-plus.org/downloads/
