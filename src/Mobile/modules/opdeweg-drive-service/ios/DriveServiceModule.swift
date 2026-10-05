import ExpoModulesCore

/// iOS needs no service: background execution comes from the `location` and `audio`
/// UIBackgroundModes plus an active location session and audio session. The module exists
/// so JavaScript can call the same API on both platforms.
public class DriveServiceModule: Module {
  public func definition() -> ModuleDefinition {
    Name("OpdewegDriveService")

    AsyncFunction("start") { (_: [String: Any]) in }

    AsyncFunction("stop") {}

    Function("isRunning") { false }
  }
}
