# BCLMS overdue reminder Windows service

This worker checks immediately on startup and then every hour. It emails each active official assigned to a pending workflow step for an application submitted more than 21 days ago. `dbo.OverdueApplicationReminderLog` records successful sends and prevents another email to the same official for the same application on the same UTC day.

## Configure and publish

1. Publish the service:

   ```powershell
   dotnet publish .\BclmsOverdueReminderService.csproj -c Release -r win-x64 --self-contained false -o C:\Services\BclmsOverdueReminderService
   ```

2. In the published `appsettings.json`, set `ConnectionStrings:BclmsConnection` and `Email:Office365:Password`. Do not commit those values to source control.

3. From an elevated PowerShell prompt, register and start it:

   ```powershell
   sc.exe create "BCLMS Overdue Reminder Service" binPath= "C:\Services\BclmsOverdueReminderService\BclmsOverdueReminderService.exe" start= auto
   sc.exe start "BCLMS Overdue Reminder Service"
   ```

The service runs under Local System by default. If its SQL authentication or SMTP/network policy requires a service account, configure that account in Windows Services before starting it.
