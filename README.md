# ClipTypr

![example](https://github.com/user-attachments/assets/b745d66c-4894-4065-bcaa-fc933328e35b)

# Usage
If the program is running you will find that icon in the TrayBar. _(The mini icons in the TaskBar)_

![icon](https://github.com/user-attachments/assets/0fb4fa4b-b9e1-4654-96f3-8d2dfd47d564)

If you click the icon this context menu will open.

![trayicon](https://github.com/user-attachments/assets/3d4790dc-d1c6-49f9-9f98-07fab7c9058c)

- Write Text from clipboard
  - This will start a 3 second timer (Timer can be changed in the config). After the timer is over it will send the whole text from the clipboard into the focused window.
- Write File from clipboard
  - This will start the same timer as above. After the timer is over it will send the file as text into the focused window.
  - **After transferring the file should be saved as .ps1 and executed with Powershell.**
- Show Logs
  - This will open a Console window that shows all logs. To close this window 'Show Logs' has to be clicked again.
- Edit Configuration
  - This opens the configuration file of the app, that can be customized at runtime.
- Run As Admin
  - This will restart the application with Administrator privileges.
- Autostart
  - If this is ticked the program will start automatically on startup. Clicking on this will add or remove it from the Autostart.
- ClipTypr - Version X.X.X
  - The current version number, you are running.
- Exit
  - This will completely close the application.
  - Works like Task Manager -> Kill.
