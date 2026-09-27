@AGENTS.md

## Tray app conventions

This is a tray-only WinUI 3 app built from the `winui-tray-app` skill scaffold on top of `winapp new`. Load that skill before changing app lifetime, the flyout, the tray icon, polling, settings, or secrets.

- Launch with `winapp run` (or `dotnet run`); never register the package by hand. Use `winapp run --clean` to test first-run behavior.
- `App.xaml.cs` owns the tray icon, single instance, and the only exit path (the Exit menu). There is no main window.
- The flyout (`Views/TrayFlyoutWindow`) is hidden on dismiss and closed after a minute hidden. Anything it starts, it must stop in `FlyoutPage.OnHidden` / `Dispose`.
- The app is offline-only: the scaffold's network and secrets services were removed, and the manifest has no `internetClient`.
- Tasks live in `LocalState	asks.json`, owned by `Services/TodoStore`. Every change goes through `TodoStore.Board` (a `TrayDo.Core` `TodoBoard`) and then `TodoStore.Commit()`, which saves and raises `Changed`.
- The tray icon becomes a drawn count (`Services/TrayBadge` + `BadgeFont`, pixels in `TrayDo.Core/Tray/BadgeIcon`) while the hit list has open tasks. It updates only on `TodoStore.Changed` and theme changes, never on a timer.
- The hit list is an ordered subset of task ids. Removing from it or "Clear all" never completes or deletes a task.
- Pure logic goes in `TrayDo.Core` (net10.0, no Windows APIs) and is covered by `TrayDo.Tests` (`dotnet test TrayDo.Tests`). The app csproj excludes both folders via `DefaultItemExcludes`.
