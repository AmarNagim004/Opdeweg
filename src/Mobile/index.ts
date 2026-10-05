// The background location task must be defined before anything else so the OS can deliver
// updates even when the app is relaunched headlessly with the screen locked.
import './src/services/location/locationTask';
import { registerRootComponent } from 'expo';
import { initialiseVoiceRuntime } from './src/services/voice/audioSession';
import App from './src/app/App';

initialiseVoiceRuntime();
registerRootComponent(App);
