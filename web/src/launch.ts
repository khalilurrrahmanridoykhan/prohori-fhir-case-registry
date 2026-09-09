import { launchSmart } from './smart';
launchSmart().catch((error: Error) => { document.getElementById('status')!.textContent = error.message; });
