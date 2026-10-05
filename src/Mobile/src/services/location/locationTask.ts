import * as TaskManager from 'expo-task-manager';
import * as Location from 'expo-location';
import { locationReporter } from './locationReporter';
import { driveIntent } from './driveIntent';
import { toSample } from './locationService';
import { LOCATION_TASK } from './locationTaskName';
import { useLocationStore } from '../../store/locationStore';
import { logger } from '../../utils/logger';

interface LocationTaskData {
  locations?: Location.LocationObject[];
}

/**
 * Background location task. Must be defined at module scope (imported from index.ts) so it is
 * registered before the OS delivers updates — including when the app is relaunched headlessly.
 */
TaskManager.defineTask<LocationTaskData>(LOCATION_TASK, async ({ data, error }) => {
  if (error) {
    logger.warn('location.task_error', { message: error.message });
    useLocationStore.getState().setGps('unavailable');
    return;
  }

  if (!(await driveIntent.get())) {
    // A stale registration outlived its driving session: stop tracking immediately.
    await Location.stopLocationUpdatesAsync(LOCATION_TASK).catch(() => undefined);
    return;
  }

  const latest = data?.locations?.at(-1);
  if (!latest) {
    return;
  }

  const sample = toSample(latest);
  if (sample) {
    await locationReporter.handleSample(sample);
  }
});
