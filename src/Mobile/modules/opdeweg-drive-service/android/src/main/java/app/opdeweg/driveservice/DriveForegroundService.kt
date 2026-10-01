package app.opdeweg.driveservice

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat

/**
 * Foreground service that represents an active Opdeweg driving session.
 *
 * Service types are computed from the permissions that are actually granted, so a revoked
 * microphone permission degrades to a location-only service instead of crashing on Android 14+.
 */
class DriveForegroundService : Service() {

  override fun onBind(intent: Intent?): IBinder? = null

  override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
    if (intent?.action == ACTION_STOP) {
      stopForegroundCompat()
      stopSelf()
      return START_NOT_STICKY
    }

    val title = intent?.getStringExtra(EXTRA_TITLE) ?: "Driving"
    val body = intent?.getStringExtra(EXTRA_BODY) ?: "Sharing your approximate position with nearby drivers."
    ensureChannel()

    try {
      ServiceCompat.startForeground(this, NOTIFICATION_ID, buildNotification(title, body), serviceTypes())
      isRunning = true
    } catch (error: RuntimeException) {
      // E.g. ForegroundServiceStartNotAllowedException or a missing permission: never crash the app.
      isRunning = false
      stopSelf()
    }

    // Not sticky: a driving session is always (re)started explicitly by the user.
    return START_NOT_STICKY
  }

  override fun onDestroy() {
    isRunning = false
    super.onDestroy()
  }

  private fun serviceTypes(): Int {
    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return 0
    var types = 0
    if (granted(Manifest.permission.ACCESS_FINE_LOCATION) || granted(Manifest.permission.ACCESS_COARSE_LOCATION)) {
      types = types or ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION
    }
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R && granted(Manifest.permission.RECORD_AUDIO)) {
      types = types or ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE
    }
    return types
  }

  private fun granted(permission: String): Boolean =
    ContextCompat.checkSelfPermission(this, permission) == PackageManager.PERMISSION_GRANTED

  private fun ensureChannel() {
    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
    val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
    if (manager.getNotificationChannel(CHANNEL_ID) == null) {
      val channel = NotificationChannel(CHANNEL_ID, "Driving session", NotificationManager.IMPORTANCE_LOW).apply {
        description = "Shown while Opdeweg shares your position and voice with nearby drivers."
        setShowBadge(false)
      }
      manager.createNotificationChannel(channel)
    }
  }

  private fun buildNotification(title: String, body: String): Notification {
    val launchIntent = packageManager.getLaunchIntentForPackage(packageName)
    val contentIntent = launchIntent?.let {
      PendingIntent.getActivity(this, 0, it, PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
    }

    return NotificationCompat.Builder(this, CHANNEL_ID)
      .setContentTitle(title)
      .setContentText(body)
      .setStyle(NotificationCompat.BigTextStyle().bigText(body))
      .setSmallIcon(applicationInfo.icon)
      .setOngoing(true)
      .setOnlyAlertOnce(true)
      .setCategory(NotificationCompat.CATEGORY_NAVIGATION)
      .setForegroundServiceBehavior(NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE)
      .setContentIntent(contentIntent)
      .build()
  }

  private fun stopForegroundCompat() {
    ServiceCompat.stopForeground(this, ServiceCompat.STOP_FOREGROUND_REMOVE)
    isRunning = false
  }

  companion object {
    const val ACTION_START = "app.opdeweg.driveservice.START"
    const val ACTION_STOP = "app.opdeweg.driveservice.STOP"
    const val EXTRA_TITLE = "title"
    const val EXTRA_BODY = "body"
    private const val CHANNEL_ID = "opdeweg-driving"
    private const val NOTIFICATION_ID = 4711

    @Volatile
    var isRunning: Boolean = false
      private set
  }
}
