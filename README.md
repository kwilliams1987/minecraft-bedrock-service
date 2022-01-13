# Minecraft Bedrock Windows Service Wrapper

This project allows the `bedrock_server.exe` file to run safely as a background service on Windows systems.

## Features

- Safe handling of Shutdown and Stop conditions by passing the `stop` command to the service and waiting for it to quit.
- Automatically call `whitelist reload` when `whitelist.json` is modified.
- Automatically call `permission reload` when `permissions.json` is modified.
- Automatically restart the server when `server.properties` is modified.
- Automatically restart the server if it is unexpectedly killed.
- Shutdown and restart include 30 sec (15 on Windows Shutdown) grace periods with server announcements.
  - System can detect if players are online and skips grace period if there are none.
- Server output is saved to disk as `bedrock_service.log`.
- `bedrock_service.exe` can also be run as a console application for troubleshooting purposes.
- Automatic server backups with configurable schedule.
  - Backup interval starts at end of last backup.

## Usage

1. Place `bedrock_service.exe` in a directory (doesn't need to be the same place as `bedrock_server.exe`).
2. Start `bedrock_service.exe` for testing.
   If `bedrock_service.exe` is not in the same directory as `bedrock_server.exe` pass the `--workingDirectory "directory\containing\bedrock_server"` parameter.
3. Exit test mode with `CTRL + X`.
4. Trigger a backup with `CTRL + B`, cancel a running backup with `CTRL + N`.
5. Create a new Windows Service entry:

   `.\sc.exe create MinecraftBedrockServer binPath= "path\to\bedrock_service.exe --workingDirectory \"directory\containing\bedrock_server\"" start= delayed-auto DispayName= "Minecraft Bedrock Dedicated Server" `

## Parameters

| Parameter	           | Default Value         | Description                                                        |
|----------------------|-----------------------|--------------------------------------------------------------------|
| `--workingDirectory` | Current Directory     | Directory of server code, logs and backups.                        |
| `--executable`       | `bedrock_server.exe`  | Filename of server program.	                                    |
| `--logFileName`      | `bedrock_service.log` | Filename of log output.                                            |
| `--backupInterval`   | `30`                  | Number of minutes between backups, `0` = disabled.                 |
| `--backupDirectory`  | `Backups`             | Place to store the backup files, as a subdirectory of `workingDir` |