# Minecraft Bedrock Windows Service Wrapper

This project allows the `bedrock_server.exe` file to run safely as a background service on Windows systems.

## Features

- Safe handling of Shutdown and Stop conditions by passing the`stop` command to the service and waiting for it to quit.
- Automatically call `whitelist reload` when `whitelist.json` is modified.
- Automatically call `permission reload` when `permissions.json` is modified.
- Automatically restart the server when `server.properties` is modified.
- Automatically restart the server if it is unexpectedly killed.
- Shutdown and restart include 30 sec (15 on Windows Shutdown) grace periods with server announcements.
- Server output is saved to disk as `bedrock_service.log`.
- `bedrock_service.exe` can also be run as a console application for troubleshooting purposes.

## Usage

1. Place `bedrock_service.exe` in a directory (doesn't need to be the same place as `bedrock_server.exe`).
2. Start `bedrock_service.exe` for testing.
   If `bedrock_service.exe` is not in the same directory as `bedrock_server.exe` pass the `--workingDirectory "directory\containing\bedrock_server"` parameter.
3. Exit test mode with `CTRL + X`.
3. Create a new Windows Service entry:

   `.\sc.exe create MinecraftBedrockServer binPath= "path\to\bedrock_service.exe --workingDirectory \"directory\containing\bedrock_server\"" start= delayed-auto DispayName= "Minecraft Bedrock Dedicated Server" `

## Parameters

| Parameter	           | Default Value         | Description                        |
|----------------------|-----------------------|------------------------------------|
| `--workingDirectory` | Current Directory     | Directory of server code and logs. |
| `--executable`       | `bedrock_server.exe`  | Filename of server program.        |
| `--logFileName`      | `bedrock_service.log` | Filename of log output.            |
