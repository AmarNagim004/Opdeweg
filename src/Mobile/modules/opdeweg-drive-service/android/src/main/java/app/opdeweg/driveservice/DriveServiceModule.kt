package app.opdeweg.driveservice

import android.content.Intent
import androidx.core.content.ContextCompat
import expo.modules.kotlin.exception.Exceptions
import expo.modules.kotlin.modules.Module
import expo.modules.kotlin.modules.ModuleDefinition
import expo.modules.kotlin.records.Field
import expo.modules.kotlin.records.Record

class DriveNotificationOptions : Record {
  @Field
  val title: String = "Onderweg"

  @Field
  val body: String = "Je deelt je geschatte locatie met rijders in de buurt."
}

class DriveServiceModule : Module() {
  private val context
    get() = appContext.reactContext ?: throw Exceptions.ReactContextLost()

  override fun definition() = ModuleDefinition {
    Name("OpdewegDriveService")

    /** Starts (or updates) the driving foreground service. Must be called while the app is visible. */
    AsyncFunction("start") { options: DriveNotificationOptions ->
      val intent = Intent(context, DriveForegroundService::class.java).apply {
        action = DriveForegroundService.ACTION_START
        putExtra(DriveForegroundService.EXTRA_TITLE, options.title)
        putExtra(DriveForegroundService.EXTRA_BODY, options.body)
      }
      ContextCompat.startForegroundService(context, intent)
    }

    AsyncFunction("stop") {
      val intent = Intent(context, DriveForegroundService::class.java).apply {
        action = DriveForegroundService.ACTION_STOP
      }
      if (DriveForegroundService.isRunning) {
        context.startService(intent)
      } else {
        context.stopService(intent)
      }
    }

    Function("isRunning") {
      DriveForegroundService.isRunning
    }
  }
}
