import { isDriveServiceAvailable, startDriveService, stopDriveService } from '../../../modules/opdeweg-drive-service';
import { logger } from '../../utils/logger';

/** Android: one foreground service (location|microphone) for the whole drive. iOS: no-op. */
export const driveService = {
  /** True when this platform keeps the drive alive with our own service. */
  available: isDriveServiceAvailable,

  async start(): Promise<boolean> {
    if (!isDriveServiceAvailable) {
      return false;
    }

    try {
      await startDriveService({
        title: 'Opdeweg · Driving',
        body: 'Sharing your approximate position with nearby drivers. Voice is live when drivers are close.',
      });
      return true;
    } catch (error) {
      logger.warn('drive_service.start_failed', { error: String(error) });
      return false;
    }
  },

  async stop(): Promise<void> {
    await stopDriveService().catch(() => undefined);
  },
};
