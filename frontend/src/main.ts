import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

function dismissBootLoader(): void {
  const loader = document.getElementById('boot-loader');
  if (!loader) return;
  loader.classList.add('hidden');
  window.setTimeout(() => loader.remove(), 220);
}

bootstrapApplication(AppComponent, appConfig)
  .then(dismissBootLoader)
  .catch((err) => {
    dismissBootLoader();
    console.error(err);
  });
