ResoDrive 0.3 is a focused public preview.

It mounts Nextcloud, WebDAV, and SFTP storage as Windows drives, runs copy and
mirror jobs, and keeps active work running when the management window is closed.
Optional sign-in startup now uses Windows Task Scheduler without an artificial
delay.

Mount recovery uses bounded retry intervals for slow or intermittent links.
Application and rclone downloads can continue after a dropped connection and are
verified before use.

The normal download is `ResoDrive-Setup.exe`. It keeps updates small by using the
shared .NET 10 Desktop Runtime and downloads that prerequisite only when required.
WinFsp remains necessary only for drive mounting.
