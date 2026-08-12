import { Routes } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { ComposeComponent } from './components/compose/compose.component';
import { ResumeComponent } from './components/resume/resume.component';
import { AtsComponent } from './components/ats/ats.component';
import { MergeComponent } from './components/merge/merge.component';
import { PreviewComponent } from './components/preview/preview.component';
import { SettingsComponent } from './components/settings/settings.component';
import { HistoryComponent } from './components/history/history.component';
import { authGuard } from './services/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'home', component: HomeComponent },
  { path: 'compose', component: ComposeComponent },
  { path: 'resume', component: ResumeComponent },
  { path: 'ats', component: AtsComponent },
  { path: 'merge', component: MergeComponent },
  { path: 'preview', component: PreviewComponent },
  { path: 'settings', component: SettingsComponent },
  { path: 'history', component: HistoryComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'home' }
];
