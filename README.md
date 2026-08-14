# YouTube Music Game Bar

YouTube Music Game Bar is a genuine Xbox Game Bar UWP widget. It hosts the official [YouTube Music](https://music.youtube.com/) website in the WinUI 2 `Microsoft.UI.Xaml.Controls.WebView2` control; it does not use an unofficial YouTube API, scrape the site, or imitate Game Bar with an always-on-top desktop window.

## Architecture

- C# UWP XAML application packaged as AppX/MSIX
- Xbox Game Bar app extension: `microsoft.gameBarUIExtension`
- Widget ID: `YouTubeMusicWidget`
- `Microsoft.Gaming.XboxGameBar` activation through `ms-gamebarwidget`
- WinUI 2 WebView2 using the persistent default UWP user-data folder
- Persistent Mobile/Desktop user-agent selector; Mobile Android/Edge presentation is the default
- Scrollbars are visually hidden on every embedded page while scrolling itself remains enabled for mouse, touch, keyboard, and controller input
- Compact browser-style webpage zoom control with remembered levels from 50% through 200% and a one-click 100% reset
- One WebView instance for the widget lifetime, so resizing, pin changes, and focus changes do not deliberately reload the site
- WebView2 requests its low-memory target while the widget reports that it is hidden, then returns to normal when visible; playback and networking remain active
- Background-media capability and manual Windows media-control integration so active playback continues when Game Bar leaves the foreground
- Compact native Back, Forward, Refresh, and Home toolbar that can be collapsed
- Only the `internetClient` and `backgroundMediaPlayback` capabilities
- A registered Start-menu identity lets Windows label the system media card correctly; selecting it forwards directly to the Game Bar widget

The manifest's package-level proxy/stub registrations follow `Microsoft.Gaming.XboxGameBar` 7.3.2607010 metadata. The package readme incorrectly assigns `IXboxGameBarWidgetNotificationHost` the same interface ID as `IXboxGameBarWidgetHost10`; this project uses the distinct `6F68D392-E4A9-46F7-A024-5275BC2FE7BA` ID embedded in the package's private WinMD, avoiding deployment error `0x8007000D`.

## NuGet packages

| Package | Version |
| --- | ---: |
| `Microsoft.Gaming.XboxGameBar` | `7.3.2607010` |
| `Microsoft.UI.Xaml` | `2.8.7` |
| `Microsoft.Web.WebView2` | `1.0.4078.44` |
| `Microsoft.NETCore.UniversalWindowsPlatform` | `6.2.14` |

## Requirements

- Windows 11, or Windows 10 version 2004 (build 19041) or later
- Xbox Game Bar installed, updated, and enabled
- Microsoft Edge WebView2 Evergreen Runtime
- Visual Studio 2022 17.10 or later
- In Visual Studio Installer, **WinUI application development** with **Universal Windows Platform tools** selected
- Windows 11 SDK 10.0.22621.0
- Developer Mode enabled in Windows when loose-file deploying

Microsoft's current documentation identifies UWP XAML as the Game Bar widget architecture and WinUI 2.8 as the stable WinUI release for UWP. Useful references:

- [Xbox Game Bar SDK](https://learn.microsoft.com/en-us/gaming/game-bar/)
- [Game Bar app activation](https://learn.microsoft.com/en-us/gaming/game-bar/guide/app-activation)
- [Get started with WinUI 2 for UWP](https://learn.microsoft.com/en-us/windows/uwp/get-started/winui2/getting-started)
- [Get started with WebView2 in WinUI 2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winui2)
- [WebView2 user-data folders](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder)

## Build

### Visual Studio

1. Open `YouTubeMusicGameBar.sln`.
2. Select `Debug` and `x64` (or the architecture matching the target PC).
3. Restore NuGet packages when prompted.
4. Build > Build Solution.

### Command line

Run from a normal PowerShell prompt:

```powershell
.\scripts\Build.ps1 -Configuration Debug -Platform x64
```

The helper first checks for Visual Studio, the UWP XAML targets, and Windows SDK 10.0.22621.0, then restores and builds with Visual Studio MSBuild. `dotnet build` alone is not the supported build path for this traditional UWP XAML project.

For the optimized everyday build, use:

```powershell
.\scripts\Build.ps1 -Configuration Release -Platform x64
```

`Release` enables compiler optimisation and the .NET Native toolchain. Use `Debug` only while diagnosing a problem in Visual Studio.

## Branding assets

The master transparent logo is `Artwork\YouTubeMusicLogo.png`. Package tiles and both Game Bar icon variants are generated with a small transparent safe margin by running:

```powershell
.\scripts\Generate-LogoAssets.ps1
```

Pass `-Source` to use a replacement square PNG. The script regenerates the package tiles, Game Bar icons, and Windows shell target-size/unplated icon family with high-quality alpha-aware scaling. The shell variants ensure surfaces such as the taskbar and audio controls do not retain a fallback icon.

## Deploy and debug

1. In Windows Settings, enable **Developer Mode**.
2. Open the solution in Visual Studio and set the project as the startup project.
3. Select an architecture matching the machine, normally `x64`.
4. Right-click the project and select **Deploy**. This registers the app extension without opening a normal app window.
5. For Game Bar debugging, open project Properties > Debug and set **Launch application** to **No**.
6. Press F5, then press Win+G and open the widget. Visual Studio attaches when Game Bar activates it.

For a sideloadable package, right-click the project and choose **Publish/Store > Create App Packages**, select sideloading, choose the required architectures, and let Visual Studio create/sign the package. On the target machine, trust the generated certificate if needed, then run the generated `Add-AppDevPackage.ps1` as instructed by Visual Studio.

## Make an installer package

Game Bar widgets use Windows package registration, so the distributable format is an MSIX package rather than a traditional portable EXE.

1. Open `YouTubeMusicGameBar.sln` in Visual Studio.
2. Select **Release** and **x64** in the top toolbar.
3. In Solution Explorer, right-click the **YouTubeMusicGameBar project** (not the solution).
4. Select **Publish** (or **Store**) > **Create App Packages**.
5. Choose **Sideloading** when Visual Studio asks how the package will be distributed.
6. Select **Release** and **x64**. Add ARM64 only when preparing a package for an ARM-based Windows PC.
7. Create or select a signing certificate when prompted. For a private test package, Visual Studio can create a self-signed certificate whose publisher matches the manifest.
8. Select **Create**. Visual Studio writes the result under `YouTubeMusicGameBar\AppPackages`.

To install it on another PC, copy the complete generated version folder, including its `Dependencies` directory. Trust the included public certificate if Windows requests it, then right-click `Add-AppDevPackage.ps1` and select **Run with PowerShell**. Keep the certificate's private key private; recipients only need its public `.cer` certificate.

The normal build helper also emits an unsigned test MSIX because project signing is disabled. That file proves packaging succeeds, but Windows will not install it by double-clicking until it has been signed. Visual Studio's **Create App Packages** wizard performs the signing step.

## Why there is no truly portable version

An Xbox Game Bar widget cannot run as a standalone EXE copied from a ZIP. Game Bar discovers it through the installed package manifest and app-extension registration. The closest equivalent is copying the generated MSIX folder to another PC and running its installation script; Windows still registers and installs the widget for that user.

The package also registers a **YouTube Music Game Bar** Start-menu entry. Windows uses that identity to show the app name instead of **Unknown app** in its media panel. Selecting the Start entry forwards to the real Game Bar widget; it does not open a second desktop player.

The system media card uses a dedicated high-resolution transparent thumbnail so Windows receives the logo's alpha channel directly instead of generating a plated tile fallback. All package, shell, Game Bar, and media logos are generated from the same transparent master artwork.

## Open in Xbox Game Bar

1. Ensure deployment succeeded and Xbox Game Bar is current.
2. Press Win+G.
3. Open **Widget Menu**.
4. Select **YouTube Music**.
5. Move, resize, or pin the widget using Game Bar's normal controls.

The initial size is 900 x 900. Desktop mode supports 240 x 300 through 1600 x 1000. In Compact mode the widget reapplies horizontal and vertical resizing with Game Bar's recommended 464-to-900 epx width range. The toolbar's **Size** menu includes a near-19.5:9 phone portrait preset, a general portrait preset, and a 900 x 900 square preset.

## Test checklist

1. **Registration:** Deploy, press Win+G, and verify YouTube Music is in Widget Menu.
2. **Website:** Open it and verify `https://music.youtube.com/` appears inside the widget.
3. **Audio:** Play a song, dismiss Game Bar with Win+G, and verify it remains audible while playing the game. Reopen Game Bar and verify playback stayed in sync.
4. **Resize:** Resize repeatedly; the page and current playback should remain loaded.
5. **Pin:** Pin the widget, close the main overlay, and verify standard Game Bar pinned behavior.
6. **Session:** Sign in where permitted, close the widget, reopen it, and verify cookies/site preferences persist.
7. **Navigation:** Test Search, Home, Explore, Library, albums, artists, playlists, and playback.
8. **Authentication:** Test Google sign-in. If Google rejects the embedded browser, the widget reports the failed sign-in and does not attempt a bypass.

## Security and privacy behavior

- Only encrypted `https:` web navigation is permitted. Plain `http:` top-level navigation and popup requests are cancelled with an in-widget warning.
- HTTPS host navigation is left to YouTube/Google so required Google-owned supporting domains are not broken by a brittle host allowlist.
- `mailto:` and `tel:` may be sent to the registered Windows handler. Other non-HTTPS protocols, including `intent:`, are ignored.
- New HTTPS windows are redirected into the one existing WebView.
- TLS validation remains enabled and untouched.
- Host objects are disabled. Fixed document-start style helpers hide scrollbar artwork and apply the user-selected page zoom without reading page data. The only other host-initiated page commands are narrow play/pause calls made when Windows media buttons are pressed.
- The app does not access or log cookies, passwords, headers, tokens, or browser data.
- The default UWP WebView2 user-data folder is retained to preserve allowed cookies, local storage, and preferences.
- The **UA** menu selects Mobile or Desktop presentation and remembers the choice. Mobile reports an Android/Edge user-agent; Desktop restores WebView2's genuine default user-agent. Neither mode is used to bypass a Google sign-in rejection.

## Troubleshooting

### Widget is missing

- Confirm Visual Studio reported **Deploy succeeded**, not merely Build succeeded.
- Close any existing widget instance before redeploying.
- Update Xbox Game Bar in Microsoft Store, then reopen it.
- Confirm the package is installed with `Get-AppxPackage YouTubeMusicGameBar`.
- If the manifest was edited, ensure `microsoft.gameBarUIExtension` and `YouTubeMusicWidget` are unchanged.

### Manifest registration or deployment fails

- Match the selected architecture to the machine.
- Install UWP tools and Windows SDK 10.0.22621.0 through Visual Studio Installer.
- Enable Developer Mode.
- If sideloading, install/trust the package signing certificate for the current user.
- Uninstall an older package only if its publisher identity conflicts, then deploy again.

### WebView2 Runtime error or blank WebView

- Install or repair the [WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/).
- Update Microsoft Edge and Windows.
- Check firewalls, VPNs, DNS filters, enterprise policy, and Controlled Folder Access.
- Verify `https://music.youtube.com/` works in a normal Edge session.
- In a Debug build, inspect Visual Studio Output for lines prefixed `[YouTube Music Game Bar]`.

### Network failure

The widget shows WebView2's navigation status and offers Try again. It does not weaken certificate checks. Confirm the device clock, proxy, DNS, and internet connectivity.

### Google authentication limitation

Google controls whether an account may sign in through an embedded browser. WebView2 has a separate app profile, so it does not automatically share an existing Edge login. If Google blocks the flow, there is no supported application-side workaround; use the widget signed out or retry if Google later permits the flow. This project intentionally does not export cookies, intercept credentials, or alter Google's authentication flow.

Mobile mode overrides the browser user-agent to request YouTube Music's mobile web presentation; Desktop mode uses WebView2's genuine default value. The mobile override can change or break Google's web UI without notice and might make embedded Google sign-in less reliable. It is not a supported way to obtain the native Android YouTube Music application, and the widget does not attempt additional spoofing if authentication is refused.

Only sign in to a package that you built from this reviewed source (or obtained from a publisher you trust). A WebView host is technically capable of being modified to inspect web content or browser data even though this implementation does neither. The persistent WebView2 profile stores Google session cookies and other site data in the app's per-user UWP data folder so the session can survive widget restarts.

### Audio stops

- Check the Windows volume mixer for the widget process.
- Confirm the widget has not been explicitly closed with its close command. Dismissing Game Bar with Win+G is supported; closing the widget ends its WebView and playback.
- Confirm Windows media controls show **YouTube Music** while a song is playing. The package uses that integration to retain background-audio eligibility.
- Check YouTube Music's own playback state and account restrictions.
- Game Bar, Windows power management, games with exclusive audio modes, and YouTube policies can affect background playback independently of this app.

## Known limitations

- Google may disallow sign-in from the embedded WebView2 environment at any time.
- The WebView2 profile is app-specific and is not the user's normal Microsoft Edge profile.
- Popup-based flows are kept in the same view to prevent uncontrolled windows; a site flow that strictly requires a separate popup may not work.
- Playback availability, ads, DRM, account entitlements, regional restrictions, and YouTube Premium behavior remain controlled by YouTube.
- Full interaction with pinned widgets follows Xbox Game Bar's focus and click-through rules.
